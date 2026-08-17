using MicroShop.OrderService.Persistence;
using MicroShop.OrderService.Persistence.Entities;

namespace MicroShop.OrderService.Infrastructure.Messaging;

public interface IOrderOutboxWriter
{
    void AddConfirmed(Order order, string? traceParent);
}

public sealed class OrderOutboxWriter(OrderDbContext dbContext) : IOrderOutboxWriter
{
    public void AddConfirmed(Order order, string? traceParent)
    {
        ArgumentNullException.ThrowIfNull(order);

        var message = OrderConfirmedMessageFactory.Create(order);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            message.MessageId,
            OrderConfirmedMessageFactory.MessageType,
            order.Id,
            OrderConfirmedMessageFactory.Serialize(message),
            traceParent,
            message.OccurredAtUtc));
    }
}
