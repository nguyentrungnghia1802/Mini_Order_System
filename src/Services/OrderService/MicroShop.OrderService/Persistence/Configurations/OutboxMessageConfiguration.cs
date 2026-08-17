using MicroShop.OrderService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.OrderService.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_outbox_messages_attempt_count_nonnegative",
                "attempt_count >= 0");
        });

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(message => message.MessageType)
            .HasColumnName("message_type")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(message => message.AggregateId)
            .HasColumnName("aggregate_id")
            .IsRequired();
        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(message => message.TraceParent)
            .HasColumnName("trace_parent")
            .HasMaxLength(256);
        builder.Property(message => message.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(message => message.AttemptCount)
            .HasColumnName("attempt_count")
            .IsRequired();
        builder.Property(message => message.NextAttemptAtUtc)
            .HasColumnName("next_attempt_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(message => message.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(4_000);
        builder.Property(message => message.LockedBy)
            .HasColumnName("locked_by")
            .HasMaxLength(128);
        builder.Property(message => message.LockedUntilUtc)
            .HasColumnName("locked_until_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(message => message.PublishedAtUtc)
            .HasColumnName("published_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.Property(message => message.DeadLetteredAtUtc)
            .HasColumnName("dead_lettered_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(message => new
        {
            message.MessageType,
            message.AggregateId
        })
        .HasDatabaseName("ux_outbox_messages_type_aggregate")
        .IsUnique();

        builder.HasIndex(message => new
        {
            message.PublishedAtUtc,
            message.DeadLetteredAtUtc,
            message.NextAttemptAtUtc,
            message.LockedUntilUtc,
            message.CreatedAtUtc
        })
        .HasDatabaseName("ix_outbox_messages_pending");
    }
}
