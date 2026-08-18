using System.Diagnostics;
using MassTransit;
using MicroShop.Contracts.Orders;
using MicroShop.NotificationService.Persistence;
using MicroShop.NotificationService.Persistence.Entities;
using MicroShop.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MicroShop.NotificationService.Features.Messaging;

public sealed partial class OrderConfirmedNotificationHandler(
    NotificationDbContext dbContext,
    ILogger<OrderConfirmedNotificationHandler> logger,
    MicroShopServiceIdentity? identity = null)
{
    public async Task HandleAsync(
        OrderConfirmedV1 message,
        Guid messageId,
        string? traceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Validate(message, messageId);
        using var logScope = MicroShopLogging.BeginScope(
            logger,
            identity,
            Activity.Current,
            orderId: message.OrderId,
            messageId: messageId);

        if (await dbContext.ConsumedMessages.AnyAsync(
                consumed => consumed.MessageId == messageId,
                cancellationToken))
        {
            MicroShopTelemetry.NotificationConsumeResults.Add(
                1,
                MicroShopTelemetry.Tags("order_confirmed", "duplicate"));
            LogDuplicate(messageId, message.OrderId);
            return;
        }

        var consumedMessage = ConsumedMessage.Create(
            messageId,
            typeof(OrderConfirmedV1).FullName ?? nameof(OrderConfirmedV1),
            nameof(OrderConfirmedNotificationHandler),
            DateTimeOffset.UtcNow,
            traceId);
        var notification = Notification.CreateFrom(messageId, message, DateTimeOffset.UtcNow);
        consumedMessage.AttachNotification(notification);
        dbContext.ConsumedMessages.Add(consumedMessage);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            MicroShopTelemetry.NotificationConsumeResults.Add(
                1,
                MicroShopTelemetry.Tags("order_confirmed", "success"));
            LogProcessed(messageId, message.OrderId);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            MicroShopTelemetry.NotificationConsumeResults.Add(
                1,
                MicroShopTelemetry.Tags("order_confirmed", "duplicate_concurrent"));
            LogConcurrentDuplicate(exception, messageId, message.OrderId);
        }
        catch
        {
            MicroShopTelemetry.NotificationConsumeResults.Add(
                1,
                MicroShopTelemetry.Tags("order_confirmed", "failure"));
            throw;
        }
    }

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Information,
        Message = "Duplicate OrderConfirmedV1 suppressed. EventCode=NOTIFICATION_DUPLICATE_SUPPRESSED MessageId={MessageId} OrderId={OrderId}")]
    private partial void LogDuplicate(Guid messageId, Guid orderId);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Information,
        Message = "Concurrent duplicate OrderConfirmedV1 suppressed. EventCode=NOTIFICATION_DUPLICATE_CONCURRENT MessageId={MessageId} OrderId={OrderId}")]
    private partial void LogConcurrentDuplicate(Exception exception, Guid messageId, Guid orderId);

    [LoggerMessage(
        EventId = 5103,
        Level = LogLevel.Information,
        Message = "OrderConfirmedV1 notification processed. EventCode=NOTIFICATION_CONSUMED MessageId={MessageId} OrderId={OrderId}")]
    private partial void LogProcessed(Guid messageId, Guid orderId);

    private static void Validate(OrderConfirmedV1 message, Guid messageId)
    {
        if (messageId == Guid.Empty || message.MessageId == Guid.Empty)
        {
            throw new InvalidOperationException("OrderConfirmedV1 requires a message ID.");
        }

        if (message.MessageId != messageId)
        {
            throw new InvalidOperationException("The envelope and OrderConfirmedV1 message IDs must match.");
        }

        if (message.SchemaVersion != 1)
        {
            throw new InvalidOperationException($"Unsupported OrderConfirmedV1 schema version '{message.SchemaVersion}'.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }
}

public sealed class OrderConfirmedConsumer(OrderConfirmedNotificationHandler handler)
    : IConsumer<OrderConfirmedV1>
{
    public Task Consume(ConsumeContext<OrderConfirmedV1> context)
    {
        var messageId = context.MessageId ?? context.Message.MessageId;
        var traceParent = context.Headers.TryGetHeader("traceparent", out var traceParentHeader)
            ? traceParentHeader?.ToString()
            : null;
        using var activity = StartConsumerActivity(
            traceParent,
            messageId,
            context.Message.OrderId);
        return handler.HandleAsync(
            context.Message,
            messageId,
            traceParent,
            context.CancellationToken);
    }

    private static Activity? StartConsumerActivity(
        string? traceParent,
        Guid messageId,
        Guid orderId)
    {
        Activity? activity;
        if (ActivityContext.TryParse(traceParent, null, isRemote: true, out var parentContext))
        {
            activity = MicroShopTelemetry.ActivitySource.StartActivity(
                "microshop.order_confirmed.consume",
                ActivityKind.Consumer,
                parentContext);
        }
        else
        {
            activity = MicroShopTelemetry.ActivitySource.StartActivity(
                "microshop.order_confirmed.consume",
                ActivityKind.Consumer);
        }

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation.name", "process");
        activity?.SetTag("messaging.message.id", messageId);
        activity?.SetTag("microshop.order.id", orderId);
        return activity;
    }
}
