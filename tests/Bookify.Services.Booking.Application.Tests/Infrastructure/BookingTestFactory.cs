using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Infrastructure;

internal static class BookingTestFactory
{
    public static DomainBooking CreateValidBooking(
        RentableUnit? rentableUnit = null,
        StayPeriod? stayPeriod = null,
        GuestCount? guestCount = null,
        GuestDetails? guestDetails = null,
        PriceSnapshot? priceSnapshot = null,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? approvalDueAtUtc = null,
        string? customerSubjectId = null)
    {
        rentableUnit ??= RentableUnitTestFactory.CreateValidRentableUnit();
        stayPeriod ??= BookingTestData.CreateStayPeriod();
        guestCount ??= BookingTestData.CreateGuestCount();
        guestDetails ??= BookingTestData.CreateGuestDetails();
        priceSnapshot ??= BookingTestData.CreatePriceSnapshot();
        createdAtUtc ??= BookingTestTime.CreatedAtUtc;
        approvalDueAtUtc ??= BookingTestTime.ApprovalDueAtUtc;

        Result<DomainBooking> result = DomainBooking.Create(
            rentableUnit,
            stayPeriod,
            guestCount,
            guestDetails,
            priceSnapshot,
            createdAtUtc.Value,
            approvalDueAtUtc.Value,
            customerSubjectId);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to create valid booking: {result.Error.Code} - {result.Error.Message}");
        }

        return result.Value;
    }

    public static DomainBooking CreatePendingPaymentBooking(
        RentableUnit? rentableUnit = null,
        StayPeriod? stayPeriod = null,
        DateTimeOffset? approvedAtUtc = null,
        DateTimeOffset? paymentDueAtUtc = null,
        string? customerSubjectId = null)
    {
        DomainBooking booking = CreateValidBooking(
            rentableUnit: rentableUnit,
            stayPeriod: stayPeriod,
            customerSubjectId: customerSubjectId);

        Result result = booking.Approve(
            approvedAtUtc ?? BookingTestTime.ApprovedAtUtc,
            paymentDueAtUtc ?? BookingTestTime.PaymentDueAtUtc);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to approve booking: {result.Error.Code} - {result.Error.Message}");
        }

        return booking;
    }

    public static DomainBooking CreatePaidBooking(
        RentableUnit? rentableUnit = null,
        StayPeriod? stayPeriod = null,
        DateTimeOffset? paidAtUtc = null,
        string? customerSubjectId = null)
    {
        DomainBooking booking = CreatePendingPaymentBooking(
            rentableUnit: rentableUnit,
            stayPeriod: stayPeriod,
            customerSubjectId: customerSubjectId);

        Result result = booking.MarkAsPaid(paidAtUtc ?? BookingTestTime.PaidAtUtc);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to mark booking as paid: {result.Error.Code} - {result.Error.Message}");
        }

        return booking;
    }

    public static DomainBooking CreateCompletedBooking(
        RentableUnit? rentableUnit = null,
        StayPeriod? stayPeriod = null,
        DateTimeOffset? completedAtUtc = null,
        string? customerSubjectId = null)
    {
        DomainBooking booking = CreatePaidBooking(
            rentableUnit: rentableUnit,
            stayPeriod: stayPeriod,
            customerSubjectId: customerSubjectId);

        Result result = booking.Complete(completedAtUtc ?? BookingTestTime.CompletedAtUtc);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to complete booking: {result.Error.Code} - {result.Error.Message}");
        }

        return booking;
    }

    public static DomainBooking CreateCancelledBooking(
        RentableUnit? rentableUnit = null,
        StayPeriod? stayPeriod = null,
        DateTimeOffset? cancelledAtUtc = null,
        string? customerSubjectId = null)
    {
        DomainBooking booking = CreateValidBooking(
            rentableUnit: rentableUnit,
            stayPeriod: stayPeriod,
            customerSubjectId: customerSubjectId);

        Result result = booking.Reject(cancelledAtUtc ?? BookingTestTime.CancelledAtUtc);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to reject booking: {result.Error.Code} - {result.Error.Message}");
        }

        return booking;
    }

    public static Payment CreatePayment(
        DomainBooking booking,
        decimal amount = 200m,
        string currency = "USD",
        DateTimeOffset? createdAtUtc = null)
    {
        Result<Payment> result = Payment.Create(
            booking.Id,
            Money.Create(amount, currency).Value,
            createdAtUtc ?? BookingTestTime.CreatedAtUtc);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to create payment: {result.Error.Code} - {result.Error.Message}");
        }

        return result.Value;
    }

    public static (Payment Payment, PaymentAttempt Attempt) CreatePendingPayment(
        DomainBooking booking,
        string externalReference = "ext-ref-001",
        decimal amount = 200m,
        string currency = "USD",
        DateTimeOffset? createdAtUtc = null)
    {
        Payment payment = CreatePayment(booking, amount, currency, createdAtUtc);

        Result<PaymentAttempt> attemptResult = payment.AddAttempt(
            $"operation-{Guid.NewGuid():N}",
            externalReference,
            (createdAtUtc ?? BookingTestTime.CreatedAtUtc).AddMinutes(1));

        if (attemptResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to add attempt: {attemptResult.Error.Code} - {attemptResult.Error.Message}");
        }

        return (payment, attemptResult.Value);
    }
}
