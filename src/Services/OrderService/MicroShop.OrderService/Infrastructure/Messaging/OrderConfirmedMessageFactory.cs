using System.Text.Json;
using MicroShop.Contracts.Orders;
using MicroShop.OrderService.Persistence.Entities;

namespace MicroShop.OrderService.Infrastructure.Messaging;

public static class OrderConfirmedMessageFactory
{
    public const string MessageType = "MicroShop.Contracts.Orders.OrderConfirmedV1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static OrderConfirmedV1 Create(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new OrderConfirmedV1(
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
            order.ConfirmedAtUtc
                ?? throw new InvalidOperationException("A confirmed order must have a confirmation timestamp."));
    }

    public static string Serialize(OrderConfirmedV1 message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.Serialize(message, JsonOptions);
    }

    public static OrderConfirmedV1 Deserialize(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var message = JsonSerializer.Deserialize<OrderConfirmedV1>(payload, JsonOptions);
        if (message is null
            || message.MessageId == Guid.Empty
            || message.OrderId == Guid.Empty
            || message.Items is null)
        {
            throw new JsonException("The OrderConfirmedV1 outbox payload is invalid.");
        }

        return message;
    }
}
