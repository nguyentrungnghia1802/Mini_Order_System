using MicroShop.OrderService.Domain;
using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.OrderService.Persistence.Configurations;

public sealed class OrderStateHistoryConfiguration : IEntityTypeConfiguration<OrderStateHistory>
{
    public void Configure(EntityTypeBuilder<OrderStateHistory> builder)
    {
        builder.ToTable("order_state_history", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_order_state_history_from_status_valid",
                $"from_status IS NULL OR from_status IN ({OrderStatuses.ValidStatusSqlValues})");
            tableBuilder.HasCheckConstraint(
                "ck_order_state_history_to_status_valid",
                $"to_status IN ({OrderStatuses.ValidStatusSqlValues})");
        });

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(history => history.OrderId)
            .HasColumnName("order_id")
            .IsRequired();
        builder.Property(history => history.FromStatus)
            .HasColumnName("from_status")
            .HasMaxLength(32);
        builder.Property(history => history.ToStatus)
            .HasColumnName("to_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(history => history.ReasonCode)
            .HasColumnName("reason_code")
            .HasMaxLength(100);
        builder.Property(history => history.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(128);
        builder.Property(history => history.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(history => new { history.OrderId, history.OccurredAtUtc, history.Id })
            .HasDatabaseName("ix_order_state_history_order_time");
    }
}
