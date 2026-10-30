namespace Bookify.Services.Booking.Infrastructure.DemoSeeding;

using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

public sealed class DemoPaymentConsistencyValidator
{
    private readonly BookingDbContext _dbContext;

    public DemoPaymentConsistencyValidator(BookingDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool bookingSeedWasApplied = await _dbContext
            .DemoSeedBatches
            .AsNoTracking()
            .AnyAsync(record => record.Key == BookingStatesDemoSeedBatch.BatchKey, cancellationToken);

        if (!bookingSeedWasApplied)
        {
            return;
        }

        DomainBooking[] bookings = await _dbContext
            .Bookings
            .AsNoTracking()
            .Where(booking => booking.GuestDetails != null &&
                (
                    booking.GuestDetails.Email == BookingStatesDemoSeedBatch.PaidDemoEmail ||
                    booking.GuestDetails.Email == BookingStatesDemoSeedBatch.CompletedDemoEmail
                ))
            .ToArrayAsync(cancellationToken);

        if (bookings.Length != 2)
        {
            throw new InvalidOperationException(
                "Demo payment consistency failed: the two paid seed bookings were not found.");
        }

        DomainBooking paidBooking = GetBookingByEmail(bookings, BookingStatesDemoSeedBatch.PaidDemoEmail);
        DomainBooking completedBooking = GetBookingByEmail(bookings, BookingStatesDemoSeedBatch.CompletedDemoEmail);

        Guid[] bookingIds =
            [
                paidBooking.Id,
                completedBooking.Id
            ];

        Payment[] payments = await _dbContext
            .Payments
            .AsNoTracking()
            .Where(payment => bookingIds.Contains(payment.BookingId))
            .Include(payment => payment.Attempts)
            .ToArrayAsync(cancellationToken);

        if (payments.Length != 2)
        {
            throw new InvalidOperationException(
                "Demo payment consistency failed: each paid seed booking must have a Payment.");
        }

        ValidateBookingPayment(
            paidBooking,
            payments,
            scenario: "paid",
            allowPaidStatus: true);

        ValidateBookingPayment(
            completedBooking,
            payments,
            scenario: "completed",
            allowPaidStatus: false);
    }

    private static DomainBooking GetBookingByEmail(
        IReadOnlyCollection<DomainBooking> bookings,
        string email)
    {
        DomainBooking[] matches = bookings
            .Where(booking => string.Equals(booking.GuestDetails?.Email, email, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Demo payment consistency failed: expected exactly one booking for '{email}'.");
        }

        return matches[0];
    }

    private static void ValidateBookingPayment(
        DomainBooking booking,
        IReadOnlyCollection<Payment> payments,
        string scenario,
        bool allowPaidStatus)
    {
        bool validStatus = allowPaidStatus
            ? booking.Status is BookingStatus.Paid or BookingStatus.Completed
            : booking.Status == BookingStatus.Completed;

        if (!validStatus)
        {
            throw ConsistencyError(scenario, "booking status is incompatible with a succeeded payment");
        }

        if (booking.PriceSnapshot is null)
        {
            throw ConsistencyError(scenario, "booking price snapshot is missing");
        }

        if (booking.PaidAtUtc is null)
        {
            throw ConsistencyError(scenario, "booking paid timestamp is missing");
        }

        if (booking.ApprovedAtUtc is null ||
           booking.ApprovedAtUtc > booking.PaidAtUtc)
        {
            throw ConsistencyError(scenario, "booking approval and payment chronology is inconsistent");
        }

        if (booking.Status == BookingStatus.Completed &&
           (booking.CompletedAtUtc is null ||
           booking.CompletedAtUtc < booking.PaidAtUtc))
        {
            throw ConsistencyError(scenario, "booking completion chronology is invalid");
        }

        Payment[] matchedPayments = payments
            .Where(payment => payment.BookingId == booking.Id)
            .ToArray();

        if (matchedPayments.Length != 1)
        {
            throw ConsistencyError(scenario, "booking must have exactly one Payment");
        }

        Payment payment = matchedPayments[0];

        if (payment.Status != PaymentStatus.Succeeded)
        {
            throw ConsistencyError(scenario, "payment status must be Succeeded");
        }

        if (payment.CompletedAtUtc is null ||
            payment.CompletedAtUtc != booking.PaidAtUtc ||
            payment.UpdatedAtUtc != payment.CompletedAtUtc)
        {
            throw ConsistencyError(scenario, "payment confirmation timestamps do not match the booking");
        }

        if (payment.PendingInitiationKey is not null ||
            payment.PendingInitiationCreatedAtUtc is not null)
        {
            throw ConsistencyError(scenario, "succeeded payment has an unresolved initiation");
        }

        if (payment.Amount.Amount <= 0 ||
            payment.Amount.Amount != booking.PriceSnapshot.TotalPrice.Amount ||
            !string.Equals(payment.Amount.Currency, booking.PriceSnapshot.TotalPrice.Currency, StringComparison.Ordinal))
        {
            throw ConsistencyError(scenario, "payment amount or currency does not match the booking snapshot");
        }

        PaymentAttempt[] attempts = payment.Attempts.ToArray();

        if (attempts.Length != 1)
        {
            throw ConsistencyError(scenario, "demo Payment must contain exactly one PaymentAttempt");
        }

        PaymentAttempt attempt = attempts[0];

        if (attempt.PaymentId != payment.Id ||
            attempt.Status != PaymentAttemptStatus.Succeeded)
        {
            throw ConsistencyError(scenario, "payment attempt is not correctly linked or succeded");
        }

        if (attempt.Amount.Amount != payment.Amount.Amount ||
            !string.Equals(attempt.Amount.Currency, payment.Amount.Currency, StringComparison.Ordinal))
        {
            throw ConsistencyError(scenario, "payment attempt amount or currency does not match its Payment");
        }

        if (attempt.CompletedAtUtc is null ||
            attempt.CompletedAtUtc != payment.CompletedAtUtc)
        {
            throw ConsistencyError(scenario, "payment attempt completion timestamp does not match its Payment");
        }

        if (payment.CreatedAtUtc > attempt.CreatedAtUtc ||
            attempt.CreatedAtUtc > attempt.CompletedAtUtc.Value)
        {
            throw ConsistencyError(scenario, "payment attempt chronology is invalid");
        }

        string expectedIdempotencyKey = $"bookify-demo-23.3-{scenario}-v1";
        string expectedExternalReference = $"fake-demo-23.3-{scenario}-v1";

        if (!string.Equals(attempt.IdempotencyKey, expectedIdempotencyKey, StringComparison.Ordinal) ||
            !string.Equals(attempt.ExternalReference, expectedExternalReference, StringComparison.Ordinal))
        {
            throw ConsistencyError(scenario, "payment attempt does not match the expected demo scenario");
        }
    }

    private static InvalidOperationException ConsistencyError(string scenario, string description)
    {
        return new InvalidOperationException(
            $"Demo payment consistency failed for scenario '{scenario}': {description}");
    }
}
