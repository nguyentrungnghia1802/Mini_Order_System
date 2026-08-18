using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.OrderService.Persistence.Configurations;

public sealed class OrderReconciliationAuditConfiguration
    : IEntityTypeConfiguration<OrderReconciliationAudit>
{
    public void Configure(EntityTypeBuilder<OrderReconciliationAudit> builder)
    {
        builder.ToTable("order_reconciliation_audits", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_order_reconciliation_audits_operation_valid",
                "operation IN ('inventory', 'cancellation')");
        });

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(audit => audit.OrderId)
            .HasColumnName("order_id")
            .IsRequired();
        builder.Property(audit => audit.Operation)
            .HasColumnName("operation")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(audit => audit.FromStatus)
            .HasColumnName("from_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(audit => audit.ToStatus)
            .HasColumnName("to_status")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(audit => audit.ReservationStatus)
            .HasColumnName("reservation_status")
            .HasMaxLength(32);
        builder.Property(audit => audit.ReservationId)
            .HasColumnName("reservation_id");
        builder.Property(audit => audit.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(audit => audit.Detail)
            .HasColumnName("detail")
            .HasMaxLength(1_000);
        builder.Property(audit => audit.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(128);
        builder.Property(audit => audit.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(audit => new { audit.OrderId, audit.OccurredAtUtc, audit.Id })
            .HasDatabaseName("ix_order_reconciliation_audits_order_time");

        builder.HasOne(audit => audit.Order)
            .WithMany(order => order.ReconciliationAudits)
            .HasForeignKey(audit => audit.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
