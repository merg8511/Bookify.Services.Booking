using Bookify.Services.Booking.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .ToTable("outbox_messages");

        builder
            .HasKey(message => message.Id)
            .HasName("pk_outbox_messages");

        builder
            .Property(message => message.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder
            .Property(message => message.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(128)
            .IsRequired();

        builder
            .Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder
            .Property(message => message.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .Property(message => message.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamp with time zone");

        builder
            .Property(message => message.AttemptCount)
            .HasColumnName("attempt_count")
            .HasColumnType("integer")
            .HasDefaultValue(0)
            .IsRequired();

        builder
            .Property(message => message.NextAttemptAtUtc)
            .HasColumnName("next_attempt_at_utc")
            .HasColumnType("timestamp with time zone");

        builder
            .Property(message => message.FailedAtUtc)
            .HasColumnName("failed_at_utc")
            .HasColumnType("timestamp with time zone");

        builder
            .Property(message => message.LastFailureCode)
            .HasColumnName("last_failure_code")
            .HasMaxLength(128);

        builder
            .HasIndex(message => new
            {
                message.CreatedAtUtc,
                message.Id
            })
            .HasDatabaseName("ix_outbox_messages_pending_created_at_utc")
            .HasFilter("processed_at_utc IS NULL AND failed_at_utc IS NULL");

        builder
            .HasIndex(message => new
            {
                message.NextAttemptAtUtc,
                message.CreatedAtUtc,
                message.Id
            })
            .HasDatabaseName("ix_outbox_message_due")
            .HasFilter("processed_at_utc IS NULL AND failed_at_utc IS NULL");
    }
}
