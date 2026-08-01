using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.OrderService.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_order_items_product_name_not_blank",
                "length(btrim(product_name)) > 0");
            tableBuilder.HasCheckConstraint(
                "ck_order_items_unit_price_nonnegative",
                "unit_price >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_order_items_quantity_positive",
                "quantity > 0");
            tableBuilder.HasCheckConstraint(
                "ck_order_items_subtotal_nonnegative",
                "subtotal >= 0");
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

        builder.HasIndex(item => new { item.OrderId, item.ProductId })
            .HasDatabaseName("ux_order_items_order_product")
            .IsUnique();
    }
}
