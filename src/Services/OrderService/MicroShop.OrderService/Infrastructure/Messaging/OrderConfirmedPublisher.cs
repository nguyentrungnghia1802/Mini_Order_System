using MassTransit;
using MicroShop.Contracts.Orders;
using MicroShop.OrderService.Persistence.Entities;

namespace MicroShop.OrderService.Infrastructure.Messaging;

public interface IOrderEventPublisher
{
    Task PublishConfirmedAsync(
        Order order,
        string? traceParent,
        CancellationToken cancellationToken);
}

public sealed class MassTransitOrderEventPublisher(IPublishEndpoint publishEndpoint)
    : IOrderEventPublisher
{
    public Task PublishConfirmedAsync(
        Order order,
        string? traceParent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        var message = new OrderConfirmedV1(
            Guid.NewGuid(),
            order.Id,
            order.CustomerName,
            order.CustomerEmail,
            order.TotalAmount,
            order.Currency,
            order.Items
                .OrderBy(item => item.ProductId)
                .Select(item => new OrderConfirmedItemV1(
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity,
                    item.Subtotal))
                .ToArray(),
            order.ConfirmedAtUtc ?? DateTimeOffset.UtcNow);

        return publishEndpoint.Publish(
            message,
            publishContext =>
            {
                publishContext.MessageId = message.MessageId;
                publishContext.CorrelationId = message.OrderId;
                if (!string.IsNullOrWhiteSpace(traceParent))
                {
                    publishContext.Headers.Set("traceparent", traceParent);
                }
            },
            cancellationToken);
    }
}
