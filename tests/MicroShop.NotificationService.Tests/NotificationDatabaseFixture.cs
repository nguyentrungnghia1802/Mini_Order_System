using MicroShop.NotificationService.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MicroShop.NotificationService.Tests;

public sealed class NotificationDatabaseFixture : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase($"microshop_notification_test_{Guid.NewGuid():N}")
        .WithUsername("notification_test")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();
    private NotificationApiFactory? factory;

    public string ConnectionString => postgres.GetConnectionString();

    public HttpClient CreateClient()
    {
        return factory?.CreateClient()
            ?? throw new InvalidOperationException("The Notification API fixture has not started.");
    }

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        factory = new NotificationApiFactory(ConnectionString);
    }

    public NotificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.FullName))
            .Options;
        return new NotificationDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
            factory = null;
        }

        await postgres.DisposeAsync();
    }

    public void Dispose()
    {
        factory?.Dispose();
        factory = null;
    }

}
