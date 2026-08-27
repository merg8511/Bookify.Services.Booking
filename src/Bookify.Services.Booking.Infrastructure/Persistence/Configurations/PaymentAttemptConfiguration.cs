using Bookify.Services.Booking.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("payment_attempts",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_payment_attempts_status",
                    "status IN ('Pending', 'Succeeded', 'Failed', 'Cancelled')");

                tableBuilder.HasCheckConstraint(
                    "ck_payment_attempts_amount",
                    "amount > 0");

                tableBuilder.HasCheckConstraint(
                    "ck_payment_attempts_completion_consistency",
                    "(" +
                    "status = 'Pending' " +
                    "AND completed_at_utc IS NULL" +
                    ") OR (" +
                    "status IN ('Succeeded', 'Failed', 'Cancelled') " +
                    "AND completed_at_utc IS NOT NULL" +
                    ")");
            });

        builder
            .HasKey(attempt => attempt.Id);

        builder
            .Property(attempt => attempt.Id)
            .HasColumnName("id");

        builder
            .Property(attempt => attempt.PaymentId)
            .HasColumnName("payment_id");

        builder
            .Property(attempt => attempt.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128)
            .IsRequired();

        builder
            .Property(attempt => attempt.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(255)
            .IsRequired();

        builder
            .Property(attempt => attempt.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder
            .Property(attempt => attempt.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder
            .Property(attempt => attempt.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder
            .OwnsOne(attempt => attempt.Amount,
                moneyBuilder =>
                {
                    moneyBuilder
                        .Property(money => money.Amount)
                        .HasColumnName("amount")
                        .HasPrecision(18, 2)
                        .IsRequired();

                    moneyBuilder
                        .Property(money => money.Currency)
                        .HasColumnName("currency")
                        .HasMaxLength(3)
                        .IsFixedLength()
                        .IsRequired();
                });

        builder
            .HasIndex(attempt => attempt.IdempotencyKey)
            .IsUnique();

        builder
            .HasIndex(attempt => attempt.ExternalReference)
            .IsUnique();
    }
}
