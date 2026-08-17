namespace MicroShop.NotificationService.Persistence.Entities;

public sealed class ConsumedMessage
{
    private ConsumedMessage()
    {
    }

    public Guid MessageId { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public string Consumer { get; private set; } = string.Empty;

    public DateTimeOffset ConsumedAtUtc { get; private set; }

    public string? TraceId { get; private set; }

    public Notification? Notification { get; private set; }

    public static ConsumedMessage Create(
        Guid messageId,
        string messageType,
        string consumer,
        DateTimeOffset consumedAtUtc,
        string? traceId)
    {
        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("Message ID is required.", nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(messageType))
        {
            throw new ArgumentException("Message type is required.", nameof(messageType));
        }

        if (string.IsNullOrWhiteSpace(consumer))
        {
            throw new ArgumentException("Consumer is required.", nameof(consumer));
        }

        return new ConsumedMessage
        {
            MessageId = messageId,
            MessageType = messageType.Trim(),
            Consumer = consumer.Trim(),
            ConsumedAtUtc = consumedAtUtc,
            TraceId = string.IsNullOrWhiteSpace(traceId) ? null : traceId.Trim()
        };
    }

    public void AttachNotification(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Notification = notification;
    }
}
