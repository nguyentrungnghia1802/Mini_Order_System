using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Persistence.Entities;

namespace MicroShop.OrderService.Tests;

public sealed class OrderDomainTests
{
    [Fact]
    public void CreateNormalizesCustomerEmailAndRecordsPendingState()
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(
            Guid.NewGuid(),
            " Nguyen Van A ",
            " Customer@Example.COM ",
            now);

        Assert.Equal("Nguyen Van A", order.CustomerName);
        Assert.Equal("customer@example.com", order.CustomerEmail);
        Assert.Equal(OrderStatuses.PendingInventory, order.Status);
        Assert.Equal(1, order.Version);
        var history = Assert.Single(order.StateHistory);
        Assert.Null(history.FromStatus);
        Assert.Equal(OrderStatuses.PendingInventory, history.ToStatus);
    }

    [Fact]
    public void AddItemsCalculatesTotalFromProductSnapshots()
    {
        var order = CreateOrder();
        order.AddItem(OrderItem.Create(Guid.NewGuid(), "Keyboard", 1_200_000.125m, 2));
        order.AddItem(OrderItem.Create(Guid.NewGuid(), "Mouse", 450_000m, 1));

        Assert.Equal(2_850_000.24m, order.TotalAmount);
        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void DuplicateProductIdsAreRejected()
    {
        var order = CreateOrder();
        var productId = Guid.NewGuid();
        order.AddItem(OrderItem.Create(productId, "Keyboard", 1_200_000m, 1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            order.AddItem(OrderItem.Create(productId, "Keyboard", 1_200_000m, 1)));

        Assert.Contains("duplicate Product IDs", exception.Message, StringComparison.Ordinal);
        Assert.Single(order.Items);
    }

    [Fact]
    public void TransitionToConfirmedIncrementsVersionAndRecordsHistory()
    {
        var order = CreateOrder();
        var now = DateTimeOffset.UtcNow.AddMinutes(1);

        order.TransitionTo(OrderStatuses.Confirmed, "INVENTORY_RESERVED", "trace-1", now);

        Assert.Equal(OrderStatuses.Confirmed, order.Status);
        Assert.Equal(2, order.Version);
        Assert.Equal(now, order.ConfirmedAtUtc);
        Assert.Equal(2, order.StateHistory.Count);
        var history = order.StateHistory.Last();
        Assert.Equal(OrderStatuses.PendingInventory, history.FromStatus);
        Assert.Equal(OrderStatuses.Confirmed, history.ToStatus);
        Assert.Equal("INVENTORY_RESERVED", history.ReasonCode);
        Assert.Equal("trace-1", history.TraceId);
    }

    [Fact]
    public void InvalidTransitionIsRejected()
    {
        var order = CreateOrder();
        order.TransitionTo(OrderStatuses.Rejected, "PRODUCT_NOT_FOUND", null, DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            order.TransitionTo(OrderStatuses.Confirmed, null, null, DateTimeOffset.UtcNow));

        Assert.Contains(OrderStatuses.Rejected, exception.Message, StringComparison.Ordinal);
        Assert.Equal(OrderStatuses.Rejected, order.Status);
    }

    [Fact]
    public void RepeatedCancelledTransitionIsIdempotent()
    {
        var order = CreateOrder();
        order.TransitionTo(OrderStatuses.Confirmed, null, null, DateTimeOffset.UtcNow);
        order.TransitionTo(OrderStatuses.Cancelled, "CANCELLED", null, DateTimeOffset.UtcNow);
        var version = order.Version;
        var historyCount = order.StateHistory.Count;

        order.TransitionTo(OrderStatuses.Cancelled, "REPEATED", null, DateTimeOffset.UtcNow);

        Assert.Equal(version, order.Version);
        Assert.Equal(historyCount, order.StateHistory.Count);
        Assert.Equal(OrderStatuses.Cancelled, order.Status);
    }

    private static Order CreateOrder()
    {
        return Order.Create(
            Guid.NewGuid(),
            "Nguyen Van A",
            "a@example.com",
            DateTimeOffset.UtcNow);
    }
}
