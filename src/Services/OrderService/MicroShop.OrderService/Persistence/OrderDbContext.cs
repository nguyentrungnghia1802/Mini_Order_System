using MicroShop.OrderService.Persistence.Configurations;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.OrderService.Persistence;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderInventoryRequestItem> InventoryRequestItems => Set<OrderInventoryRequestItem>();

    public DbSet<OrderStateHistory> OrderStateHistory => Set<OrderStateHistory>();

    public DbSet<OrderReconciliationAudit> ReconciliationAudits => Set<OrderReconciliationAudit>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new OrderInventoryRequestItemConfiguration());
        modelBuilder.ApplyConfiguration(new OrderStateHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new OrderReconciliationAuditConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
