using System.Net;
using System.Net.Http.Json;
using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MicroShop.OrderService.Tests;

public sealed class OrderPersistenceTests(OrderDatabaseFixture fixture) : IClassFixture<OrderDatabaseFixture>
{
    [Fact]
    public async Task OrderServiceStartsWithOwnedDatabaseAndReadiness()
    {
        using var rootResponse = await fixture.Client.GetAsync("/");
        using var readinessResponse = await fixture.Client.GetAsync("/health/ready");
        using var openApiResponse = await fixture.Client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
    }

    [Fact]
    public async Task PersistsOrderItemsAndStateHistoryWithAuthoritativeSnapshots()
    {
        var now = DateTimeOffset.UtcNow;
        var order = Order.Create(Guid.NewGuid(), "Nguyen Van A", "A@EXAMPLE.COM", now);
        order.AddItem(OrderItem.Create(Guid.NewGuid(), "Keyboard", 1_200_000m, 2));
        order.AddItem(OrderItem.Create(Guid.NewGuid(), "Mouse", 450_000m, 1));
        order.TransitionTo(OrderStatuses.Confirmed, "INVENTORY_RESERVED", "trace-order-1", now.AddSeconds(1));

        await using (var dbContext = fixture.CreateDbContext())
        {
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var persisted = await readContext.Orders
            .AsNoTracking()
            .Include(candidate => candidate.Items)
            .Include(candidate => candidate.StateHistory)
            .SingleAsync(candidate => candidate.Id == order.Id);

        Assert.Equal(OrderStatuses.Confirmed, persisted.Status);
        Assert.Equal("a@example.com", persisted.CustomerEmail);
        Assert.Equal(2_850_000m, persisted.TotalAmount);
        Assert.Equal(2, persisted.Items.Count);
        Assert.Equal(2, persisted.StateHistory.Count);
        Assert.Contains(persisted.Items, item => item.ProductName == "Keyboard" && item.UnitPrice == 1_200_000m);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task DatabaseRejectsUnknownOrderStatus()
    {
        await using var dbContext = fixture.CreateDbContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO orders
                    (id, customer_name, customer_email, status, currency, total_amount,
                     created_at_utc, updated_at_utc, version)
                VALUES
                    ({Guid.NewGuid()}, 'Constraint Test', 'constraint@example.com', 'not_a_status',
                     'VND', 0, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow}, 1)
                """));

        Assert.Equal("23514", exception.SqlState);
    }

    [Fact]
    public async Task OrderDatabaseCredentialsCannotConnectToProductDatabase()
    {
        Assert.True(await fixture.OrderUserCannotConnectToProductDatabaseAsync());
    }
}
