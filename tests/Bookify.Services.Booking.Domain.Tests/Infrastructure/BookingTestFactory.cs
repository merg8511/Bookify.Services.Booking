using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;
using DomainBookingStatus = Bookify.Services.Booking.Domain.Bookings.BookingStatus;

namespace Bookify.Services.Booking.Domain.Tests.Infrastructure;

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
            throw new InvalidOperationException($"Failed to approve booking: {result.Error.Code}");
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
            throw new InvalidOperationException($"Failed to mark booking as paid: {result.Error.Code}");
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
            throw new InvalidOperationException($"Failed to complete booking: {result.Error.Code}");
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
            throw new InvalidOperationException($"Failed to reject booking: {result.Error.Code}");
        }

        return booking;
    }

    public static DomainBooking CreateBookingWithStatus(
        DomainBookingStatus status,
        RentableUnit? rentableUnit = null,
        StayPeriod? stayPeriod = null)
    {
        return status switch
        {
            DomainBookingStatus.PendingApproval => CreateValidBooking(rentableUnit, stayPeriod),
            DomainBookingStatus.PendingPayment => CreatePendingPaymentBooking(rentableUnit, stayPeriod),
            DomainBookingStatus.Paid => CreatePaidBooking(rentableUnit, stayPeriod),
            DomainBookingStatus.Completed => CreateCompletedBooking(rentableUnit, stayPeriod),
            DomainBookingStatus.Cancelled => CreateCancelledBooking(rentableUnit, stayPeriod),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported booking status.")
        };
    }
}
