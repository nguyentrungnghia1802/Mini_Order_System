using System.Globalization;
using MicroShop.Contracts.Orders;

namespace MicroShop.NotificationService.Persistence.Entities;

public sealed class Notification
{
    private Notification()
    {
    }

    public Guid Id { get; private set; }

    public Guid SourceMessageId { get; private set; }

    public ConsumedMessage? ConsumedMessage { get; private set; }

    public Guid OrderId { get; private set; }

    public string CustomerEmail { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public decimal TotalAmount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public static Notification CreateFrom(
        Guid sourceMessageId,
        OrderConfirmedV1 message,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (sourceMessageId == Guid.Empty)
        {
            throw new ArgumentException("Source message ID is required.", nameof(sourceMessageId));
        }

        if (message.OrderId == Guid.Empty)
        {
            throw new InvalidOperationException("A confirmed order event requires an order ID.");
        }

        if (string.IsNullOrWhiteSpace(message.CustomerEmail))
        {
            throw new InvalidOperationException("A confirmed order event requires a customer email.");
        }

        if (message.TotalAmount < 0)
        {
            throw new InvalidOperationException("A confirmed order event cannot have a negative total.");
        }

        if (message.Items is null || message.Items.Count == 0)
        {
            throw new InvalidOperationException("A confirmed order event requires item snapshots.");
        }

        var currency = message.Currency.Trim().ToUpperInvariant();
        if (currency.Length != 3)
        {
            throw new InvalidOperationException("A confirmed order event requires a three-letter currency.");
        }

        var normalizedTotal = decimal.Round(message.TotalAmount, 2, MidpointRounding.ToEven);
        return new Notification
        {
            Id = Guid.NewGuid(),
            SourceMessageId = sourceMessageId,
            OrderId = message.OrderId,
            CustomerEmail = message.CustomerEmail.Trim().ToLowerInvariant(),
            Subject = "Order confirmed",
            Body = string.Format(
                CultureInfo.InvariantCulture,
                "Your order {0} was confirmed for {1:N2} {2}.",
                message.OrderId,
                normalizedTotal,
                currency),
            TotalAmount = normalizedTotal,
            Currency = currency,
            IsRead = false,
            CreatedAtUtc = createdAtUtc
        };
    }

    public void MarkAsRead(DateTimeOffset readAtUtc)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAtUtc = readAtUtc;
    }
}
