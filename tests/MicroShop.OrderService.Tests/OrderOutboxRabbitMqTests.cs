using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Features.Orders;
using MicroShop.OrderService.Infrastructure.Products;
using MicroShop.OrderService.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.RabbitMq;

namespace MicroShop.OrderService.Tests;

public sealed class OrderOutboxRabbitMqTests(OrderDatabaseFixture fixture)
    : IClassFixture<OrderDatabaseFixture>, IAsyncLifetime, IDisposable
{
    private readonly RabbitMqContainer rabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .WithUsername("microshop_order_test")
        .WithPassword("microshop_order_test_password")
        .Build();
    private OutboxRabbitApiFactory? factory;
    private HttpClient? client;

    public async Task InitializeAsync()
    {
        await rabbitMq.StartAsync();
        factory = new OutboxRabbitApiFactory(fixture.ConnectionString, rabbitMq.GetConnectionString());
        client = factory.CreateClient();
        await WaitForReadinessAsync(client);
    }

    [Fact]
    public async Task RabbitMqOutageLeavesConfirmedOrderDurableAndRecoveryDrainsOutbox()
    {
        var apiClient = client ?? throw new InvalidOperationException("The Order API fixture has not started.");
        await rabbitMq.StopAsync();

        var email = $"outbox-rabbit-outage-{Guid.NewGuid():N}@example.com";
        using var response = await apiClient.PostAsJsonAsync(
            "/api/v1/orders",
            new
            {
                customerName = "Rabbit Outage Test",
                customerEmail = email,
                items = new[]
                {
                    new
                    {
                        productId = FakeProductCatalog.MechanicalKeyboardId,
                        quantity = 1
                    }
                }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(created);
        Assert.Equal(OrderStatuses.Confirmed, created.Status);

        await using (var verificationContext = fixture.CreateDbContext())
        {
            var persisted = await verificationContext.Orders
                .AsNoTracking()
                .SingleAsync(order => order.Id == created.Id);
            Assert.Equal(OrderStatuses.Confirmed, persisted.Status);
        }

        await WaitForConditionAsync(async () =>
        {
            await using var recoveryContext = fixture.CreateDbContext();
            var message = await recoveryContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(candidate => candidate.AggregateId == created.Id);
            return message.PublishedAtUtc is null && message.AttemptCount > 0;
        }, TimeSpan.FromSeconds(10));

        await using (var outageContext = fixture.CreateDbContext())
        {
            var pending = await outageContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(message => message.AggregateId == created.Id);
            Assert.Null(pending.PublishedAtUtc);
            Assert.Null(pending.DeadLetteredAtUtc);
        }

        using (var outageReadiness = await apiClient.GetAsync("/health/ready"))
        {
            Assert.Equal(HttpStatusCode.OK, outageReadiness.StatusCode);
        }

        client.Dispose();
        client = null;
        factory?.Dispose();
        factory = null;
        await Task.Delay(TimeSpan.FromMilliseconds(1_200));
        await rabbitMq.StartAsync();
        factory = new OutboxRabbitApiFactory(fixture.ConnectionString, rabbitMq.GetConnectionString());
        client = factory.CreateClient();
        await WaitForReadinessAsync(client);

        await WaitForConditionAsync(async () =>
        {
            await using var recoveryContext = fixture.CreateDbContext();
            var message = await recoveryContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(candidate => candidate.AggregateId == created.Id);
            return message.PublishedAtUtc is not null;
        }, TimeSpan.FromSeconds(30));

        await using var finalContext = fixture.CreateDbContext();
        var published = await finalContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.AggregateId == created.Id);
        Assert.True(published.AttemptCount >= 2);
        Assert.NotNull(published.PublishedAtUtc);
    }

    public async Task DisposeAsync()
    {
        Dispose();

        await rabbitMq.DisposeAsync();
    }

    public void Dispose()
    {
        client?.Dispose();
        client = null;
        factory?.Dispose();
        factory = null;
    }

    private static async Task WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new Xunit.Sdk.XunitException("The outbox condition was not reached before the timeout.");
    }

    private static Task WaitForReadinessAsync(HttpClient httpClient)
    {
        return WaitForConditionAsync(async () =>
        {
            using var response = await httpClient.GetAsync("/health/ready");
            return response.StatusCode == HttpStatusCode.OK;
        }, TimeSpan.FromSeconds(30));
    }

    private sealed class OutboxRabbitApiFactory(
        string orderConnectionString,
        string rabbitConnectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Integration");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var rabbit = new Uri(rabbitConnectionString);
                var credentials = rabbit.UserInfo.Split(':', 2);
                var virtualHost = Uri.UnescapeDataString(rabbit.AbsolutePath.TrimStart('/'));
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OrderDatabase:ConnectionString"] = orderConnectionString,
                    ["ProductService:UseFakeClient"] = bool.TrueString,
                    ["ProductService:BaseUrl"] = "http://product.test",
                    ["RABBITMQ_HOST"] = rabbit.Host,
                    ["RABBITMQ_PORT"] = rabbit.Port.ToString(CultureInfo.InvariantCulture),
                    ["RABBITMQ_VHOST"] = string.IsNullOrWhiteSpace(virtualHost) ? "/" : virtualHost,
                    ["RABBITMQ_USER"] = Uri.UnescapeDataString(credentials[0]),
                    ["RABBITMQ_PASSWORD"] = credentials.Length > 1
                        ? Uri.UnescapeDataString(credentials[1])
                        : null,
                    ["RABBITMQ_USE_IN_MEMORY"] = bool.FalseString,
                    ["ORDER_OUTBOX_ENABLED"] = bool.TrueString,
                    ["ORDER_OUTBOX_POLL_INTERVAL_MS"] = "100",
                    ["ORDER_OUTBOX_LEASE_DURATION_MS"] = "1000",
                    ["ORDER_OUTBOX_RETRY_BASE_DELAY_MS"] = "100",
                    ["ORDER_OUTBOX_RETRY_MAX_DELAY_MS"] = "500",
                    ["ORDER_OUTBOX_BACKLOG_LOG_INTERVAL_MS"] = "1000"
                });
            });
        }
    }
}
