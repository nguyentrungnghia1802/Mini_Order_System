using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace MicroShop.ProductService.Persistence;

public sealed class ProductDbContextFactory : IDesignTimeDbContextFactory<ProductDbContext>
{
    public ProductDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODUCT_DB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = ReadEnvironment("PRODUCT_DB_HOST", "localhost"),
                Port = ReadPort("PRODUCT_DB_PORT", 5432),
                Database = ReadEnvironment("PRODUCT_DB_NAME", "microshop_product"),
                Username = ReadEnvironment("PRODUCT_DB_USER", "product_app"),
                ApplicationName = "microshop-product-ef"
            };

            var password = Environment.GetEnvironmentVariable("PRODUCT_DB_PASSWORD");
            if (!string.IsNullOrWhiteSpace(password))
            {
                builder.Password = password;
            }

            connectionString = builder.ConnectionString;
        }

        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(ProductDbContext).Assembly.FullName))
            .Options;

        return new ProductDbContext(options);
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
