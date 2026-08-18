using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.OrderService.Persistence.Configurations;

public sealed class OrderInventoryRequestItemConfiguration
    : IEntityTypeConfiguration<OrderInventoryRequestItem>
{
    public void Configure(EntityTypeBuilder<OrderInventoryRequestItem> builder)
    {
        builder.ToTable("order_inventory_request_items", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_order_inventory_request_items_quantity_positive",
                "quantity > 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(item => item.OrderId)
            .HasColumnName("order_id")
            .IsRequired();
        builder.Property(item => item.ProductId)
            .HasColumnName("product_id")
            .IsRequired();
        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.HasIndex(item => new { item.OrderId, item.ProductId })
            .IsUnique()
            .HasDatabaseName("ux_order_inventory_request_items_order_product");
        builder.HasIndex(item => item.ProductId)
            .HasDatabaseName("ix_order_inventory_request_items_product_id");

        builder.HasOne(item => item.Order)
            .WithMany(order => order.InventoryRequestItems)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
