using Bookify.Services.Booking.Infrastructure.Persistence.Payments.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("payment_webhook_events");

        builder.HasKey(webhookEvent => webhookEvent.Id);

        builder
            .Property(webhookEvent => webhookEvent.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder
            .Property(webhookEvent => webhookEvent.Provider)
            .HasColumnName("provider")
            .HasColumnType("character varying(32)")
            .HasMaxLength(32)
            .IsRequired();

        builder
            .Property(webhookEvent => webhookEvent.EventId)
            .HasColumnName("event_id")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(webhookEvent => webhookEvent.EventType)
            .HasColumnName("event_type")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(webhookEvent => webhookEvent.ReceivedAtUtc)
            .HasColumnName("received_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .Property(webhookEvent => webhookEvent.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamp with time zone");

        builder
            .Property(webhookEvent => webhookEvent.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasColumnType("character varying(32)")
            .HasMaxLength(32)
            .HasConversion<string>()
            .IsRequired();

        builder
            .Property(webhookEvent => webhookEvent.ErrorCode)
            .HasColumnName("error_code")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200);

        builder
            .Property(webhookEvent => webhookEvent.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder
            .HasIndex(webhookEvent => webhookEvent.EventId)
            .IsUnique()
            .HasDatabaseName("ux_payment_webhook_events_event_id");

        builder.ToTable(
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_payment_webhook_events_status",
                    """
                    processing_status IN
                    (
                        'Processing',
                        'Processed',
                        'Failed'
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    "ck_payment_webhook_events_processed_time",
                    """
                    processed_at_utc IS NULL
                    OR
                    processed_at_utc >= received_at_utc
                    """);

                tableBuilder.HasCheckConstraint(
                    "ck_payment_webhook_events_state",
                    """
                    (
                        processing_status = 'Processing'
                        AND processed_at_utc IS NULL
                        AND error_code IS NULL
                        AND error_message IS NULL
                    )
                    OR
                    (
                        processing_status = 'Processed'
                        AND processed_at_utc IS NOT NULL
                        AND error_code IS NULL
                        AND error_message IS NULL
                    )
                    OR
                    (
                        processing_status = 'Failed'
                        AND processed_at_utc IS NULL
                        AND error_code IS NOT NULL
                        AND error_message IS NOT NULL
                    )
                    """);
            });
    }
}
