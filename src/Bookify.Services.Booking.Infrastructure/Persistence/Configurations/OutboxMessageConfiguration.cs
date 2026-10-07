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
            .HasIndex(message => new
            {
                message.CreatedAtUtc,
                message.Id
            })
            .HasDatabaseName("ix_outbox_messages_pending_created_at_utc")
            .HasFilter("processed_at_utc IS NULL");
    }
}
