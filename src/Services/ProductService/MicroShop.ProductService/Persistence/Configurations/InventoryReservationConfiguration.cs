using MicroShop.ProductService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.ProductService.Persistence.Configurations;

public sealed class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
{
    public void Configure(EntityTypeBuilder<InventoryReservation> builder)
    {
        builder.ToTable("inventory_reservations", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_inventory_reservations_status_valid",
                "status IN ('reserved', 'released')");
            tableBuilder.HasCheckConstraint(
                "ck_inventory_reservations_currency_vnd",
                "currency = 'VND'");
            tableBuilder.HasCheckConstraint(
                "ck_inventory_reservations_total_nonnegative",
                "total_amount >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_inventory_reservations_release_timestamp_consistent",
                "(status = 'released' AND released_at_utc IS NOT NULL) OR (status = 'reserved' AND released_at_utc IS NULL)");
        });

        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(reservation => reservation.OrderId)
            .HasColumnName("order_id")
            .IsRequired();
        builder.Property(reservation => reservation.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(reservation => reservation.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(reservation => reservation.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(reservation => reservation.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(reservation => reservation.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(reservation => reservation.ReleasedAtUtc)
            .HasColumnName("released_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(reservation => reservation.OrderId)
            .IsUnique()
            .HasDatabaseName("ux_inventory_reservations_order_id");
        builder.HasIndex(reservation => new { reservation.Status, reservation.CreatedAtUtc })
            .HasDatabaseName("ix_inventory_reservations_status_created_at");

        builder.HasMany(reservation => reservation.Items)
            .WithOne(item => item.Reservation)
            .HasForeignKey(item => item.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
