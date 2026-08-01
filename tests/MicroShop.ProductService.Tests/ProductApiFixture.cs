using MicroShop.ProductService.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MicroShop.ProductService.Tests;

public sealed class ProductApiFixture : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase($"microshop_product_test_{Guid.NewGuid():N}")
        .WithUsername("product_test")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();
    private ProductApiFactory? _factory;

    public HttpClient Client => _factory?.CreateClient()
        ?? throw new InvalidOperationException("The Product API fixture has not started.");

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new ProductApiFactory(ConnectionString);
        _ = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        await dbContext.Database.MigrateAsync();
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

    public async Task<bool> ProductTableHasNegativeStockConstraintAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var product = new MicroShop.ProductService.Persistence.Entities.Product
        {
            Id = Guid.NewGuid(),
            Name = "Constraint test",
            UnitPrice = 1m,
            Currency = "VND",
            AvailableStock = -1,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Version = 1
        };

        dbContext.Products.Add(product);
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }

    private sealed class ProductApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ProductDatabase:ConnectionString"] = connectionString
                });
            });
        }
    }
}
