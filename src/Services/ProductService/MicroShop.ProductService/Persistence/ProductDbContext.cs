using MicroShop.ProductService.Persistence.Configurations;
using MicroShop.ProductService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.ProductService.Persistence;

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();

    public DbSet<InventoryReservationItem> InventoryReservationItems => Set<InventoryReservationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryReservationConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryReservationItemConfiguration());
    }
}
