using MicroShop.NotificationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.NotificationService.Persistence.Configurations;

public sealed class ConsumedMessageConfiguration : IEntityTypeConfiguration<ConsumedMessage>
{
    public void Configure(EntityTypeBuilder<ConsumedMessage> builder)
    {
        builder.ToTable("consumed_messages");
        builder.HasKey(message => message.MessageId);
        builder.Property(message => message.MessageId)
            .HasColumnName("message_id")
            .ValueGeneratedNever();
        builder.Property(message => message.MessageType)
            .HasColumnName("message_type")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(message => message.Consumer)
            .HasColumnName("consumer")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(message => message.ConsumedAtUtc)
            .HasColumnName("consumed_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(message => message.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(200);
        builder.HasIndex(message => new { message.Consumer, message.ConsumedAtUtc })
            .HasDatabaseName("ix_consumed_messages_consumer_consumed_at");
        builder.HasOne(message => message.Notification)
            .WithOne(notification => notification.ConsumedMessage)
            .HasForeignKey<Notification>(notification => notification.SourceMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
