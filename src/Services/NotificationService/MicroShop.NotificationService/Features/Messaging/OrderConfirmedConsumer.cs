using MassTransit;
using MicroShop.Contracts.Orders;
using MicroShop.NotificationService.Persistence;
using MicroShop.NotificationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MicroShop.NotificationService.Features.Messaging;

public sealed partial class OrderConfirmedNotificationHandler(
    NotificationDbContext dbContext,
    ILogger<OrderConfirmedNotificationHandler> logger)
{
    public async Task HandleAsync(
        OrderConfirmedV1 message,
        Guid messageId,
        string? traceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        Validate(message, messageId);

        if (await dbContext.ConsumedMessages.AnyAsync(
                consumed => consumed.MessageId == messageId,
                cancellationToken))
        {
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
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            LogConcurrentDuplicate(exception, messageId, message.OrderId);
        }
    }

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Information,
        Message = "Duplicate OrderConfirmedV1 suppressed. MessageId={MessageId} OrderId={OrderId}")]
    private partial void LogDuplicate(Guid messageId, Guid orderId);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Information,
        Message = "Concurrent duplicate OrderConfirmedV1 suppressed. MessageId={MessageId} OrderId={OrderId}")]
    private partial void LogConcurrentDuplicate(Exception exception, Guid messageId, Guid orderId);

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
        var traceId = context.Headers.TryGetHeader("traceparent", out var traceParent)
            ? traceParent?.ToString()
            : null;
        return handler.HandleAsync(
            context.Message,
            messageId,
            traceId,
            context.CancellationToken);
    }
}
