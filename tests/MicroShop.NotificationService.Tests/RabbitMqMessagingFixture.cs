using System.Globalization;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace MicroShop.NotificationService.Tests;

public sealed class RabbitMqMessagingFixture : IAsyncLifetime
{
    public const string NotificationEndpointName = "microshop-notification-order-confirmed-v1";

    private readonly RabbitMqContainer rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .WithUsername("microshop_test")
        .WithPassword("microshop_test_password")
        .Build();

    public string ConnectionString => rabbitMq.GetConnectionString();

    public async Task InitializeAsync()
    {
        await rabbitMq.StartAsync();
    }

    public async Task<RabbitMqBusHost> StartPublisherBusAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddMassTransit(massTransit =>
        {
            massTransit.UsingRabbitMq((_, bus) =>
            {
                bus.Host(new Uri(ConnectionString));
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var bus = serviceProvider.GetRequiredService<IBusControl>();
        await bus.StartAsync();
        return new RabbitMqBusHost(serviceProvider, bus);
    }

    public async Task<uint> GetQueueMessageCountAsync(string queueName)
    {
        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(ConnectionString)
        };
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var queue = await channel.QueueDeclarePassiveAsync(queueName);
        return queue.MessageCount;
    }

    public async Task WaitForQueueAsync(string queueName, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await GetQueueMessageCountAsync(queueName);
                return;
            }
            catch (Exception) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"RabbitMQ queue '{queueName}' was not declared in time.");
    }

    public async Task<uint> WaitForQueueMessageAsync(string queueName, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var messageCount = await GetQueueMessageCountAsync(queueName);
                if (messageCount > 0)
                {
                    return messageCount;
                }
            }
            catch (Exception) when (DateTimeOffset.UtcNow < deadline)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new Xunit.Sdk.XunitException(
            $"RabbitMQ queue '{queueName}' did not receive a message in time.");
    }

    public IReadOnlyDictionary<string, string?> CreateApplicationConfiguration()
    {
        var connection = new Uri(ConnectionString);
        var credentials = connection.UserInfo.Split(':', 2);
        var virtualHost = Uri.UnescapeDataString(connection.AbsolutePath.TrimStart('/'));

        return new Dictionary<string, string?>
        {
            ["RABBITMQ_HOST"] = connection.Host,
            ["RABBITMQ_PORT"] = connection.Port.ToString(CultureInfo.InvariantCulture),
            ["RABBITMQ_VHOST"] = string.IsNullOrWhiteSpace(virtualHost) ? "/" : virtualHost,
            ["RABBITMQ_USER"] = Uri.UnescapeDataString(credentials[0]),
            ["RABBITMQ_PASSWORD"] = credentials.Length > 1
                ? Uri.UnescapeDataString(credentials[1])
                : null,
            ["RABBITMQ_USE_IN_MEMORY"] = "false"
        };
    }

    public async Task DisposeAsync()
    {
        await rabbitMq.DisposeAsync();
    }
}

public sealed class RabbitMqBusHost(
    ServiceProvider serviceProvider,
    IBusControl bus) : IAsyncDisposable
{
    public IPublishEndpoint PublishEndpoint => bus;

    public async ValueTask DisposeAsync()
    {
        await bus.StopAsync();
        await serviceProvider.DisposeAsync();
    }
}
