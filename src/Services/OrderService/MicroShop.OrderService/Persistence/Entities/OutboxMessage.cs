namespace MicroShop.OrderService.Persistence.Entities;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string MessageType { get; private set; } = string.Empty;

    public Guid AggregateId { get; private set; }

    public string Payload { get; private set; } = string.Empty;

    public string? TraceParent { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public DateTimeOffset? DeadLetteredAtUtc { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        string messageType,
        Guid aggregateId,
        string payload,
        string? traceParent,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new OutboxMessage
        {
            Id = id,
            MessageType = messageType,
            AggregateId = aggregateId,
            Payload = payload,
            TraceParent = traceParent,
            CreatedAtUtc = createdAtUtc,
            NextAttemptAtUtc = createdAtUtc
        };
    }

    public void Claim(string workerId, DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (PublishedAtUtc is not null || DeadLetteredAtUtc is not null)
        {
            throw new InvalidOperationException("Only pending outbox messages can be claimed.");
        }

        AttemptCount++;
        LockedBy = workerId;
        LockedUntilUtc = now.Add(leaseDuration);
    }

    public bool TryMarkPublished(string workerId, DateTimeOffset now)
    {
        if (!HasLease(workerId))
        {
            return false;
        }

        PublishedAtUtc = now;
        LastError = null;
        LockedBy = null;
        LockedUntilUtc = null;
        return true;
    }

    public bool TryMarkFailed(
        string workerId,
        DateTimeOffset now,
        TimeSpan nextAttemptDelay,
        bool deadLetter)
    {
        if (!HasLease(workerId))
        {
            return false;
        }

        LockedBy = null;
        LockedUntilUtc = null;
        NextAttemptAtUtc = now.Add(nextAttemptDelay);
        if (deadLetter)
        {
            DeadLetteredAtUtc = now;
            NextAttemptAtUtc = now.AddYears(100);
        }

        return true;
    }

    public bool TryDeadLetter(
        string? workerId,
        DateTimeOffset now,
        string reason)
    {
        if (PublishedAtUtc is not null
            || DeadLetteredAtUtc is not null
            || (workerId is not null && !HasLease(workerId)))
        {
            return false;
        }

        DeadLetteredAtUtc = now;
        LockedBy = null;
        LockedUntilUtc = null;
        NextAttemptAtUtc = now.AddYears(100);
        SetLastError(reason);
        return true;
    }

    public void SetLastError(string? error)
    {
        LastError = string.IsNullOrWhiteSpace(error)
            ? null
            : error[..Math.Min(error.Length, 4_000)];
    }

    private bool HasLease(string workerId)
    {
        return PublishedAtUtc is null
            && DeadLetteredAtUtc is null
            && string.Equals(LockedBy, workerId, StringComparison.Ordinal);
    }
}
