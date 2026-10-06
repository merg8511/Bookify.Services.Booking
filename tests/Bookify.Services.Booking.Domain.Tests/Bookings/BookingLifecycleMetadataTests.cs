using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingLifecycleMetadataTests
{
    [Fact]
    public void Create_ShouldAssignInitialLifecycleMetadata()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();

        Assert.NotNull(booking.Reference);
        Assert.False(string.IsNullOrWhiteSpace(booking.Reference.Value));
        Assert.Equal(BookingTestTime.CreatedAtUtc, booking.CreatedAtUtc);
        Assert.Equal(BookingTestTime.ApprovalDueAtUtc, booking.ApprovalDueAtUtc);
        Assert.Null(booking.ApprovedAtUtc);
        Assert.Null(booking.PaymentDueAtUtc);
        Assert.Null(booking.PaidAtUtc);
        Assert.Null(booking.CancelledAtUtc);
        Assert.Null(booking.CompletedAtUtc);
    }

    [Fact]
    public void Approve_ShouldSetApprovalAndPaymentMetadata()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();

        Result result = booking.Approve(
            BookingTestTime.ApprovedAtUtc,
            BookingTestTime.PaymentDueAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(BookingTestTime.ApprovedAtUtc, booking.ApprovedAtUtc);
        Assert.Equal(BookingTestTime.PaymentDueAtUtc, booking.PaymentDueAtUtc);
        Assert.Null(booking.PaidAtUtc);
        Assert.Null(booking.CancelledAtUtc);
        Assert.Null(booking.CompletedAtUtc);
    }

    [Fact]
    public void Reject_ShouldSetCancelledAtUtc()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();

        Result result = booking.Reject(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(BookingCancellationReason.RejectedByOwner, booking.CancellationReason);
        Assert.Equal(BookingTestTime.CancelledAtUtc, booking.CancelledAtUtc);
        Assert.Null(booking.ApprovedAtUtc);
        Assert.Null(booking.PaymentDueAtUtc);
        Assert.Null(booking.PaidAtUtc);
        Assert.Null(booking.CompletedAtUtc);
    }

    [Fact]
    public void Cancel_FromPendingApproval_ShouldSetCancelledAtUtc()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();

        Result result = booking.Cancel(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(BookingCancellationReason.CancelledByGuest, booking.CancellationReason);
        Assert.Equal(BookingTestTime.CancelledAtUtc, booking.CancelledAtUtc);
    }

    [Fact]
    public void Cancel_FromPendingPayment_ShouldPreserveApprovalMetadataAndSetCancelledAtUtc()
    {
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();

        Result result = booking.Cancel(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(BookingTestTime.ApprovedAtUtc, booking.ApprovedAtUtc);
        Assert.Equal(BookingTestTime.PaymentDueAtUtc, booking.PaymentDueAtUtc);
        Assert.Equal(BookingTestTime.CancelledAtUtc, booking.CancelledAtUtc);
    }

    [Fact]
    public void ExpirePayment_ShouldSetCancelledAtUtc()
    {
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();

        Result result = booking.ExpirePayment(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(BookingCancellationReason.PaymentExpired, booking.CancellationReason);
        Assert.Equal(BookingTestTime.CancelledAtUtc, booking.CancelledAtUtc);
    }

    [Fact]
    public void MarkAsPaid_ShouldSetPaidAtUtc()
    {
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();

        Result result = booking.MarkAsPaid(BookingTestTime.PaidAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Paid, booking.Status);
        Assert.Equal(BookingTestTime.ApprovedAtUtc, booking.ApprovedAtUtc);
        Assert.Equal(BookingTestTime.PaymentDueAtUtc, booking.PaymentDueAtUtc);
        Assert.Equal(BookingTestTime.PaidAtUtc, booking.PaidAtUtc);
        Assert.Null(booking.CancelledAtUtc);
        Assert.Null(booking.CompletedAtUtc);
    }

    [Fact]
    public void Complete_ShouldSetCompletedAtUtc()
    {
        DomainBooking booking = BookingTestFactory.CreatePaidBooking();

        Result result = booking.Complete(BookingTestTime.CompletedAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.Equal(BookingTestTime.CreatedAtUtc, booking.CreatedAtUtc);
        Assert.Equal(BookingTestTime.ApprovalDueAtUtc, booking.ApprovalDueAtUtc);
        Assert.Equal(BookingTestTime.ApprovedAtUtc, booking.ApprovedAtUtc);
        Assert.Equal(BookingTestTime.PaymentDueAtUtc, booking.PaymentDueAtUtc);
        Assert.Equal(BookingTestTime.PaidAtUtc, booking.PaidAtUtc);
        Assert.Equal(BookingTestTime.CompletedAtUtc, booking.CompletedAtUtc);
        Assert.Null(booking.CancelledAtUtc);
    }
}
