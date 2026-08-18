using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Features.Orders;
using MicroShop.OrderService.Infrastructure.Messaging;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Features.Reconciliation;

public sealed class OrderReconciliationService(
    OrderDbContext dbContext,
    IProductInventoryClient productInventoryClient,
    IOrderOutboxWriter orderOutboxWriter)
{
    public async Task<OrderReconciliationOutcome> ReconcileAsync(
        Guid orderId,
        string traceId,
        string? traceParent,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var order = await dbContext.Orders
            .Include(candidate => candidate.Items)
            .Include(candidate => candidate.InventoryRequestItems)
            .Include(candidate => candidate.StateHistory)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
        if (order is null)
        {
            return OrderReconciliationOutcome.NotFound(
                ReconciliationOperations.Inventory,
                now);
        }

        if (order.Status is not (OrderStatuses.InventoryUnknown or OrderStatuses.CancellationPending))
        {
            return OrderReconciliationOutcome.Failure(
                order,
                OperationFor(order.Status),
                ReconciliationOutcomes.Conflict,
                null,
                null,
                now,
                StatusCodes.Status409Conflict,
                "Order state conflict",
                "Only inventory_unknown and cancellation_pending orders can be reconciled.",
                "RECONCILIATION_STATE_CONFLICT");
        }

        var operation = order.Status == OrderStatuses.InventoryUnknown
            ? ReconciliationOperations.Inventory
            : ReconciliationOperations.Cancellation;
        var lookup = await productInventoryClient.GetReservationByOrderAsync(
            new ProductReservationLookupRequest(order.Id, traceParent),
            cancellationToken);

        try
        {
            return operation == ReconciliationOperations.Inventory
                ? await ReconcileInventoryAsync(order, lookup, traceId, traceParent, cancellationToken)
                : await ReconcileCancellationAsync(order, lookup, traceId, traceParent, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrderReconciliationOutcome.Failure(
                order,
                operation,
                ReconciliationOutcomes.Conflict,
                lookup.Status,
                lookup.ReservationId,
                DateTimeOffset.UtcNow,
                StatusCodes.Status409Conflict,
                "Order state conflict",
                "The order changed while reconciliation was being recorded. Retry the reconciliation query.",
                "RECONCILIATION_CONCURRENCY_CONFLICT");
        }
    }

    private async Task<OrderReconciliationOutcome> ReconcileInventoryAsync(
        Order order,
        ProductReservationLookupResult lookup,
        string traceId,
        string? traceParent,
        CancellationToken cancellationToken)
    {
        if (lookup.Failure is ProductReservationFailure.DependencyUnavailable
            or ProductReservationFailure.OutcomeUnknown)
        {
            var detail = lookup.Detail ?? "The Product reservation lookup remains unavailable.";
            var now = DateTimeOffset.UtcNow;
            await RecordAuditAsync(
                order,
                ReconciliationOperations.Inventory,
                ReconciliationOutcomes.DependencyUnavailable,
                lookup,
                detail,
                traceId,
                now,
                cancellationToken);
            return OrderReconciliationOutcome.Failure(
                order,
                ReconciliationOperations.Inventory,
                ReconciliationOutcomes.DependencyUnavailable,
                lookup.Status,
                lookup.ReservationId,
                now,
                StatusCodes.Status503ServiceUnavailable,
                "Product Service unavailable",
                detail,
                "RECONCILIATION_DEPENDENCY_UNAVAILABLE");
        }

        if (lookup.Failure is ProductReservationFailure.InvalidResponse)
        {
            return await RecordConflictAsync(
                order,
                ReconciliationOperations.Inventory,
                lookup,
                "The Product Service returned an invalid reservation lookup.",
                traceId,
                cancellationToken);
        }

        if (lookup.IsReservationMissing || lookup.Status == "released")
        {
            var detail = lookup.IsReservationMissing
                ? "Product confirms that no reservation exists for this order."
                : "Product confirms that the reservation was already released.";
            var code = order.FailureCode == "ORDER_TOTAL_LIMIT_EXCEEDED"
                ? "ORDER_TOTAL_LIMIT_EXCEEDED"
                : "INVENTORY_RESERVATION_NOT_FOUND";
            return await RejectInventoryAsync(
                order,
                lookup,
                code,
                detail,
                traceId,
                cancellationToken);
        }

        if (lookup.Failure is not ProductReservationFailure.None
            || !string.Equals(lookup.Status, "reserved", StringComparison.Ordinal))
        {
            return await RecordConflictAsync(
                order,
                ReconciliationOperations.Inventory,
                lookup,
                "The Product reservation state cannot be reconciled safely.",
                traceId,
                cancellationToken);
        }

        if (!TryMatchReservation(
                order,
                lookup,
                out var mismatchDetail,
                out var exceedsOrderLimit))
        {
            if (!exceedsOrderLimit)
            {
                return await RecordConflictAsync(
                    order,
                    ReconciliationOperations.Inventory,
                    lookup,
                    mismatchDetail,
                    traceId,
                    cancellationToken);
            }
        }

        if (exceedsOrderLimit || order.FailureCode == "ORDER_TOTAL_LIMIT_EXCEEDED")
        {
            var release = await productInventoryClient.ReleaseAsync(
                new ProductReleaseRequest(order.Id, traceParent),
                cancellationToken);
            if (!release.IsSuccess)
            {
                var detail = release.Detail
                    ?? "The Product reservation remains reserved and could not be released safely.";
                var now = DateTimeOffset.UtcNow;
                await RecordAuditAsync(
                    order,
                    ReconciliationOperations.Inventory,
                    ReconciliationOutcomes.StillPending,
                    lookup,
                    detail,
                    traceId,
                    now,
                    cancellationToken);
                return OrderReconciliationOutcome.Failure(
                    order,
                    ReconciliationOperations.Inventory,
                    ReconciliationOutcomes.StillPending,
                    lookup.Status,
                    lookup.ReservationId,
                    now,
                    StatusCodes.Status503ServiceUnavailable,
                    "Inventory outcome still unknown",
                    detail,
                    "RECONCILIATION_RELEASE_UNKNOWN");
            }

            return await RejectInventoryAsync(
                order,
                lookup,
                "ORDER_TOTAL_LIMIT_EXCEEDED",
                "The reservation was released because the order total exceeds the allowed limit.",
                traceId,
                cancellationToken);
        }

        var confirmedAtUtc = DateTimeOffset.UtcNow;
        var fromStatus = order.Status;
        foreach (var snapshot in lookup.Items)
        {
            order.AddItem(OrderItem.Create(
                snapshot.ProductId,
                snapshot.ProductName,
                snapshot.UnitPrice,
                snapshot.Quantity));
        }

        order.ClearFailure(confirmedAtUtc);
        order.TransitionTo(
            OrderStatuses.Confirmed,
            "RECONCILIATION_RESERVATION_CONFIRMED",
            traceId,
            confirmedAtUtc);
        orderOutboxWriter.AddConfirmed(order, traceParent);
        await RecordAuditAsync(
            order,
            ReconciliationOperations.Inventory,
            ReconciliationOutcomes.Confirmed,
            lookup,
            "Product confirms a matching reserved inventory outcome.",
            traceId,
            confirmedAtUtc,
            cancellationToken,
            fromStatus);
        return OrderReconciliationOutcome.Success(
            order,
            ReconciliationOperations.Inventory,
            ReconciliationOutcomes.Confirmed,
            changed: true,
            lookup.Status,
            lookup.ReservationId,
            confirmedAtUtc,
            "The Order was confirmed from the matching Product reservation.");
    }

    private async Task<OrderReconciliationOutcome> ReconcileCancellationAsync(
        Order order,
        ProductReservationLookupResult lookup,
        string traceId,
        string? traceParent,
        CancellationToken cancellationToken)
    {
        if (lookup.Failure is ProductReservationFailure.DependencyUnavailable
            or ProductReservationFailure.OutcomeUnknown)
        {
            var detail = lookup.Detail ?? "The Product reservation lookup remains unavailable.";
            var now = DateTimeOffset.UtcNow;
            await RecordAuditAsync(
                order,
                ReconciliationOperations.Cancellation,
                ReconciliationOutcomes.DependencyUnavailable,
                lookup,
                detail,
                traceId,
                now,
                cancellationToken);
            return OrderReconciliationOutcome.Failure(
                order,
                ReconciliationOperations.Cancellation,
                ReconciliationOutcomes.DependencyUnavailable,
                lookup.Status,
                lookup.ReservationId,
                now,
                StatusCodes.Status503ServiceUnavailable,
                "Product Service unavailable",
                detail,
                "RECONCILIATION_DEPENDENCY_UNAVAILABLE");
        }

        if (lookup.Failure is ProductReservationFailure.InvalidResponse
            || (lookup.Failure is ProductReservationFailure.None
                && lookup.Status is not ("reserved" or "released")))
        {
            return await RecordConflictAsync(
                order,
                ReconciliationOperations.Cancellation,
                lookup,
                "The Product reservation state cannot be reconciled safely.",
                traceId,
                cancellationToken);
        }

        if (lookup.IsReservationMissing || lookup.Status == "released")
        {
            return await CompleteCancellationAsync(
                order,
                lookup,
                traceId,
                "Product confirms that no stock remains reserved for this Order.",
                cancellationToken);
        }

        if (lookup.Status != "reserved")
        {
            return await RecordConflictAsync(
                order,
                ReconciliationOperations.Cancellation,
                lookup,
                "The Product reservation state is not a supported cancellation state.",
                traceId,
                cancellationToken);
        }

        var release = await productInventoryClient.ReleaseAsync(
            new ProductReleaseRequest(order.Id, traceParent),
            cancellationToken);
        if (!release.IsSuccess)
        {
            var detail = release.Detail
                ?? "The Product reservation release outcome remains unknown.";
            var now = DateTimeOffset.UtcNow;
            await RecordAuditAsync(
                order,
                ReconciliationOperations.Cancellation,
                ReconciliationOutcomes.StillPending,
                lookup,
                detail,
                traceId,
                now,
                cancellationToken);
            return OrderReconciliationOutcome.Failure(
                order,
                ReconciliationOperations.Cancellation,
                ReconciliationOutcomes.StillPending,
                lookup.Status,
                lookup.ReservationId,
                now,
                StatusCodes.Status503ServiceUnavailable,
                "Cancellation outcome still unknown",
                detail,
                "RECONCILIATION_RELEASE_UNKNOWN");
        }

        return await CompleteCancellationAsync(
            order,
            lookup,
            traceId,
            "Product release completed idempotently during reconciliation.",
            cancellationToken);
    }

    private async Task<OrderReconciliationOutcome> RejectInventoryAsync(
        Order order,
        ProductReservationLookupResult lookup,
        string code,
        string detail,
        string traceId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var fromStatus = order.Status;
        order.RecordFailure(code, detail, now);
        order.TransitionTo(OrderStatuses.Rejected, code, traceId, now);
        await RecordAuditAsync(
            order,
            ReconciliationOperations.Inventory,
            ReconciliationOutcomes.Rejected,
            lookup,
            detail,
            traceId,
            now,
            cancellationToken,
            fromStatus);
        return OrderReconciliationOutcome.Success(
            order,
            ReconciliationOperations.Inventory,
            ReconciliationOutcomes.Rejected,
            changed: true,
            lookup.Status,
            lookup.ReservationId,
            now,
            detail);
    }

    private async Task<OrderReconciliationOutcome> CompleteCancellationAsync(
        Order order,
        ProductReservationLookupResult lookup,
        string traceId,
        string detail,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var fromStatus = order.Status;
        order.ClearFailure(now);
        order.TransitionTo(
            OrderStatuses.Cancelled,
            "RECONCILIATION_CANCELLATION_COMPLETED",
            traceId,
            now);
        await RecordAuditAsync(
            order,
            ReconciliationOperations.Cancellation,
            ReconciliationOutcomes.Cancelled,
            lookup,
            detail,
            traceId,
            now,
            cancellationToken,
            fromStatus);
        return OrderReconciliationOutcome.Success(
            order,
            ReconciliationOperations.Cancellation,
            ReconciliationOutcomes.Cancelled,
            changed: true,
            lookup.Status,
            lookup.ReservationId,
            now,
            detail);
    }

    private async Task<OrderReconciliationOutcome> RecordConflictAsync(
        Order order,
        string operation,
        ProductReservationLookupResult lookup,
        string detail,
        string traceId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await RecordAuditAsync(
            order,
            operation,
            ReconciliationOutcomes.Conflict,
            lookup,
            detail,
            traceId,
            now,
            cancellationToken);
        return OrderReconciliationOutcome.Failure(
            order,
            operation,
            ReconciliationOutcomes.Conflict,
            lookup.Status,
            lookup.ReservationId,
            now,
            StatusCodes.Status409Conflict,
            "Reconciliation conflict",
            detail,
            "RECONCILIATION_CONFLICT");
    }

    private async Task RecordAuditAsync(
        Order order,
        string operation,
        string outcome,
        ProductReservationLookupResult lookup,
        string detail,
        string traceId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken,
        string? fromStatus = null)
    {
        dbContext.ReconciliationAudits.Add(OrderReconciliationAudit.Create(
            order.Id,
            operation,
            fromStatus ?? order.Status,
            order.Status,
            lookup.Status,
            lookup.ReservationId,
            outcome,
            detail,
            traceId,
            occurredAtUtc));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool TryMatchReservation(
        Order order,
        ProductReservationLookupResult lookup,
        out string detail,
        out bool exceedsOrderLimit)
    {
        detail = "The Product reservation does not match the Order inventory intent.";
        exceedsOrderLimit = false;
        if (lookup.Currency != "VND"
            || lookup.ReservationId is null
            || lookup.ReservationId == Guid.Empty
            || lookup.Items.Count != order.InventoryRequestItems.Count
            || order.InventoryRequestItems.Count == 0)
        {
            return false;
        }

        var requestedItems = order.InventoryRequestItems.ToDictionary(item => item.ProductId);
        var returnedProductIds = new HashSet<Guid>();
        decimal total = 0;
        foreach (var item in lookup.Items)
        {
            if (!returnedProductIds.Add(item.ProductId)
                || item.ProductId == Guid.Empty
                || !requestedItems.TryGetValue(item.ProductId, out var requestedItem)
                || requestedItem.Quantity != item.Quantity
                || item.Quantity <= 0
                || string.IsNullOrWhiteSpace(item.ProductName)
                || item.ProductName.Trim().Length > 200
                || item.UnitPrice < 0)
            {
                return false;
            }

            var expectedSubtotal = decimal.Round(
                item.UnitPrice * item.Quantity,
                2,
                MidpointRounding.ToEven);
            if (item.Subtotal != expectedSubtotal)
            {
                return false;
            }

            total += item.Subtotal;
        }

        var normalizedTotal = decimal.Round(total, 2, MidpointRounding.ToEven);
        if (normalizedTotal != lookup.TotalAmount)
        {
            return false;
        }

        if (normalizedTotal > OrderValidator.MaxOrderTotal)
        {
            detail = "The Product reservation total exceeds the Order total limit.";
            exceedsOrderLimit = true;
            return false;
        }

        return true;
    }

    private static string OperationFor(string status)
    {
        return status == OrderStatuses.CancellationPending
            ? ReconciliationOperations.Cancellation
            : ReconciliationOperations.Inventory;
    }
}
