using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace MicroShop.NotificationService.Persistence;

public sealed class NotificationDbContextFactory
    : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("NOTIFICATION_DB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = ReadEnvironment("NOTIFICATION_DB_HOST", "localhost"),
                Port = ReadPort("NOTIFICATION_DB_PORT", 5432),
                Database = ReadEnvironment("NOTIFICATION_DB_NAME", "microshop_notification"),
                Username = ReadEnvironment("NOTIFICATION_DB_USER", "notification_app"),
                ApplicationName = "microshop-notification-ef"
            };

            var password = Environment.GetEnvironmentVariable("NOTIFICATION_DB_PASSWORD");
            if (!string.IsNullOrWhiteSpace(password))
            {
                builder.Password = password;
            }

            connectionString = builder.ConnectionString;
        }

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.FullName))
            .Options;

        return new NotificationDbContext(options);
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
