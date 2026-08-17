using System.Collections.Concurrent;
using MicroShop.Contracts.Orders;
using MicroShop.OrderService.Infrastructure.Messaging;
using MicroShop.OrderService.Persistence;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MicroShop.OrderService.Tests;

public sealed class OutboxDispatcherTests(OrderDatabaseFixture fixture)
    : IClassFixture<OrderDatabaseFixture>
{
    [Fact]
    public async Task DispatcherPublishesAndMarksOutboxWithStableMessageId()
    {
        var messageId = await AddPendingMessageAsync();
        var transport = new RecordingTransport();
        await using var services = CreateServices(transport);
        var dispatcher = CreateDispatcher(services);

        Assert.True(await dispatcher.DispatchOnceAsync());

        var published = Assert.Single(transport.Messages);
        Assert.Equal(messageId, published.MessageId);
        await using var verificationContext = fixture.CreateDbContext();
        var outbox = await verificationContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == messageId);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.NotNull(outbox.PublishedAtUtc);
        Assert.Null(outbox.DeadLetteredAtUtc);
        Assert.Null(outbox.LockedBy);
        Assert.Null(outbox.LockedUntilUtc);
        Assert.Null(outbox.LastError);
    }

    [Fact]
    public async Task ConcurrentDispatchersClaimAnOutboxMessageOnlyOnce()
    {
        var messageId = await AddPendingMessageAsync();
        var transport = new RecordingTransport();
        await using var firstServices = CreateServices(transport);
        await using var secondServices = CreateServices(transport);
        var firstDispatcher = CreateDispatcher(firstServices);
        var secondDispatcher = CreateDispatcher(secondServices);

        await Task.WhenAll(
            firstDispatcher.DispatchOnceAsync(),
            secondDispatcher.DispatchOnceAsync());

        Assert.Single(transport.Messages);
        Assert.Equal(messageId, transport.Messages.Single().MessageId);
        await using var verificationContext = fixture.CreateDbContext();
        var outbox = await verificationContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == messageId);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.NotNull(outbox.PublishedAtUtc);
    }

    [Fact]
    public async Task FailedPublishRetriesAndDeadLettersAtMaximumAttempts()
    {
        var messageId = await AddPendingMessageAsync();
        var transport = new RecordingTransport { Fail = true };
        var options = new OutboxOptions
        {
            MaxAttempts = 2,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
            RetryMaxDelay = TimeSpan.FromMilliseconds(1)
        };
        await using var services = CreateServices(transport);
        var dispatcher = CreateDispatcher(services, options);

        Assert.True(await dispatcher.DispatchOnceAsync());
        await Task.Delay(25);
        Assert.True(await dispatcher.DispatchOnceAsync());

        await using var verificationContext = fixture.CreateDbContext();
        var outbox = await verificationContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == messageId);
        Assert.Equal(2, outbox.AttemptCount);
        Assert.NotNull(outbox.DeadLetteredAtUtc);
        Assert.Null(outbox.PublishedAtUtc);
        Assert.Contains("InvalidOperationException", outbox.LastError);
    }

    [Fact]
    public async Task LeaseExpiryAllowsAReplacementDispatcherToRecoverAfterRestart()
    {
        var messageId = await AddPendingMessageAsync();
        var blockingTransport = new BlockingTransport();
        var options = new OutboxOptions
        {
            MaxAttempts = 3,
            LeaseDuration = TimeSpan.FromSeconds(1),
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
            RetryMaxDelay = TimeSpan.FromMilliseconds(1)
        };
        await using var firstServices = CreateServices(blockingTransport);
        var firstDispatcher = CreateDispatcher(firstServices, options);
        using var cancellation = new CancellationTokenSource();
        var firstDispatch = firstDispatcher.DispatchOnceAsync(cancellation.Token);
        await blockingTransport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstDispatch);

        await Task.Delay(TimeSpan.FromMilliseconds(1_200));
        var recoveryTransport = new RecordingTransport();
        await using var recoveryServices = CreateServices(recoveryTransport);
        var recoveryDispatcher = CreateDispatcher(recoveryServices, options);

        Assert.True(await recoveryDispatcher.DispatchOnceAsync());
        Assert.Equal(messageId, Assert.Single(recoveryTransport.Messages).MessageId);
        await using var verificationContext = fixture.CreateDbContext();
        var outbox = await verificationContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == messageId);
        Assert.Equal(2, outbox.AttemptCount);
        Assert.NotNull(outbox.PublishedAtUtc);
        Assert.Null(outbox.DeadLetteredAtUtc);
    }

    private async Task<Guid> AddPendingMessageAsync()
    {
        var message = new OrderConfirmedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Outbox Test Customer",
            $"{Guid.NewGuid():N}@example.com",
            125m,
            "VND",
            [new(Guid.NewGuid(), "Outbox Product", 125m, 1, 125m)],
            DateTimeOffset.UtcNow);
        await using var dbContext = fixture.CreateDbContext();
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            message.MessageId,
            OrderConfirmedMessageFactory.MessageType,
            message.OrderId,
            OrderConfirmedMessageFactory.Serialize(message),
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            message.OccurredAtUtc));
        await dbContext.SaveChangesAsync();
        return message.MessageId;
    }

    private ServiceProvider CreateServices(
        IOrderConfirmedMessageTransport transport)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(fixture.ConnectionString));
        services.AddSingleton<IOrderConfirmedMessageTransport>(transport);
        return services.BuildServiceProvider();
    }

    private static OutboxDispatcher CreateDispatcher(
        ServiceProvider services,
        OutboxOptions? options = null)
    {
        return new OutboxDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options ?? new OutboxOptions()),
            NullLogger<OutboxDispatcher>.Instance);
    }

    private sealed class RecordingTransport : IOrderConfirmedMessageTransport
    {
        public bool Fail { get; init; }

        public ConcurrentQueue<OrderConfirmedV1> Messages { get; } = new();

        public Task PublishAsync(
            OrderConfirmedV1 message,
            string? traceParent,
            CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new InvalidOperationException("Simulated broker outage.");
            }

            Messages.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingTransport : IOrderConfirmedMessageTransport
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task PublishAsync(
            OrderConfirmedV1 message,
            string? traceParent,
            CancellationToken cancellationToken)
        {
            Entered.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
