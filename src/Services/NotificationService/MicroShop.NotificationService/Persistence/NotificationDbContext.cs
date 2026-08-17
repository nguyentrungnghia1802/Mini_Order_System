using MicroShop.NotificationService.Persistence.Configurations;
using MicroShop.NotificationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroShop.NotificationService.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options)
{
    public DbSet<ConsumedMessage> ConsumedMessages => Set<ConsumedMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ConsumedMessageConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
    }
}
