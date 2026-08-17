using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Features.Orders;
using MicroShop.OrderService.Infrastructure.Messaging;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Tests;

public sealed class OrderOrchestrationTests(OrderDatabaseFixture fixture)
    : IClassFixture<OrderDatabaseFixture>
{
    [Fact]
    public async Task CreatePersistsPendingBeforeReservationAndConfirmsFromProductSnapshots()
    {
        var productId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var client = new StubProductInventoryClient(new ProductReservationResult(
            ProductReservationFailure.None,
            reservationId,
            [new(productId, "Authoritative Product", 125_000m, 2, 250_000m)],
            250_000m,
            null,
            null,
            null,
            true,
            false));
        await using var dbContext = fixture.CreateDbContext();
        var publisher = new RecordingOrderEventPublisher();
        var service = new OrderApplicationService(dbContext, client, publisher);

        var outcome = await service.CreateAsync(
            CreateRequest(productId, 2),
            "orchestration-success",
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(OrderStatuses.Confirmed, outcome.Order.Status);
        Assert.Equal(250_000m, outcome.Order.TotalAmount);
        Assert.Equal("Authoritative Product", outcome.Order.Items.Single().ProductName);
        Assert.NotNull(client.ReserveRequest);
        Assert.Equal(outcome.Order.Id, client.ReserveRequest!.OrderId);
        Assert.Equal(2, outcome.Order.StateHistory.Count);
        Assert.Equal("PRODUCT_RESERVATION_CONFIRMED", outcome.Order.StateHistory.Last().ReasonCode);
        var message = Assert.Single(publisher.Messages);
        Assert.Equal(outcome.Order.Id, message.OrderId);
        Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", publisher.TraceParent);
        Assert.NotEqual(Guid.Empty, message.MessageId);
        Assert.Equal(250_000m, message.TotalAmount);
        Assert.Equal("Authoritative Product", Assert.Single(message.Items).ProductName);
    }

    [Fact]
    public async Task UnavailableProductServicePersistsInventoryUnknown()
    {
        var client = new StubProductInventoryClient(new ProductReservationResult(
            ProductReservationFailure.DependencyUnavailable,
            null,
            [],
            0,
            null,
            null,
            "The Product Service is unavailable.",
            false,
            false));
        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderApplicationService(dbContext, client, new NoOpOrderEventPublisher());

        var outcome = await service.CreateAsync(
            CreateRequest(Guid.NewGuid(), 1),
            "orchestration-unavailable",
            null,
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, outcome.StatusCode);
        Assert.Equal("PRODUCT_SERVICE_UNAVAILABLE", outcome.Code);
        Assert.Equal(OrderStatuses.InventoryUnknown, outcome.Order.Status);
        Assert.Equal(2, outcome.Order.StateHistory.Count);
        Assert.Equal(OrderStatuses.InventoryUnknown, outcome.Order.StateHistory.Last().ToStatus);
    }

    [Fact]
    public async Task AmbiguousProductOutcomePersistsInventoryUnknown()
    {
        var client = new StubProductInventoryClient(new ProductReservationResult(
            ProductReservationFailure.OutcomeUnknown,
            null,
            [],
            0,
            null,
            null,
            "The reservation timed out.",
            false,
            false));
        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderApplicationService(dbContext, client, new NoOpOrderEventPublisher());

        var outcome = await service.CreateAsync(
            CreateRequest(Guid.NewGuid(), 1),
            "orchestration-timeout",
            null,
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, outcome.StatusCode);
        Assert.Equal("INVENTORY_OUTCOME_UNKNOWN", outcome.Code);
        Assert.Equal(OrderStatuses.InventoryUnknown, outcome.Order.Status);
    }

    [Fact]
    public async Task KnownProductFailurePersistsRejectedOrder()
    {
        var productId = Guid.NewGuid();
        var client = new StubProductInventoryClient(new ProductReservationResult(
            ProductReservationFailure.InsufficientStock,
            null,
            [],
            0,
            productId,
            0,
            "Product is out of stock.",
            false,
            false));
        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderApplicationService(dbContext, client, new NoOpOrderEventPublisher());

        var outcome = await service.CreateAsync(
            CreateRequest(productId, 1),
            "orchestration-rejected",
            null,
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(StatusCodes.Status409Conflict, outcome.StatusCode);
        Assert.Equal("INSUFFICIENT_STOCK", outcome.Code);
        Assert.Equal(OrderStatuses.Rejected, outcome.Order.Status);
        Assert.Equal("INSUFFICIENT_STOCK", outcome.Order.FailureCode);
    }

    [Fact]
    public async Task SnapshotMismatchDoesNotConfirmOrder()
    {
        var requestedProductId = Guid.NewGuid();
        var returnedProductId = Guid.NewGuid();
        var client = new StubProductInventoryClient(new ProductReservationResult(
            ProductReservationFailure.None,
            Guid.NewGuid(),
            [new(returnedProductId, "Wrong Product", 10m, 1, 10m)],
            10m,
            null,
            null,
            null,
            true,
            false));
        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderApplicationService(dbContext, client, new NoOpOrderEventPublisher());

        var outcome = await service.CreateAsync(
            CreateRequest(requestedProductId, 1),
            "orchestration-invalid-response",
            null,
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("INVENTORY_OUTCOME_UNKNOWN", outcome.Code);
        Assert.Equal(OrderStatuses.InventoryUnknown, outcome.Order.Status);
        Assert.Empty(outcome.Order.Items);
    }

    [Fact]
    public async Task DirectPublishFailureLeavesConfirmedOrderForOutboxHardening()
    {
        var productId = Guid.NewGuid();
        var client = new StubProductInventoryClient(new ProductReservationResult(
            ProductReservationFailure.None,
            Guid.NewGuid(),
            [new(productId, "Confirmed Before Publish Failure", 99m, 1, 99m)],
            99m,
            null,
            null,
            null,
            true,
            false));
        var publisher = new FailingOrderEventPublisher();
        await using var dbContext = fixture.CreateDbContext();
        var service = new OrderApplicationService(dbContext, client, publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            CreateRequest(productId, 1),
            "direct-publish-failure",
            null,
            CancellationToken.None));

        Assert.NotNull(publisher.OrderId);
        await using var verificationContext = fixture.CreateDbContext();
        var persisted = await verificationContext.Orders
            .AsNoTracking()
            .SingleAsync(order => order.Id == publisher.OrderId);

        Assert.Equal(OrderStatuses.Confirmed, persisted.Status);
        Assert.Equal(99m, persisted.TotalAmount);
    }

    private static CreateOrderRequest CreateRequest(Guid productId, int quantity)
    {
        return new CreateOrderRequest
        {
            CustomerName = "Orchestration Test",
            CustomerEmail = $"{Guid.NewGuid():N}@example.com",
            Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = quantity }]
        };
    }

    private sealed class StubProductInventoryClient(ProductReservationResult reservation)
        : IProductInventoryClient
    {
        public ProductReservationRequest? ReserveRequest { get; private set; }

        public Task<ProductReservationResult> ReserveAsync(
            ProductReservationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReserveRequest = request;
            return Task.FromResult(reservation);
        }

        public Task<ProductReleaseResult> ReleaseAsync(
            ProductReleaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ProductReleaseResult(
                ProductReservationFailure.None,
                Guid.NewGuid(),
                null,
                false));
        }
    }

    private sealed class NoOpOrderEventPublisher : IOrderEventPublisher
    {
        public Task PublishConfirmedAsync(
            Order order,
            string? traceParent,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOrderEventPublisher : IOrderEventPublisher
    {
        public List<MicroShop.Contracts.Orders.OrderConfirmedV1> Messages { get; } = [];

        public string? TraceParent { get; private set; }

        public Task PublishConfirmedAsync(
            Order order,
            string? traceParent,
            CancellationToken cancellationToken)
        {
            TraceParent = traceParent;
            Messages.Add(new MicroShop.Contracts.Orders.OrderConfirmedV1(
                Guid.NewGuid(),
                order.Id,
                order.CustomerName,
                order.CustomerEmail,
                order.TotalAmount,
                order.Currency,
                order.Items
                    .OrderBy(item => item.ProductId)
                    .Select(item => new MicroShop.Contracts.Orders.OrderConfirmedItemV1(
                        item.ProductId,
                        item.ProductName,
                        item.UnitPrice,
                        item.Quantity,
                        item.Subtotal))
                    .ToArray(),
                order.ConfirmedAtUtc!.Value));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingOrderEventPublisher : IOrderEventPublisher
    {
        public Guid? OrderId { get; private set; }

        public Task PublishConfirmedAsync(
            Order order,
            string? traceParent,
            CancellationToken cancellationToken)
        {
            OrderId = order.Id;
            throw new InvalidOperationException("Simulated direct publish failure.");
        }
    }
}
