using MicroShop.Contracts.Orders;
using MicroShop.NotificationService.Features.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicroShop.NotificationService.Tests;

public sealed class NotificationConsumerTests(NotificationDatabaseFixture fixture)
    : IClassFixture<NotificationDatabaseFixture>
{
    [Fact]
    public async Task PersistsConsumedMessageAndReadableNotificationInOneOwnedDatabase()
    {
        var message = CreateMessage();
        await using var dbContext = fixture.CreateDbContext();
        var handler = CreateHandler(dbContext);

        await handler.HandleAsync(message, message.MessageId, "00-trace-1", CancellationToken.None);

        await using var verificationContext = fixture.CreateDbContext();
        var consumed = await verificationContext.ConsumedMessages
            .Include(candidate => candidate.Notification)
            .SingleAsync(candidate => candidate.MessageId == message.MessageId);
        var notification = await verificationContext.Notifications
            .SingleAsync(candidate => candidate.SourceMessageId == message.MessageId);

        Assert.Equal(typeof(OrderConfirmedV1).FullName, consumed.MessageType);
        Assert.Equal("00-trace-1", consumed.TraceId);
        Assert.NotNull(consumed.Notification);
        Assert.Equal(message.OrderId, notification.OrderId);
        Assert.Equal("a@example.com", notification.CustomerEmail);
        Assert.Equal("Order confirmed", notification.Subject);
        Assert.Contains(message.OrderId.ToString(), notification.Body, StringComparison.Ordinal);
        Assert.Contains("1,250,000.00", notification.Body, StringComparison.Ordinal);
        Assert.Equal(1_250_000m, notification.TotalAmount);
        Assert.Equal("VND", notification.Currency);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task SuppressesDuplicateDeliveryByMessageId()
    {
        var message = CreateMessage();
        await using (var firstContext = fixture.CreateDbContext())
        {
            await CreateHandler(firstContext).HandleAsync(
                message,
                message.MessageId,
                "00-trace-first",
                CancellationToken.None);
        }

        await using (var secondContext = fixture.CreateDbContext())
        {
            await CreateHandler(secondContext).HandleAsync(
                message,
                message.MessageId,
                "00-trace-replay",
                CancellationToken.None);
        }

        await using var verificationContext = fixture.CreateDbContext();
        Assert.Equal(1, await verificationContext.ConsumedMessages.CountAsync(
            candidate => candidate.MessageId == message.MessageId));
        Assert.Equal(1, await verificationContext.Notifications.CountAsync(
            candidate => candidate.SourceMessageId == message.MessageId));
    }

    [Fact]
    public async Task SuppressesConcurrentDuplicateDeliveryByDatabaseConstraints()
    {
        var message = CreateMessage();
        var handlers = Enumerable.Range(0, 8)
            .Select(_ => ConsumeWithIndependentContextAsync(message))
            .ToArray();

        await Task.WhenAll(handlers);

        await using var verificationContext = fixture.CreateDbContext();
        Assert.Equal(1, await verificationContext.ConsumedMessages.CountAsync(
            candidate => candidate.MessageId == message.MessageId));
        Assert.Equal(1, await verificationContext.Notifications.CountAsync(
            candidate => candidate.SourceMessageId == message.MessageId));
    }

    [Fact]
    public async Task RollsBackConsumedMessageWhenNotificationWriteFails()
    {
        var message = CreateMessage() with
        {
            CustomerEmail = new string('x', 321) + "@example.com"
        };

        await using (var dbContext = fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => CreateHandler(dbContext).HandleAsync(
                message,
                message.MessageId,
                null,
                CancellationToken.None));
        }

        await using var verificationContext = fixture.CreateDbContext();
        Assert.False(await verificationContext.ConsumedMessages.AnyAsync(
            candidate => candidate.MessageId == message.MessageId));
        Assert.False(await verificationContext.Notifications.AnyAsync(
            candidate => candidate.SourceMessageId == message.MessageId));
    }

    [Fact]
    public async Task RejectsUnsupportedSchemaBeforeWritingSideEffects()
    {
        var message = CreateMessage() with { SchemaVersion = 2 };
        await using var dbContext = fixture.CreateDbContext();
        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            message,
            message.MessageId,
            null,
            CancellationToken.None));

        Assert.False(await dbContext.ConsumedMessages.AnyAsync(
            candidate => candidate.MessageId == message.MessageId));
        Assert.False(await dbContext.Notifications.AnyAsync(
            candidate => candidate.SourceMessageId == message.MessageId));
    }

    private static OrderConfirmedNotificationHandler CreateHandler(
        MicroShop.NotificationService.Persistence.NotificationDbContext dbContext)
    {
        return new OrderConfirmedNotificationHandler(
            dbContext,
            NullLogger<OrderConfirmedNotificationHandler>.Instance);
    }

    private async Task ConsumeWithIndependentContextAsync(OrderConfirmedV1 message)
    {
        await using var dbContext = fixture.CreateDbContext();
        await CreateHandler(dbContext).HandleAsync(
            message,
            message.MessageId,
            "00-concurrent-duplicate",
            CancellationToken.None);
    }

    private static OrderConfirmedV1 CreateMessage()
    {
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        return new OrderConfirmedV1(
            messageId,
            orderId,
            "Nguyen Van A",
            "A@EXAMPLE.COM",
            1_250_000m,
            "VND",
            [new OrderConfirmedItemV1(productId, "Keyboard", 1_250_000m, 1, 1_250_000m)],
            DateTimeOffset.UtcNow);
    }
}
