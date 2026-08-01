using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.OrderService.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_orders_customer_name_not_blank",
                "length(btrim(customer_name)) > 0");
            tableBuilder.HasCheckConstraint(
                "ck_orders_status_valid",
                $"status IN ({OrderStatuses.ValidStatusSqlValues})");
            tableBuilder.HasCheckConstraint(
                "ck_orders_currency_vnd",
                "currency = 'VND'");
            tableBuilder.HasCheckConstraint(
                "ck_orders_total_nonnegative",
                "total_amount >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_orders_version_positive",
                "version > 0");
        });

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(order => order.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(150)
            .IsRequired();
        builder.Property(order => order.CustomerEmail)
            .HasColumnName("customer_email")
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(order => order.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(order => order.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(order => order.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(order => order.FailureCode)
            .HasColumnName("failure_code")
            .HasMaxLength(100);
        builder.Property(order => order.FailureDetail)
            .HasColumnName("failure_detail")
            .HasMaxLength(1_000);
        builder.Property(order => order.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(order => order.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(order => order.ConfirmedAtUtc)
            .HasColumnName("confirmed_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(order => order.CancelledAtUtc)
            .HasColumnName("cancelled_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(order => order.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(order => new { order.CreatedAtUtc, order.Id })
            .HasDatabaseName("ix_orders_created_at_id")
            .IsDescending();
        builder.HasIndex(order => new { order.CustomerEmail, order.CreatedAtUtc })
            .HasDatabaseName("ix_orders_customer_email_created_at");
        builder.HasIndex(order => new { order.Status, order.UpdatedAtUtc })
            .HasDatabaseName("ix_orders_status_updated_at");

        builder.HasMany(order => order.Items)
            .WithOne(item => item.Order)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(order => order.StateHistory)
            .WithOne(history => history.Order)
            .HasForeignKey(history => history.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
