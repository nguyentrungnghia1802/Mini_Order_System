using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MicroShop.ServiceDefaults;

public static class MicroShopTelemetry
{
    public const string ActivitySourceName = "MicroShop";
    public const string MeterName = "MicroShop";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> OrderOutcomes = Meter.CreateCounter<long>(
        "microshop.order.outcomes",
        unit: "{outcome}",
        description: "Order create and cancellation outcomes.");

    public static readonly Counter<long> ProductDependencyRequests = Meter.CreateCounter<long>(
        "microshop.product.dependency.requests",
        unit: "{request}",
        description: "Calls made by Order Service to Product Service.");

    public static readonly Counter<long> ProductDependencyFailures = Meter.CreateCounter<long>(
        "microshop.product.dependency.failures",
        unit: "{failure}",
        description: "Product Service dependency failures observed by Order Service.");

    public static readonly Counter<long> ReservationResults = Meter.CreateCounter<long>(
        "microshop.inventory.reservation.results",
        unit: "{result}",
        description: "Inventory reservation and release results.");

    public static readonly Counter<long> NotificationConsumeResults = Meter.CreateCounter<long>(
        "microshop.notification.consume.results",
        unit: "{result}",
        description: "Notification consumer processing results.");

    public static readonly Counter<long> OutboxFailures = Meter.CreateCounter<long>(
        "microshop.order.outbox.failures",
        unit: "{failure}",
        description: "Order outbox publish failures.");

    public static readonly ObservableGauge<long> OutboxPending = Meter.CreateObservableGauge(
        "microshop.order.outbox.pending",
        static () => Volatile.Read(ref pendingOutboxMessages),
        unit: "{message}",
        description: "Current number of pending order outbox messages.");

    public static readonly ObservableGauge<long> OutboxDeadLettered = Meter.CreateObservableGauge(
        "microshop.order.outbox.dead_lettered",
        static () => Volatile.Read(ref deadLetteredOutboxMessages),
        unit: "{message}",
        description: "Current number of dead-lettered order outbox messages.");

    private static long pendingOutboxMessages;
    private static long deadLetteredOutboxMessages;

    public static void SetOutboxBacklog(int pendingCount, int deadLetteredCount)
    {
        Volatile.Write(ref pendingOutboxMessages, Math.Max(0, pendingCount));
        Volatile.Write(ref deadLetteredOutboxMessages, Math.Max(0, deadLetteredCount));
    }

    public static TagList Tags(
        string operation,
        string result)
    {
        return new TagList
        {
            { "operation", operation },
            { "result", result }
        };
    }
}
