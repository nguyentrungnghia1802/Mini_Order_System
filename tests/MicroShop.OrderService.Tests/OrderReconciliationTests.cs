using System.Net;
using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Features.Orders;
using MicroShop.OrderService.Features.Reconciliation;
using MicroShop.OrderService.Infrastructure.Messaging;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Tests;

public sealed class OrderReconciliationTests(OrderDatabaseFixture fixture)
    : IClassFixture<OrderDatabaseFixture>
{
    [Fact]
    public async Task MatchingReservedInventoryConfirmsUnknownOrderAndWritesAuditAndOutbox()
    {
        var productId = Guid.NewGuid();
        var orderId = await SeedInventoryUnknownAsync(productId, 2);
        var reservationId = Guid.NewGuid();
        var client = new ReconciliationProductClient(
            LookupResult: ReservedLookup(orderId, reservationId, productId, 2, 250_000m),
            ReleaseResult: null);

        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderReconciliationService(
            dbContext,
            client,
            new OrderOutboxWriter(dbContext));

        var outcome = await service.ReconcileAsync(
            orderId,
            "reconcile-confirm",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(ReconciliationOutcomes.Confirmed, outcome.Outcome);
        Assert.Equal(OrderStatuses.Confirmed, outcome.Order!.Status);
        Assert.Single(outcome.Order.Items);
        Assert.Equal(250_000m, outcome.Order.TotalAmount);
        Assert.Equal(orderId, client.LookupRequest!.OrderId);
        Assert.Null(client.ReleaseRequest);

        await using var verification = fixture.CreateDbContext();
        var persisted = await verification.Orders
            .Include(order => order.Items)
            .Include(order => order.ReconciliationAudits)
            .Include(order => order.StateHistory)
            .SingleAsync(order => order.Id == orderId);
        Assert.Equal(OrderStatuses.Confirmed, persisted.Status);
        Assert.Single(persisted.Items);
        var audit = Assert.Single(persisted.ReconciliationAudits);
        Assert.Equal(ReconciliationOperations.Inventory, audit.Operation);
        Assert.Equal(OrderStatuses.InventoryUnknown, audit.FromStatus);
        Assert.Equal(OrderStatuses.Confirmed, audit.ToStatus);
        Assert.Equal(ReconciliationOutcomes.Confirmed, audit.Outcome);
        Assert.Equal(reservationId, audit.ReservationId);
        Assert.Single(await verification.OutboxMessages.Where(message => message.AggregateId == orderId).ToListAsync());
    }

    [Fact]
    public async Task MissingReservationRejectsUnknownOrderAndRecordsSafeOutcome()
    {
        var productId = Guid.NewGuid();
        var orderId = await SeedInventoryUnknownAsync(productId, 1);
        var client = new ReconciliationProductClient(
            LookupResult: new ProductReservationLookupResult(
                ProductReservationFailure.ReservationNotFound,
                null,
                null,
                null,
                [],
                0,
                null,
                null,
                "No reservation exists."),
            ReleaseResult: null);

        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderReconciliationService(
            dbContext,
            client,
            new OrderOutboxWriter(dbContext));

        var outcome = await service.ReconcileAsync(
            orderId,
            "reconcile-missing",
            null,
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(ReconciliationOutcomes.Rejected, outcome.Outcome);
        Assert.Equal(OrderStatuses.Rejected, outcome.Order!.Status);
        Assert.Equal("INVENTORY_RESERVATION_NOT_FOUND", outcome.Order.FailureCode);
        Assert.Empty(outcome.Order.Items);

        await using var verification = fixture.CreateDbContext();
        var persisted = await verification.Orders
            .Include(order => order.ReconciliationAudits)
            .SingleAsync(order => order.Id == orderId);
        var audit = Assert.Single(persisted.ReconciliationAudits);
        Assert.Equal(OrderStatuses.InventoryUnknown, audit.FromStatus);
        Assert.Equal(OrderStatuses.Rejected, audit.ToStatus);
        Assert.Equal(ReconciliationOutcomes.Rejected, audit.Outcome);
        Assert.Empty(await verification.OutboxMessages.Where(message => message.AggregateId == orderId).ToListAsync());
    }

    [Fact]
    public async Task MissingCancellationReservationCancelsPendingOrderAndClearsFailure()
    {
        var productId = Guid.NewGuid();
        var orderId = await SeedCancellationPendingAsync(productId);
        var client = new ReconciliationProductClient(
            LookupResult: new ProductReservationLookupResult(
                ProductReservationFailure.ReservationNotFound,
                null,
                null,
                null,
                [],
                0,
                null,
                null,
                "No reservation exists."),
            ReleaseResult: null);

        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderReconciliationService(
            dbContext,
            client,
            new OrderOutboxWriter(dbContext));

        var outcome = await service.ReconcileAsync(
            orderId,
            "reconcile-cancel-missing",
            null,
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(ReconciliationOutcomes.Cancelled, outcome.Outcome);
        Assert.Equal(OrderStatuses.Cancelled, outcome.Order!.Status);
        Assert.Null(outcome.Order.FailureCode);
        Assert.Null(client.ReleaseRequest);

        await using var verification = fixture.CreateDbContext();
        var persisted = await verification.Orders
            .Include(order => order.ReconciliationAudits)
            .SingleAsync(order => order.Id == orderId);
        var audit = Assert.Single(persisted.ReconciliationAudits);
        Assert.Equal(ReconciliationOperations.Cancellation, audit.Operation);
        Assert.Equal(OrderStatuses.CancellationPending, audit.FromStatus);
        Assert.Equal(OrderStatuses.Cancelled, audit.ToStatus);
        Assert.Equal(ReconciliationOutcomes.Cancelled, audit.Outcome);
    }

    [Fact]
    public async Task ReservedCancellationReleasesReservationBeforeCompletingCancellation()
    {
        var productId = Guid.NewGuid();
        var orderId = await SeedCancellationPendingAsync(productId);
        var reservationId = Guid.NewGuid();
        var client = new ReconciliationProductClient(
            LookupResult: ReservedLookup(orderId, reservationId, productId, 1, 125_000m),
            ReleaseResult: new ProductReleaseResult(
                ProductReservationFailure.None,
                reservationId,
                null,
                false));

        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderReconciliationService(
            dbContext,
            client,
            new OrderOutboxWriter(dbContext));

        var outcome = await service.ReconcileAsync(
            orderId,
            "reconcile-cancel-release",
            null,
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(OrderStatuses.Cancelled, outcome.Order!.Status);
        Assert.Equal(orderId, client.ReleaseRequest!.OrderId);
        Assert.Equal(ReconciliationOutcomes.Cancelled, outcome.Outcome);
    }

    [Fact]
    public async Task DependencyUnavailableKeepsInventoryUnknownAndAuditsPendingState()
    {
        var productId = Guid.NewGuid();
        var orderId = await SeedInventoryUnknownAsync(productId, 1);
        var client = new ReconciliationProductClient(
            LookupResult: new ProductReservationLookupResult(
                ProductReservationFailure.DependencyUnavailable,
                null,
                null,
                null,
                [],
                0,
                null,
                null,
                "Product Service is unavailable."),
            ReleaseResult: null);

        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderReconciliationService(
            dbContext,
            client,
            new OrderOutboxWriter(dbContext));

        var outcome = await service.ReconcileAsync(
            orderId,
            "reconcile-unavailable",
            null,
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (HttpStatusCode)outcome.StatusCode);
        Assert.Equal(ReconciliationOutcomes.DependencyUnavailable, outcome.Outcome);
        Assert.Equal(OrderStatuses.InventoryUnknown, outcome.Order!.Status);

        await using var verification = fixture.CreateDbContext();
        var audit = await verification.ReconciliationAudits.SingleAsync(audit => audit.OrderId == orderId);
        Assert.Equal(OrderStatuses.InventoryUnknown, audit.FromStatus);
        Assert.Equal(OrderStatuses.InventoryUnknown, audit.ToStatus);
        Assert.Equal(ReconciliationOutcomes.DependencyUnavailable, audit.Outcome);
    }

    [Fact]
    public async Task MismatchedReservationStaysPendingAndRecordsConflictAudit()
    {
        var requestedProductId = Guid.NewGuid();
        var returnedProductId = Guid.NewGuid();
        var orderId = await SeedInventoryUnknownAsync(requestedProductId, 1);
        var client = new ReconciliationProductClient(
            LookupResult: ReservedLookup(orderId, Guid.NewGuid(), returnedProductId, 1, 10m),
            ReleaseResult: null);

        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderReconciliationService(
            dbContext,
            client,
            new OrderOutboxWriter(dbContext));

        var outcome = await service.ReconcileAsync(
            orderId,
            "reconcile-conflict",
            null,
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(HttpStatusCode.Conflict, (HttpStatusCode)outcome.StatusCode);
        Assert.Equal(ReconciliationOutcomes.Conflict, outcome.Outcome);
        Assert.Equal(OrderStatuses.InventoryUnknown, outcome.Order!.Status);

        await using var verification = fixture.CreateDbContext();
        var audit = await verification.ReconciliationAudits.SingleAsync(audit => audit.OrderId == orderId);
        Assert.Equal(ReconciliationOutcomes.Conflict, audit.Outcome);
        Assert.Equal(OrderStatuses.InventoryUnknown, audit.FromStatus);
        Assert.Equal(OrderStatuses.InventoryUnknown, audit.ToStatus);
    }

    [Fact]
    public async Task MatchingReservationOverOrderLimitIsReleasedAndRejected()
    {
        var productId = Guid.NewGuid();
        var orderId = await SeedInventoryUnknownAsync(productId, 1);
        var reservationId = Guid.NewGuid();
        var total = OrderValidator.MaxOrderTotal + 1m;
        var client = new ReconciliationProductClient(
            LookupResult: ReservedLookup(orderId, reservationId, productId, 1, total),
            ReleaseResult: new ProductReleaseResult(
                ProductReservationFailure.None,
                reservationId,
                null,
                false));

        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderReconciliationService(
            dbContext,
            client,
            new OrderOutboxWriter(dbContext));

        var outcome = await service.ReconcileAsync(
            orderId,
            "reconcile-over-limit",
            null,
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(ReconciliationOutcomes.Rejected, outcome.Outcome);
        Assert.Equal(OrderStatuses.Rejected, outcome.Order!.Status);
        Assert.Equal("ORDER_TOTAL_LIMIT_EXCEEDED", outcome.Order.FailureCode);
        Assert.Equal(orderId, client.ReleaseRequest!.OrderId);

        await using var verification = fixture.CreateDbContext();
        var audit = await verification.ReconciliationAudits.SingleAsync(audit => audit.OrderId == orderId);
        Assert.Equal(ReconciliationOutcomes.Rejected, audit.Outcome);
        Assert.Equal("reserved", audit.ReservationStatus);
    }

    private async Task<Guid> SeedInventoryUnknownAsync(Guid productId, int quantity)
    {
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = fixture.CreateDbContext();
        var order = Order.Create(
            orderId,
            "Reconciliation User",
            $"{orderId:N}@example.com",
            now);
        order.AddInventoryRequestItem(OrderInventoryRequestItem.Create(productId, quantity));
        order.RecordFailure("INVENTORY_OUTCOME_UNKNOWN", "Reservation outcome is unknown.", now);
        order.TransitionTo(
            OrderStatuses.InventoryUnknown,
            "INVENTORY_OUTCOME_UNKNOWN",
            "seed",
            now);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return orderId;
    }

    private async Task<Guid> SeedCancellationPendingAsync(Guid productId)
    {
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = fixture.CreateDbContext();
        var order = Order.Create(
            orderId,
            "Cancellation User",
            $"{orderId:N}@example.com",
            now);
        order.AddInventoryRequestItem(OrderInventoryRequestItem.Create(productId, 1));
        order.AddItem(OrderItem.Create(productId, "Seed Product", 125_000m, 1));
        order.TransitionTo(OrderStatuses.Confirmed, "seed-confirmed", "seed", now);
        order.RecordFailure("PRODUCT_SERVICE_UNAVAILABLE", "Release outcome is unknown.", now);
        order.TransitionTo(
            OrderStatuses.CancellationPending,
            "CANCELLATION_STARTED",
            "seed",
            now);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();
        return orderId;
    }

    private static ProductReservationLookupResult ReservedLookup(
        Guid orderId,
        Guid reservationId,
        Guid productId,
        int quantity,
        decimal total)
    {
        return new ProductReservationLookupResult(
            ProductReservationFailure.None,
            reservationId,
            "reserved",
            "VND",
            [new(productId, "Authoritative Product", total / quantity, quantity, total)],
            total,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            null,
            null);
    }

    private sealed class ReconciliationProductClient(
        ProductReservationLookupResult LookupResult,
        ProductReleaseResult? ReleaseResult) : IProductInventoryClient
    {
        public ProductReservationLookupRequest? LookupRequest { get; private set; }

        public ProductReleaseRequest? ReleaseRequest { get; private set; }

        public Task<ProductReservationResult> ReserveAsync(
            ProductReservationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("Reconciliation tests do not reserve inventory.");
        }

        public Task<ProductReleaseResult> ReleaseAsync(
            ProductReleaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseRequest = request;
            return Task.FromResult(ReleaseResult ?? new ProductReleaseResult(
                ProductReservationFailure.DependencyUnavailable,
                null,
                "No release result was configured.",
                false));
        }

        public Task<ProductReservationLookupResult> GetReservationByOrderAsync(
            ProductReservationLookupRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LookupRequest = request;
            return Task.FromResult(LookupResult);
        }
    }
}
