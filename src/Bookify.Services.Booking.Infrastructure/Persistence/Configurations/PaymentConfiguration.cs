using Bookify.Services.Booking.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("payments",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_payments_status",
                    "status IN ('Pending', 'Succeeded', 'Failed', 'Cancelled')");

                tableBuilder.HasCheckConstraint(
                    "ck_payments_amount", "amount > 0");

                tableBuilder.HasCheckConstraint(
                    "ck_payments_completion_consistency",
                    "(status IN ('Succeeded', 'Cancelled') " +
                    "AND completed_at_utc IS NOT NULL) OR " +
                    "(status IN ('Pending', 'Failed') " +
                    "AND completed_at_utc IS NULL)");
            });

        builder
            .HasKey(payment => payment.Id);

        builder
            .Property(payment => payment.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder
            .Property(payment => payment.BookingId)
            .HasColumnName("booking_id");

        builder
            .Property(payment => payment.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder
            .Property(payment => payment.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder
            .Property(payment => payment.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder
            .Property(payment => payment.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder
            .OwnsOne(payment => payment.Amount,
                moneyBuilder =>
                {
                    moneyBuilder
                        .Property(money => money.Amount)
                        .HasColumnName("amount")
                        .HasPrecision(18, 3)
                        .IsRequired();

                    moneyBuilder
                        .Property(money => money.Currency)
                        .HasColumnName("currency")
                        .HasMaxLength(3)
                        .IsFixedLength()
                        .IsRequired();
                });

        builder
            .HasIndex(payment => payment.BookingId)
            .IsUnique();

        builder
            .HasOne<DomainBooking>()
            .WithOne()
            .HasForeignKey<Payment>(payment => payment.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(payment => payment.Attempts)
            .WithOne()
            .HasForeignKey(attempt => attempt.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Navigation(payment => payment.Attempts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
