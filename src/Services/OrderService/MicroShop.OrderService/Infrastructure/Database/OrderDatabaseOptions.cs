using Npgsql;

namespace MicroShop.OrderService.Infrastructure.Database;

public sealed class OrderDatabaseOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = "microshop_order";

    public string Username { get; set; } = "order_app";

    public string Password { get; set; } = string.Empty;

    public string? ConnectionString { get; set; }

    public bool HasConnectionCredentials =>
        !string.IsNullOrWhiteSpace(ConnectionString) || !string.IsNullOrWhiteSpace(Password);

    public string BuildConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return ConnectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            ApplicationName = "microshop-order-service",
            IncludeErrorDetail = false
        };

        return builder.ConnectionString;
    }
}
