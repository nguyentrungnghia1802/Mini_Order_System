using MicroShop.ProductService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.ProductService.Persistence.Configurations;

public sealed class InventoryReservationItemConfiguration : IEntityTypeConfiguration<InventoryReservationItem>
{
    public void Configure(EntityTypeBuilder<InventoryReservationItem> builder)
    {
        builder.ToTable("inventory_reservation_items", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_inventory_reservation_items_status_valid",
                "status IN ('reserved', 'released')");
            tableBuilder.HasCheckConstraint(
                "ck_inventory_reservation_items_quantity_positive",
                "quantity > 0");
            tableBuilder.HasCheckConstraint(
                "ck_inventory_reservation_items_subtotal_nonnegative",
                "subtotal >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_inventory_reservation_items_unit_price_nonnegative",
                "unit_price >= 0");
        });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(item => item.ReservationId)
            .HasColumnName("reservation_id")
            .IsRequired();
        builder.Property(item => item.ProductId)
            .HasColumnName("product_id")
            .IsRequired();
        builder.Property(item => item.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(item => item.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();
        builder.Property(item => item.Subtotal)
            .HasColumnName("subtotal")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(item => new { item.ReservationId, item.ProductId })
            .IsUnique()
            .HasDatabaseName("ux_inventory_reservation_items_reservation_product");
        builder.HasIndex(item => item.ProductId)
            .HasDatabaseName("ix_inventory_reservation_items_product_id");

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
