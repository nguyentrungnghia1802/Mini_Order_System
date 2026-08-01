using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace MicroShop.OrderService.Persistence;

public sealed class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ORDER_DB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = ReadEnvironment("ORDER_DB_HOST", "localhost"),
                Port = ReadPort("ORDER_DB_PORT", 5432),
                Database = ReadEnvironment("ORDER_DB_NAME", "microshop_order"),
                Username = ReadEnvironment("ORDER_DB_USER", "order_app"),
                ApplicationName = "microshop-order-ef"
            };

            var password = Environment.GetEnvironmentVariable("ORDER_DB_PASSWORD");
            if (!string.IsNullOrWhiteSpace(password))
            {
                builder.Password = password;
            }

            connectionString = builder.ConnectionString;
        }

        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName))
            .Options;

        return new OrderDbContext(options);
    }

    private static string ReadEnvironment(string name, string fallback)
    {
        return Environment.GetEnvironmentVariable(name) ?? fallback;
    }

    private static int ReadPort(string name, int fallback)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), out var port) ? port : fallback;
    }
}
