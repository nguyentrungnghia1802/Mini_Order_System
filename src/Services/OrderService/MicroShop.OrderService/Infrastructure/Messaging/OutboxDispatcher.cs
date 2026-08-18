using System.Diagnostics;
using MicroShop.OrderService.Persistence;
using MicroShop.OrderService.Persistence.Entities;
using MicroShop.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MicroShop.OrderService.Infrastructure.Messaging;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly MicroShopServiceIdentity? _identity;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    private DateTimeOffset _lastBacklogLogAtUtc = DateTimeOffset.MinValue;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger,
        MicroShopServiceIdentity? identity = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _identity = identity;
    }

    public async Task<bool> DispatchOnceAsync(CancellationToken cancellationToken = default)
    {
        var claimResult = await ClaimNextAsync(cancellationToken);
        if (!claimResult.Handled)
        {
            return false;
        }

        if (claimResult.Claim is null)
        {
            return true;
        }

        var claim = claimResult.Claim;
        using var activity = StartPublisherActivity(claim);
        using var logScope = MicroShopLogging.BeginScope(
            _logger,
            _identity,
            activity,
            orderId: claim.AggregateId,
            messageId: claim.MessageId);
        try
        {
            var message = OrderConfirmedMessageFactory.Deserialize(claim.Payload);
            if (message.MessageId != claim.MessageId || message.OrderId != claim.AggregateId)
            {
                throw new InvalidDataException("The outbox payload identity does not match its stored envelope.");
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var transport = scope.ServiceProvider
                .GetRequiredService<IOrderConfirmedMessageTransport>();
            await transport.PublishAsync(
                message,
                activity?.Id ?? claim.TraceParent,
                cancellationToken);
            await MarkPublishedAsync(claim, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            MicroShopTelemetry.OutboxFailures.Add(
                1,
                MicroShopTelemetry.Tags("publish", "exception"));
            OutboxLog.PublishFailed(
                _logger,
                exception,
                claim.MessageId,
                claim.AggregateId,
                claim.AttemptCount);
            await MarkFailedAsync(claim, exception, cancellationToken);
        }

        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var handled = await DispatchOnceAsync(stoppingToken);
                await LogBacklogIfDueAsync(stoppingToken);
                if (!handled)
                {
                    await Task.Delay(_options.PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                using var logScope = MicroShopLogging.BeginScope(
                    _logger,
                    _identity,
                    Activity.Current);
                OutboxLog.IterationFailed(_logger, exception);
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
        }
    }

    private async Task LogBacklogIfDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastBacklogLogAtUtc < _options.BacklogLogInterval)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var summary = await OutboxBacklogQuery.ReadAsync(dbContext, cancellationToken);
        _lastBacklogLogAtUtc = now;
        MicroShopTelemetry.SetOutboxBacklog(
            summary.PendingCount,
            summary.DeadLetteredCount);
        using var logScope = MicroShopLogging.BeginScope(
            _logger,
            _identity,
            Activity.Current);
        OutboxLog.Backlog(
            _logger,
            summary.PendingCount,
            summary.OldestPendingAge(now)?.TotalSeconds ?? 0,
            summary.DeadLetteredCount);
    }

    private async Task<ClaimResult> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var message = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM outbox_messages
                WHERE published_at_utc IS NULL
                  AND dead_lettered_at_utc IS NULL
                  AND next_attempt_at_utc <= {now}
                  AND (locked_until_utc IS NULL OR locked_until_utc <= {now})
                ORDER BY next_attempt_at_utc, created_at_utc, id
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return ClaimResult.None;
        }

        if (message.AttemptCount >= _options.MaxAttempts)
        {
            message.TryDeadLetter(
                workerId: null,
                now,
                "Maximum outbox delivery attempts were reached before the message could be dispatched.");
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ClaimResult.HandledWithoutClaim;
        }

        message.Claim(_workerId, now, _options.LeaseDuration);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ClaimResult(
            new OutboxClaim(
                message.Id,
                message.AggregateId,
                message.Payload,
                message.TraceParent,
                message.AttemptCount),
            Handled: true);
    }

    private async Task MarkPublishedAsync(
        OutboxClaim claim,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var message = await dbContext.OutboxMessages
            .SingleOrDefaultAsync(candidate => candidate.Id == claim.MessageId, cancellationToken);
        if (message is null
            || !message.TryMarkPublished(_workerId, DateTimeOffset.UtcNow))
        {
            OutboxLog.PublishLeaseLost(
                _logger,
                claim.MessageId,
                claim.AggregateId,
                _workerId);
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(
        OutboxClaim claim,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var message = await dbContext.OutboxMessages
            .SingleOrDefaultAsync(candidate => candidate.Id == claim.MessageId, cancellationToken);
        if (message is null)
        {
            return;
        }

        var deadLetter = claim.AttemptCount >= _options.MaxAttempts;
        message.SetLastError($"{exception.GetType().Name}: {exception.Message}");
        if (!message.TryMarkFailed(
                _workerId,
                DateTimeOffset.UtcNow,
                CalculateRetryDelay(claim.AttemptCount),
                deadLetter))
        {
            OutboxLog.FailedPublishLeaseLost(
                _logger,
                claim.MessageId,
                claim.AggregateId);
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        var delay = _options.RetryBaseDelay;
        for (var attempt = 1; attempt < attemptCount && delay < _options.RetryMaxDelay; attempt++)
        {
            var nextDelay = delay + delay;
            delay = nextDelay > _options.RetryMaxDelay
                ? _options.RetryMaxDelay
                : nextDelay;
        }

        return delay;
    }

    private static Activity? StartPublisherActivity(OutboxClaim claim)
    {
        Activity? activity;
        if (ActivityContext.TryParse(claim.TraceParent, null, isRemote: true, out var parentContext))
        {
            activity = MicroShopTelemetry.ActivitySource.StartActivity(
                "microshop.order_confirmed.publish",
                ActivityKind.Producer,
                parentContext);
        }
        else
        {
            activity = MicroShopTelemetry.ActivitySource.StartActivity(
                "microshop.order_confirmed.publish",
                ActivityKind.Producer);
        }

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation.name", "publish");
        activity?.SetTag("messaging.message.id", claim.MessageId);
        activity?.SetTag("microshop.order.id", claim.AggregateId);
        return activity;
    }

    private sealed record OutboxClaim(
        Guid MessageId,
        Guid AggregateId,
        string Payload,
        string? TraceParent,
        int AttemptCount);

    private sealed record ClaimResult(OutboxClaim? Claim, bool Handled)
    {
        public static ClaimResult None { get; } = new(null, false);

        public static ClaimResult HandledWithoutClaim { get; } = new(null, true);
    }
}

internal static partial class OutboxLog
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Error,
        Message = "Order outbox message failed. EventCode=ORDER_OUTBOX_PUBLISH_FAILED MessageId={MessageId} OrderId={OrderId} AttemptCount={AttemptCount}.")]
    public static partial void PublishFailed(
        ILogger logger,
        Exception exception,
        Guid messageId,
        Guid orderId,
        int attemptCount);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Error,
        Message = "Order outbox dispatcher iteration failed.")]
    public static partial void IterationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Warning,
        Message = "Order outbox message was published but its lease was no longer owned. EventCode=ORDER_OUTBOX_LEASE_LOST MessageId={MessageId} OrderId={OrderId} WorkerId={WorkerId}.")]
    public static partial void PublishLeaseLost(
        ILogger logger,
        Guid messageId,
        Guid orderId,
        string workerId);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Warning,
        Message = "Order outbox message could not be updated after a failed publish because its lease was lost. EventCode=ORDER_OUTBOX_FAILURE_LEASE_LOST MessageId={MessageId} OrderId={OrderId}.")]
    public static partial void FailedPublishLeaseLost(
        ILogger logger,
        Guid messageId,
        Guid orderId);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Information,
        Message = "Order outbox backlog: {PendingCount} pending message(s), oldest age {OldestPendingAgeSeconds} second(s), {DeadLetteredCount} dead-lettered message(s).")]
    public static partial void Backlog(
        ILogger logger,
        int pendingCount,
        double oldestPendingAgeSeconds,
        int deadLetteredCount);
}
