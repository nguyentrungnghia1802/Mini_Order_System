using MicroShop.OrderService.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MicroShop.OrderService.Tests;

public sealed class OrderDatabaseFixture : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase($"microshop_order_test_{Guid.NewGuid():N}")
        .WithUsername("order_test")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();
    private OrderApiFactory? _factory;

    public HttpClient Client => _factory?.CreateClient()
        ?? throw new InvalidOperationException("The Order API fixture has not started.");

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new OrderApiFactory(ConnectionString);
        _ = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public OrderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName))
            .Options;
        return new OrderDbContext(options);
    }

    public async Task<bool> OrderUserCannotConnectToProductDatabaseAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var orderRole = $"order_boundary_{suffix}";
        var productRole = $"product_boundary_{suffix}";
        var orderPassword = Guid.NewGuid().ToString("N");
        var productPassword = Guid.NewGuid().ToString("N");
        var productDatabase = $"microshop_product_isolation_{suffix}";

        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();

        await ExecuteAsync(
            admin,
            $"CREATE ROLE {QuoteIdentifier(orderRole)} LOGIN PASSWORD {QuoteLiteral(orderPassword)}");
        await ExecuteAsync(
            admin,
            $"CREATE ROLE {QuoteIdentifier(productRole)} LOGIN PASSWORD {QuoteLiteral(productPassword)}");
        await ExecuteAsync(
            admin,
            $"CREATE DATABASE {QuoteIdentifier(productDatabase)} OWNER {QuoteIdentifier(productRole)}");
        await ExecuteAsync(
            admin,
            $"REVOKE CONNECT ON DATABASE {QuoteIdentifier(productDatabase)} FROM PUBLIC");
        await ExecuteAsync(
            admin,
            $"GRANT CONNECT ON DATABASE {QuoteIdentifier(productDatabase)} TO {QuoteIdentifier(productRole)}");

        var productConnection = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = productDatabase,
            Username = orderRole,
            Password = orderPassword
        };

        try
        {
            await using var orderConnection = new NpgsqlConnection(productConnection.ConnectionString);
            await orderConnection.OpenAsync();
            return false;
        }
        catch (PostgresException exception) when (exception.SqlState == "42501")
        {
            return true;
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        await _postgres.DisposeAsync();
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _factory = null;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string QuoteLiteral(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private sealed class OrderApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OrderDatabase:ConnectionString"] = connectionString
                });
            });
        }
    }
}
