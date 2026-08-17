using MassTransit;
using MicroShop.Contracts.Orders;

namespace MicroShop.OrderService.Infrastructure.Messaging;

public interface IOrderConfirmedMessageTransport
{
    Task PublishAsync(
        OrderConfirmedV1 message,
        string? traceParent,
        CancellationToken cancellationToken);
}

public sealed class MassTransitOrderConfirmedMessageTransport(IPublishEndpoint publishEndpoint)
    : IOrderConfirmedMessageTransport
{
    public Task PublishAsync(
        OrderConfirmedV1 message,
        string? traceParent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

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
