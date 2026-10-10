using Bookify.Services.Booking.Infrastructure.Persistence.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{

    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .ToTable("notification_deliveries");

        builder
            .HasKey(delivery => new { delivery.BookingId, delivery.NotificationType })
            .HasName("pk_notification_deliveries");

        builder
            .Property(delivery => delivery.BookingId)
            .HasColumnName("booking_id")
            .HasColumnType("uuid");

        builder
            .Property(delivery => delivery.NotificationType)
            .HasColumnName("notification_type")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder
            .Property(delivery => delivery.DeliveredAtUtc)
            .HasColumnName("delivered_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
