using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingLifecycleMetadataTests
{
    [Fact]
    public void Create_ShouldAssignReferenceAndCreatedAtUtc()
    {
        // ACT
        DomainBooking booking =
            CreateBooking();

        // ASSERT
        Assert.NotNull(
            booking.Reference);

        Assert.False(
            string.IsNullOrWhiteSpace(
                booking.Reference.Value));

        Assert.Equal(
            BookingTestTime.CreatedAtUtc,
            booking.CreatedAtUtc);

        Assert.Null(
            booking.ApprovalDueAtUtc);

        Assert.Null(
            booking.ApprovedAtUtc);

        Assert.Null(
            booking.PaymentDueAtUtc);

        Assert.Null(
            booking.PaidAtUtc);

        Assert.Null(
            booking.CancelledAtUtc);

        Assert.Null(
            booking.CompletedAtUtc);
    }

    [Fact]
    public void Approve_ShouldSetApprovedAtUtc()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        // ACT
        Result result =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            BookingTestTime.ApprovedAtUtc,
            booking.ApprovedAtUtc);

        Assert.Null(
            booking.PaymentDueAtUtc);

        Assert.Null(
            booking.PaidAtUtc);

        Assert.Null(
            booking.CancelledAtUtc);

        Assert.Null(
            booking.CompletedAtUtc);
    }

    [Fact]
    public void Reject_ShouldSetCancelledAtUtc()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        // ACT
        Result result =
            booking.Reject(
                BookingTestTime.CancelledAtUtc);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.RejectedByOwner,
            booking.CancellationReason);

        Assert.Equal(
            BookingTestTime.CancelledAtUtc,
            booking.CancelledAtUtc);

        Assert.Null(
            booking.ApprovedAtUtc);

        Assert.Null(
            booking.PaidAtUtc);

        Assert.Null(
            booking.CompletedAtUtc);
    }

    [Fact]
    public void Cancel_FromPendingApproval_ShouldSetCancelledAtUtc()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        // ACT
        Result result =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            booking.CancellationReason);

        Assert.Equal(
            BookingTestTime.CancelledAtUtc,
            booking.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_FromPendingPayment_ShouldSetCancelledAtUtc()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        Result approvalResult =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc);

        Assert.True(
            approvalResult.IsSuccess);

        // ACT
        Result cancellationResult =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

        // ASSERT
        Assert.True(
            cancellationResult.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            booking.CancellationReason);

        Assert.Equal(
            BookingTestTime.ApprovedAtUtc,
            booking.ApprovedAtUtc);

        Assert.Equal(
            BookingTestTime.CancelledAtUtc,
            booking.CancelledAtUtc);
    }

    [Fact]
    public void ExpirePayment_ShouldSetCancelledAtUtc()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        Result approvalResult =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc);

        Assert.True(
            approvalResult.IsSuccess);

        // ACT
        Result expirationResult =
            booking.ExpirePayment(
                BookingTestTime.CancelledAtUtc);

        // ASSERT
        Assert.True(
            expirationResult.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.PaymentExpired,
            booking.CancellationReason);

        Assert.Equal(
            BookingTestTime.CancelledAtUtc,
            booking.CancelledAtUtc);
    }

    [Fact]
    public void MarkAsPaid_ShouldSetPaidAtUtc()
    {
        // ARRANGE
        DomainBooking booking =
            CreatePendingPaymentBooking();

        // ACT
        Result result =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            BookingTestTime.ApprovedAtUtc,
            booking.ApprovedAtUtc);

        Assert.Equal(
            BookingTestTime.PaidAtUtc,
            booking.PaidAtUtc);

        Assert.Null(
            booking.CancelledAtUtc);

        Assert.Null(
            booking.CompletedAtUtc);
    }

    [Fact]
    public void Complete_ShouldSetCompletedAtUtc()
    {
        // ARRANGE
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result paymentResult =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        Assert.True(
            paymentResult.IsSuccess);

        // ACT
        Result completionResult =
            booking.Complete(
                BookingTestTime.CompletedAtUtc);

        // ASSERT
        Assert.True(
            completionResult.IsSuccess);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);

        Assert.Equal(
            BookingTestTime.CreatedAtUtc,
            booking.CreatedAtUtc);

        Assert.Equal(
            BookingTestTime.ApprovedAtUtc,
            booking.ApprovedAtUtc);

        Assert.Equal(
            BookingTestTime.PaidAtUtc,
            booking.PaidAtUtc);

        Assert.Equal(
            BookingTestTime.CompletedAtUtc,
            booking.CompletedAtUtc);

        Assert.Null(
            booking.CancelledAtUtc);
    }

    private static DomainBooking
        CreatePendingPaymentBooking()
    {
        DomainBooking booking =
            CreateBooking();

        Result approvalResult =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc);

        Assert.True(
            approvalResult.IsSuccess);

        return booking;
    }

    private static DomainBooking CreateBooking()
    {
        RentableUnit rentableUnit =
            RentableUnit.Create(
                    Guid.NewGuid(),
                    "Room A",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        10,
                        10),
                    new DateOnly(
                        2026,
                        10,
                        12))
                .Value;

        GuestCount guestCount =
            GuestCount.Create(
                    2)
                .Value;

        GuestDetails guestDetails =
            GuestDetails.Create(
                    "John Doe",
                    "john@example.com",
                    "+50377778888")
                .Value;

        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount,
                guestDetails,
                BookingTestTime.CreatedAtUtc);

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }
}
