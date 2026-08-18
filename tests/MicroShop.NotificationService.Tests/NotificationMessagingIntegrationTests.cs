using System.Diagnostics;
using System.Net;
using System.Net.Http;
using MassTransit;
using MicroShop.Contracts.Orders;
using MicroShop.NotificationService.Persistence.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MicroShop.NotificationService.Tests;

public sealed class NotificationMessagingIntegrationTests(
    NotificationDatabaseFixture database,
    RabbitMqMessagingFixture rabbitMq)
    : IClassFixture<NotificationDatabaseFixture>, IClassFixture<RabbitMqMessagingFixture>
{
    [Fact]
    public async Task PublishesOrderConfirmedEventAndNotificationConsumesIt()
    {
        await using var notificationService = await StartNotificationServiceAsync();
        await using var publisher = await rabbitMq.StartPublisherBusAsync();
        var message = CreateMessage();

        await PublishAsync(publisher.PublishEndpoint, message);

        var notification = await WaitForNotificationAsync(message.MessageId);
        Assert.Equal(message.OrderId, notification.OrderId);
        Assert.Equal(message.CustomerEmail.ToLowerInvariant(), notification.CustomerEmail);
        Assert.Equal(message.TotalAmount, notification.TotalAmount);
    }

    [Fact]
    public async Task PublishesDuplicateEventWithOneDurableNotification()
    {
        await using var notificationService = await StartNotificationServiceAsync();
        await using var publisher = await rabbitMq.StartPublisherBusAsync();
        var message = CreateMessage();

        await PublishAsync(publisher.PublishEndpoint, message);
        await PublishAsync(publisher.PublishEndpoint, message);

        var notification = await WaitForNotificationAsync(message.MessageId);
        await using var verificationContext = database.CreateDbContext();
        Assert.Equal(message.OrderId, notification.OrderId);
        Assert.Equal(
            1,
            await verificationContext.ConsumedMessages.CountAsync(
                consumed => consumed.MessageId == message.MessageId));
        Assert.Equal(
            1,
            await verificationContext.Notifications.CountAsync(
                candidate => candidate.SourceMessageId == message.MessageId));
    }

    [Fact]
    public async Task MovesUnsupportedMessageToErrorQueueAfterBoundedRetry()
    {
        await using var notificationService = await StartNotificationServiceAsync();
        await using var publisher = await rabbitMq.StartPublisherBusAsync();
        var message = CreateMessage() with { SchemaVersion = 2 };

        await PublishAsync(publisher.PublishEndpoint, message);

        var errorQueue = $"{RabbitMqMessagingFixture.NotificationEndpointName}_error";
        Assert.True(
            await rabbitMq.WaitForQueueMessageAsync(errorQueue, TimeSpan.FromSeconds(15)) >= 1);

        await using var verificationContext = database.CreateDbContext();
        Assert.False(await verificationContext.ConsumedMessages.AnyAsync(
            consumed => consumed.MessageId == message.MessageId));
        Assert.False(await verificationContext.Notifications.AnyAsync(
            notification => notification.SourceMessageId == message.MessageId));
    }

    [Fact]
    public async Task QueuedMessageSurvivesNotificationServiceRestart()
    {
        var initialService = await StartNotificationServiceAsync();
        await using var publisher = await rabbitMq.StartPublisherBusAsync();
        var message = CreateMessage();

        await initialService.DisposeAsync();
        await PublishAsync(publisher.PublishEndpoint, message);

        await using var restartedService = await StartNotificationServiceAsync();
        var notification = await WaitForNotificationAsync(message.MessageId);

        Assert.Equal(message.OrderId, notification.OrderId);
    }

    [Fact]
    public async Task ConsumerProcessRestartBetweenRedeliveryAttemptsPreservesIdempotency()
    {
        var initialService = await StartNotificationServiceAsync();
        await using var publisher = await rabbitMq.StartPublisherBusAsync();
        var message = CreateMessage();

        await PublishAsync(publisher.PublishEndpoint, message);
        await WaitForNotificationAsync(message.MessageId);

        await initialService.StopAsync();
        await initialService.DisposeAsync();

        await using var restartedService = await StartNotificationServiceAsync();
        await PublishAsync(publisher.PublishEndpoint, message);
        await WaitForQueueDepthAsync(0, TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await using var verificationContext = database.CreateDbContext();
        Assert.Equal(1, await verificationContext.ConsumedMessages.CountAsync(
            consumed => consumed.MessageId == message.MessageId));
        Assert.Equal(1, await verificationContext.Notifications.CountAsync(
            candidate => candidate.SourceMessageId == message.MessageId));
    }

    [Fact]
    public async Task PublishingDoesNotWaitForStoppedNotificationService()
    {
        await using var publisher = await rabbitMq.StartPublisherBusAsync();
        var message = CreateMessage();
        var stopwatch = Stopwatch.StartNew();

        await PublishAsync(publisher.PublishEndpoint, message)
            .WaitAsync(TimeSpan.FromSeconds(5));

        stopwatch.Stop();
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Publishing took {stopwatch.Elapsed} while no consumer was running.");

        await using var verificationContext = database.CreateDbContext();
        Assert.False(await verificationContext.Notifications.AnyAsync(
            notification => notification.SourceMessageId == message.MessageId));
    }

    private async Task<NotificationRabbitServiceHost> StartNotificationServiceAsync()
    {
        var rabbitConfiguration = rabbitMq.CreateApplicationConfiguration()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        var factory = new NotificationRabbitApiFactory(
            database.ConnectionString,
            rabbitConfiguration);
        var client = factory.CreateClient();
        try
        {
            await rabbitMq.WaitForQueueAsync(
                RabbitMqMessagingFixture.NotificationEndpointName,
                TimeSpan.FromSeconds(15));
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await client.GetAsync("/health/ready");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return new NotificationRabbitServiceHost(factory, client);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            throw new Xunit.Sdk.XunitException(
                "Notification Service did not become ready after its RabbitMQ endpoint was declared.");
        }
        catch
        {
            client.Dispose();
            await factory.DisposeAsync();
            throw;
        }
    }

    private async Task WaitForQueueDepthAsync(uint expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await rabbitMq.GetQueueMessageCountAsync(
                    RabbitMqMessagingFixture.NotificationEndpointName) == expected)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new Xunit.Sdk.XunitException(
            $"RabbitMQ queue '{RabbitMqMessagingFixture.NotificationEndpointName}' did not reach depth {expected}.");
    }

    private async Task<Notification> WaitForNotificationAsync(Guid sourceMessageId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var dbContext = database.CreateDbContext();
            var notification = await dbContext.Notifications
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.SourceMessageId == sourceMessageId);
            if (notification is not null)
            {
                return notification;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new Xunit.Sdk.XunitException(
            $"Notification for source message '{sourceMessageId}' was not persisted in time.");
    }

    private static Task PublishAsync(
        IPublishEndpoint publisher,
        OrderConfirmedV1 message)
    {
        return publisher.Publish(
            message,
            publishContext =>
            {
                publishContext.MessageId = message.MessageId;
                publishContext.CorrelationId = message.OrderId;
            });
    }

    private static OrderConfirmedV1 CreateMessage()
    {
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        return new OrderConfirmedV1(
            messageId,
            orderId,
            "RabbitMQ integration test",
            $"{messageId:N}@example.com",
            250_000m,
            "VND",
            [new OrderConfirmedItemV1(productId, "RabbitMQ test product", 250_000m, 1, 250_000m)],
            DateTimeOffset.UtcNow);
    }
}

public sealed class NotificationRabbitApiFactory(
    string databaseConnectionString,
    IReadOnlyDictionary<string, string?> rabbitConfiguration)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = rabbitConfiguration.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
            configuration["NOTIFICATION_DB_CONNECTION_STRING"] = databaseConnectionString;
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

public sealed class NotificationRabbitServiceHost(
    NotificationRabbitApiFactory factory,
    HttpClient client)
    : IAsyncDisposable
{
    private int disposed;

    public async Task StopAsync()
    {
        var applicationLifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();
        applicationLifetime.StopApplication();
        await Task.Delay(TimeSpan.FromMilliseconds(500));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        client.Dispose();
        await factory.DisposeAsync();
    }
}
