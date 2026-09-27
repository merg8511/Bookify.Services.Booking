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
    public void Create_ShouldAssignInitialLifecycleMetadata()
    {
        DomainBooking booking =
            CreateBooking();

        Assert.NotNull(
            booking.Reference);

        Assert.False(
            string.IsNullOrWhiteSpace(
                booking.Reference.Value));

        Assert.Equal(
            BookingTestTime.CreatedAtUtc,
            booking.CreatedAtUtc);

        Assert.Equal(
            BookingTestTime.ApprovalDueAtUtc,
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
    public void Approve_ShouldSetApprovalAndPaymentMetadata()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.PaymentDueAtUtc);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            BookingTestTime.ApprovedAtUtc,
            booking.ApprovedAtUtc);

        Assert.Equal(
            BookingTestTime.PaymentDueAtUtc,
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
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.Reject(
                BookingTestTime.CancelledAtUtc);

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
            booking.PaymentDueAtUtc);

        Assert.Null(
            booking.PaidAtUtc);

        Assert.Null(
            booking.CompletedAtUtc);
    }

    [Fact]
    public void Cancel_FromPendingApproval_ShouldSetCancelledAtUtc()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

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
    public void Cancel_FromPendingPayment_ShouldPreserveApprovalMetadataAndSetCancelledAtUtc()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result result =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingTestTime.ApprovedAtUtc,
            booking.ApprovedAtUtc);

        Assert.Equal(
            BookingTestTime.PaymentDueAtUtc,
            booking.PaymentDueAtUtc);

        Assert.Equal(
            BookingTestTime.CancelledAtUtc,
            booking.CancelledAtUtc);
    }

    [Fact]
    public void ExpirePayment_ShouldSetCancelledAtUtc()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result result =
            booking.ExpirePayment(
                BookingTestTime.CancelledAtUtc);

        Assert.True(
            result.IsSuccess);

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
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result result =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            BookingTestTime.ApprovedAtUtc,
            booking.ApprovedAtUtc);

        Assert.Equal(
            BookingTestTime.PaymentDueAtUtc,
            booking.PaymentDueAtUtc);

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
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Assert.True(
            booking.MarkAsPaid(
                    BookingTestTime.PaidAtUtc)
                .IsSuccess);

        Result result =
            booking.Complete(
                BookingTestTime.CompletedAtUtc);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);

        Assert.Equal(
            BookingTestTime.CreatedAtUtc,
            booking.CreatedAtUtc);

        Assert.Equal(
            BookingTestTime.ApprovalDueAtUtc,
            booking.ApprovalDueAtUtc);

        Assert.Equal(
            BookingTestTime.ApprovedAtUtc,
            booking.ApprovedAtUtc);

        Assert.Equal(
            BookingTestTime.PaymentDueAtUtc,
            booking.PaymentDueAtUtc);

        Assert.Equal(
            BookingTestTime.PaidAtUtc,
            booking.PaidAtUtc);

        Assert.Equal(
            BookingTestTime.CompletedAtUtc,
            booking.CompletedAtUtc);

        Assert.Null(
            booking.CancelledAtUtc);
    }

    private static DomainBooking CreatePendingPaymentBooking()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.PaymentDueAtUtc);

        Assert.True(
            result.IsSuccess);

        return booking;
    }

    private static DomainBooking CreateBooking()
    {
        return DomainBooking.Create(
                CreateRentableUnit(),
                CreateStayPeriod(),
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc)
            .Value;
    }

    private static RentableUnit CreateRentableUnit()
    {
        return RentableUnit.Create(
                Guid.NewGuid(),
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;
    }

    private static StayPeriod CreateStayPeriod()
    {
        return StayPeriod.Create(
                new DateOnly(
                    2026,
                    10,
                    10),
                new DateOnly(
                    2026,
                    10,
                    12))
            .Value;
    }
}
