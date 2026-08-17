using MicroShop.NotificationService.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroShop.NotificationService.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_notifications_customer_email_not_blank",
                "length(btrim(customer_email)) > 0");
            tableBuilder.HasCheckConstraint(
                "ck_notifications_subject_not_blank",
                "length(btrim(subject)) > 0");
            tableBuilder.HasCheckConstraint(
                "ck_notifications_body_not_blank",
                "length(btrim(body)) > 0");
            tableBuilder.HasCheckConstraint(
                "ck_notifications_total_nonnegative",
                "total_amount >= 0");
            tableBuilder.HasCheckConstraint(
                "ck_notifications_currency_length",
                "length(currency) = 3");
        });

        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(notification => notification.SourceMessageId)
            .HasColumnName("source_message_id")
            .IsRequired();
        builder.Property(notification => notification.OrderId)
            .HasColumnName("order_id")
            .IsRequired();
        builder.Property(notification => notification.CustomerEmail)
            .HasColumnName("customer_email")
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(notification => notification.Subject)
            .HasColumnName("subject")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(notification => notification.Body)
            .HasColumnName("body")
            .HasMaxLength(2_000)
            .IsRequired();
        builder.Property(notification => notification.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();
        builder.Property(notification => notification.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();
        builder.Property(notification => notification.IsRead)
            .HasColumnName("is_read")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(notification => notification.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(notification => notification.ReadAtUtc)
            .HasColumnName("read_at_utc")
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(notification => notification.SourceMessageId)
            .IsUnique()
            .HasDatabaseName("ux_notifications_source_message_id");
        builder.HasIndex(notification => new { notification.CustomerEmail, notification.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_notifications_customer_email_created_at");
        builder.HasIndex(notification => notification.OrderId)
            .HasDatabaseName("ix_notifications_order_id");
    }
}
