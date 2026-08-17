using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Infrastructure.Messaging;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Features.Orders;

public sealed class OrderApplicationService(
    OrderDbContext dbContext,
    IProductInventoryClient productInventoryClient,
    IOrderOutboxWriter orderOutboxWriter)
{
    public async Task<CreateOrderOutcome> CreateAsync(
        CreateOrderRequest request,
        string traceId,
        string? traceParent,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(
            Guid.NewGuid(),
            request.CustomerName!.Trim(),
            request.CustomerEmail!.Trim(),
            now);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        var requestedItems = request.Items!
            .Select(item => new ProductReservationRequestItem(item.ProductId, item.Quantity))
            .ToArray();
        var reservation = await productInventoryClient.ReserveAsync(
            new ProductReservationRequest(order.Id, requestedItems, traceParent),
            cancellationToken);

        if (!reservation.IsSuccess)
        {
            return await HandleReservationFailureAsync(
                order,
                reservation,
                traceId,
                cancellationToken);
        }

        if (!TryValidateReservation(
                reservation,
                requestedItems,
                out var total,
                out var invalidReservationDetail))
        {
            return await MarkUnknownAsync(
                order,
                "INVENTORY_OUTCOME_UNKNOWN",
                invalidReservationDetail,
                traceId,
                StatusCodes.Status503ServiceUnavailable,
                "Inventory outcome unknown",
                cancellationToken);
        }

        if (total > OrderValidator.MaxOrderTotal)
        {
            var release = await productInventoryClient.ReleaseAsync(
                new ProductReleaseRequest(order.Id, traceParent),
                cancellationToken);
            if (!release.IsSuccess)
            {
                var releaseCode = release.Failure is ProductReservationFailure.DependencyUnavailable
                    ? "PRODUCT_SERVICE_UNAVAILABLE"
                    : "INVENTORY_OUTCOME_UNKNOWN";
                var releaseDetail = release.Failure is ProductReservationFailure.DependencyUnavailable
                    ? "The Product Service is unavailable while releasing the reservation."
                    : "The inventory release outcome could not be determined safely.";
                return await MarkUnknownAsync(
                    order,
                    releaseCode,
                    releaseDetail,
                    traceId,
                    StatusCodes.Status503ServiceUnavailable,
                    "Inventory outcome unknown",
                    cancellationToken);
            }

            return await RejectAsync(
                order,
                "ORDER_TOTAL_LIMIT_EXCEEDED",
                $"The maximum order total is {OrderValidator.MaxOrderTotal:0.##} VND.",
                traceId,
                cancellationToken,
                StatusCodes.Status400BadRequest,
                "Validation error");
        }

        foreach (var snapshot in reservation.Items)
        {
            order.AddItem(OrderItem.Create(
                snapshot.ProductId,
                snapshot.ProductName,
                snapshot.UnitPrice,
                snapshot.Quantity));
        }

        order.TransitionTo(
            OrderStatuses.Confirmed,
            "PRODUCT_RESERVATION_CONFIRMED",
            traceId,
            DateTimeOffset.UtcNow);
        orderOutboxWriter.AddConfirmed(order, traceParent);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateOrderOutcome.Success(order);
    }

    public async Task<CancelOrderOutcome> CancelAsync(
        Guid orderId,
        string traceId,
        string? traceParent,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .Include(candidate => candidate.Items)
            .SingleOrDefaultAsync(candidate => candidate.Id == orderId, cancellationToken);
        if (order is null)
        {
            return CancelOrderOutcome.NotFound();
        }

        if (order.Status == OrderStatuses.Cancelled)
        {
            return CancelOrderOutcome.Success(order, idempotentReplay: true);
        }

        if (order.Status != OrderStatuses.Confirmed)
        {
            return CancelOrderOutcome.Failure(
                order,
                StatusCodes.Status409Conflict,
                "Order state conflict",
                "The order can only be cancelled from the confirmed state.",
                "ORDER_STATE_CONFLICT");
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        order.TransitionTo(
            OrderStatuses.CancellationPending,
            "CANCELLATION_STARTED",
            traceId,
            startedAtUtc);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CancelOrderOutcome.Failure(
                order,
                StatusCodes.Status409Conflict,
                "Order state conflict",
                "The order changed while cancellation was starting.",
                "ORDER_STATE_CONFLICT");
        }

        var release = await productInventoryClient.ReleaseAsync(
            new ProductReleaseRequest(order.Id, traceParent),
            cancellationToken);
        if (!release.IsSuccess)
        {
            var failure = MapReleaseFailure(release);
            var failedAtUtc = DateTimeOffset.UtcNow;
            order.RecordFailure(failure.Code, failure.Detail, failedAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            return CancelOrderOutcome.Failure(
                order,
                failure.StatusCode,
                failure.Title,
                failure.Detail,
                failure.Code);
        }

        order.TransitionTo(
            OrderStatuses.Cancelled,
            "PRODUCT_RESERVATION_RELEASED",
            traceId,
            DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CancelOrderOutcome.Success(order, idempotentReplay: false);
    }

    private async Task<CreateOrderOutcome> HandleReservationFailureAsync(
        Order order,
        ProductReservationResult reservation,
        string traceId,
        CancellationToken cancellationToken)
    {
        return reservation.Failure switch
        {
            ProductReservationFailure.ProductNotFound => await RejectAsync(
                order,
                "PRODUCT_NOT_FOUND",
                $"Product '{reservation.ProductId}' was not found.",
                traceId,
                cancellationToken,
                StatusCodes.Status404NotFound,
                "Product not found"),
            ProductReservationFailure.ProductInactive => await RejectAsync(
                order,
                "PRODUCT_INACTIVE",
                $"Product '{reservation.ProductId}' is inactive.",
                traceId,
                cancellationToken,
                StatusCodes.Status409Conflict,
                "Product inactive"),
            ProductReservationFailure.InsufficientStock => await RejectAsync(
                order,
                "INSUFFICIENT_STOCK",
                $"Product '{reservation.ProductId}' has only {reservation.AvailableStock} unit(s) available.",
                traceId,
                cancellationToken,
                StatusCodes.Status409Conflict,
                "Insufficient stock"),
            ProductReservationFailure.RequestMismatch => await RejectAsync(
                order,
                "RESERVATION_REQUEST_MISMATCH",
                reservation.Detail ?? "The reservation request does not match the existing order reservation.",
                traceId,
                cancellationToken,
                StatusCodes.Status409Conflict,
                "Reservation request mismatch"),
            ProductReservationFailure.ReservationStateConflict => await RejectAsync(
                order,
                "RESERVATION_STATE_CONFLICT",
                reservation.Detail ?? "The Product reservation is in an incompatible state.",
                traceId,
                cancellationToken,
                StatusCodes.Status409Conflict,
                "Reservation state conflict"),
            ProductReservationFailure.DependencyUnavailable => await MarkUnknownAsync(
                order,
                "PRODUCT_SERVICE_UNAVAILABLE",
                reservation.Detail ?? "The Product Service is unavailable.",
                traceId,
                StatusCodes.Status503ServiceUnavailable,
                "Product Service unavailable",
                cancellationToken),
            ProductReservationFailure.OutcomeUnknown => await MarkUnknownAsync(
                order,
                "INVENTORY_OUTCOME_UNKNOWN",
                reservation.Detail ?? "The inventory reservation outcome could not be determined safely.",
                traceId,
                StatusCodes.Status503ServiceUnavailable,
                "Inventory outcome unknown",
                cancellationToken),
            _ => await MarkUnknownAsync(
                order,
                "INVENTORY_OUTCOME_UNKNOWN",
                reservation.Detail ?? "The Product Service returned an invalid reservation result.",
                traceId,
                StatusCodes.Status503ServiceUnavailable,
                "Inventory outcome unknown",
                cancellationToken)
        };
    }

    private async Task<CreateOrderOutcome> MarkUnknownAsync(
        Order order,
        string code,
        string detail,
        string traceId,
        int statusCode,
        string title,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        order.RecordFailure(code, detail, now);
        order.TransitionTo(OrderStatuses.InventoryUnknown, code, traceId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreateOrderOutcome.Failure(order, statusCode, title, detail, code);
    }

    private async Task<CreateOrderOutcome> RejectAsync(
        Order order,
        string code,
        string detail,
        string traceId,
        CancellationToken cancellationToken,
        int statusCode = StatusCodes.Status409Conflict,
        string title = "Order rejected")
    {
        var now = DateTimeOffset.UtcNow;
        order.RecordFailure(code, detail, now);
        order.TransitionTo(OrderStatuses.Rejected, code, traceId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreateOrderOutcome.Failure(order, statusCode, title, detail, code);
    }

    private static bool TryValidateReservation(
        ProductReservationResult reservation,
        ProductReservationRequestItem[] requestedItems,
        out decimal total,
        out string detail)
    {
        total = 0;
        detail = "The Product Service returned an invalid reservation result.";
        if (reservation.ReservationId is null
            || reservation.ReservationId == Guid.Empty)
        {
            detail = "The Product Service did not return a reservation ID.";
            return false;
        }

        if (reservation.Items.Count != requestedItems.Length)
        {
            detail = "The Product Service returned a different number of reservation items.";
            return false;
        }

        var requestedByProductId = requestedItems.ToDictionary(item => item.ProductId);
        var returnedProductIds = new HashSet<Guid>();
        foreach (var snapshot in reservation.Items)
        {
            if (!returnedProductIds.Add(snapshot.ProductId)
                || !requestedByProductId.TryGetValue(snapshot.ProductId, out var requestedItem)
                || requestedItem.Quantity != snapshot.Quantity
                || string.IsNullOrWhiteSpace(snapshot.ProductName)
                || snapshot.UnitPrice < 0)
            {
                detail = "The Product Service returned snapshots that do not match the order request.";
                return false;
            }

            var expectedSubtotal = decimal.Round(
                snapshot.UnitPrice * snapshot.Quantity,
                2,
                MidpointRounding.ToEven);
            if (snapshot.Subtotal != expectedSubtotal)
            {
                detail = "The Product Service returned an invalid item subtotal.";
                return false;
            }

            total += snapshot.Subtotal;
        }

        total = decimal.Round(total, 2, MidpointRounding.ToEven);
        if (total != reservation.TotalAmount)
        {
            detail = "The Product Service returned a total that does not match its item snapshots.";
            return false;
        }

        return true;
    }

    private static (int StatusCode, string Title, string Code, string Detail) MapReleaseFailure(
        ProductReleaseResult release)
    {
        return release.Failure switch
        {
            ProductReservationFailure.DependencyUnavailable => (
                StatusCodes.Status503ServiceUnavailable,
                "Product Service unavailable",
                "PRODUCT_SERVICE_UNAVAILABLE",
                release.Detail ?? "The Product Service is unavailable while releasing the reservation."),
            _ => (
                StatusCodes.Status503ServiceUnavailable,
                "Inventory outcome unknown",
                "INVENTORY_OUTCOME_UNKNOWN",
                release.Detail ?? "The inventory release outcome could not be determined safely.")
        };
    }
}

public sealed record CreateOrderOutcome(
    bool IsSuccess,
    Order Order,
    int StatusCode,
    string Title,
    string Detail,
    string Code)
{
    public static CreateOrderOutcome Success(Order order)
    {
        return new(true, order, StatusCodes.Status201Created, "", "", "");
    }

    public static CreateOrderOutcome Failure(
        Order order,
        int statusCode,
        string title,
        string detail,
        string code)
    {
        return new(false, order, statusCode, title, detail, code);
    }
}

public sealed record CancelOrderOutcome(
    bool IsSuccess,
    Order? Order,
    int StatusCode,
    string Title,
    string Detail,
    string Code,
    bool IdempotentReplay)
{
    public static CancelOrderOutcome Success(Order order, bool idempotentReplay)
    {
        return new(true, order, StatusCodes.Status200OK, string.Empty, string.Empty, string.Empty, idempotentReplay);
    }

    public static CancelOrderOutcome Failure(
        Order order,
        int statusCode,
        string title,
        string detail,
        string code)
    {
        return new(false, order, statusCode, title, detail, code, false);
    }

    public static CancelOrderOutcome NotFound()
    {
        return new(false, null, StatusCodes.Status404NotFound, "Order not found", string.Empty, "ORDER_NOT_FOUND", false);
    }
}
