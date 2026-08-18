namespace MicroShop.OrderService.Persistence.Entities;

public sealed class OrderReconciliationAudit
{
    private OrderReconciliationAudit()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Order? Order { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public string FromStatus { get; private set; } = string.Empty;

    public string ToStatus { get; private set; } = string.Empty;

    public string? ReservationStatus { get; private set; }

    public Guid? ReservationId { get; private set; }

    public string Outcome { get; private set; } = string.Empty;

    public string? Detail { get; private set; }

    public string? TraceId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static OrderReconciliationAudit Create(
        Guid orderId,
        string operation,
        string fromStatus,
        string toStatus,
        string? reservationStatus,
        Guid? reservationId,
        string outcome,
        string? detail,
        string? traceId,
        DateTimeOffset occurredAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID is required.", nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new ArgumentException("Reconciliation operation is required.", nameof(operation));
        }

        if (string.IsNullOrWhiteSpace(fromStatus))
        {
            throw new ArgumentException("Previous Order status is required.", nameof(fromStatus));
        }

        if (string.IsNullOrWhiteSpace(toStatus))
        {
            throw new ArgumentException("Current Order status is required.", nameof(toStatus));
        }

        if (string.IsNullOrWhiteSpace(outcome))
        {
            throw new ArgumentException("Reconciliation outcome is required.", nameof(outcome));
        }

        return new OrderReconciliationAudit
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Operation = operation.Trim(),
            FromStatus = fromStatus.Trim(),
            ToStatus = toStatus.Trim(),
            ReservationStatus = string.IsNullOrWhiteSpace(reservationStatus)
                ? null
                : reservationStatus.Trim(),
            ReservationId = reservationId,
            Outcome = outcome.Trim(),
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim(),
            TraceId = string.IsNullOrWhiteSpace(traceId) ? null : traceId.Trim(),
            OccurredAtUtc = occurredAtUtc
        };
    }
}
