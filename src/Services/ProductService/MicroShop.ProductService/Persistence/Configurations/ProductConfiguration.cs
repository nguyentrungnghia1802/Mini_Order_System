using MicroShop.ProductService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.ProductService.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_products_name_not_blank",
                "length(btrim(name)) > 0");
            tableBuilder.HasCheckConstraint(
                "ck_products_unit_price_nonnegative",
                "unit_price >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_products_available_stock_nonnegative",
                "available_stock >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_products_currency_vnd",
                "currency = 'VND'");
        });

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(product => product.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(product => product.Description)
            .HasColumnName("description")
            .HasMaxLength(2_000);
        builder.Property(product => product.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(product => product.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(product => product.AvailableStock)
            .HasColumnName("available_stock")
            .IsRequired();
        builder.Property(product => product.IsActive)
            .HasColumnName("is_active")
            .IsRequired();
        builder.Property(product => product.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(product => product.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(product => product.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(product => new { product.IsActive, product.Name, product.Id })
            .HasDatabaseName("ix_products_active_name_id");
    }
}
