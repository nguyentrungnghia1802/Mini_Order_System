using MicroShop.OrderService.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MicroShop.OrderService.Infrastructure.Messaging;

public sealed record OutboxBacklogSummary(
    int PendingCount,
    DateTimeOffset? OldestPendingAtUtc,
    int DeadLetteredCount)
{
    public TimeSpan? OldestPendingAge(DateTimeOffset now)
    {
        return OldestPendingAtUtc is null
            ? null
            : now - OldestPendingAtUtc.Value;
    }
}

public static class OutboxBacklogQuery
{
    public static async Task<OutboxBacklogSummary> ReadAsync(
        OrderDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var pending = dbContext.OutboxMessages
            .Where(message => message.PublishedAtUtc == null && message.DeadLetteredAtUtc == null);
        var pendingCount = await pending.CountAsync(cancellationToken);
        var oldestPendingAtUtc = await pending
            .Select(message => (DateTimeOffset?)message.CreatedAtUtc)
            .MinAsync(cancellationToken);
        var deadLetteredCount = await dbContext.OutboxMessages
            .CountAsync(message => message.DeadLetteredAtUtc != null, cancellationToken);

        return new OutboxBacklogSummary(
            pendingCount,
            oldestPendingAtUtc,
            deadLetteredCount);
    }
}

public sealed class OrderOutboxHealthCheck(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options) : IHealthCheck
{
    private readonly OutboxOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var summary = await OutboxBacklogQuery.ReadAsync(dbContext, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var oldestAge = summary.OldestPendingAge(now);
            var data = new Dictionary<string, object>
            {
                ["pendingCount"] = summary.PendingCount,
                ["oldestPendingAtUtc"] = summary.OldestPendingAtUtc?.ToString("O") ?? string.Empty,
                ["oldestPendingAgeSeconds"] = oldestAge?.TotalSeconds ?? 0,
                ["deadLetteredCount"] = summary.DeadLetteredCount
            };

            var backlogExceeded = summary.PendingCount > _options.MaxPendingMessages
                || oldestAge > _options.MaxPendingAge;
            var deadLettered = summary.DeadLetteredCount > 0;
            if ((_options.FailReadinessOnDeadLettered && deadLettered) || backlogExceeded)
            {
                return HealthCheckResult.Unhealthy(
                    "Order outbox is outside its configured readiness policy.",
                    data: data);
            }

            if (deadLettered)
            {
                return HealthCheckResult.Degraded(
                    "Order outbox contains dead-lettered messages.",
                    data: data);
            }

            return HealthCheckResult.Healthy("Order outbox is within its readiness policy.", data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Order outbox status could not be read.",
                exception);
        }
    }
}
