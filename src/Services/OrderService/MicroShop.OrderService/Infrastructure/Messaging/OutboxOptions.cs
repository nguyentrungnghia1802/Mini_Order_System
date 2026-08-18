namespace MicroShop.OrderService.Infrastructure.Messaging;

public sealed class OutboxOptions
{
    public bool Enabled { get; set; } = true;

    public int MaxAttempts { get; set; } = 10;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    public int MaxPendingMessages { get; set; } = 1_000;

    public TimeSpan MaxPendingAge { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan BacklogLogInterval { get; set; } = TimeSpan.FromSeconds(30);

    public bool FailReadinessOnDeadLettered { get; set; }
}
