namespace MicroShop.NotificationService.Infrastructure.Messaging;

public sealed class NotificationMessagingOptions
{
    public int RetryCount { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(250);
}
