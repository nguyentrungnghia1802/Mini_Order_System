namespace MicroShop.OrderService.Persistence.Entities;

public sealed class OrderStateHistory
{
    private OrderStateHistory()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Order? Order { get; private set; }

    public string? FromStatus { get; private set; }

    public string ToStatus { get; private set; } = string.Empty;

    public string? ReasonCode { get; private set; }

    public string? TraceId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    internal static OrderStateHistory Create(
        Guid orderId,
        string? fromStatus,
        string toStatus,
        string? reasonCode,
        string? traceId,
        DateTimeOffset occurredAtUtc)
    {
        return new OrderStateHistory
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim(),
            TraceId = string.IsNullOrWhiteSpace(traceId) ? null : traceId.Trim(),
            OccurredAtUtc = occurredAtUtc
        };
    }
}
