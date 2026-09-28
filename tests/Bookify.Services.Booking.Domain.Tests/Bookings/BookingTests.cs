using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.Errors;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingTests
{
    private static readonly Guid PropertyId =
        Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldReturnCompletePendingApprovalBooking()
    {
        // ARRANGE
        RentableUnit rentableUnit =
            CreateRentableUnit();

        StayPeriod stayPeriod =
            CreateStayPeriod();

        PriceSnapshot priceSnapshot =
            BookingTestData.CreatePriceSnapshot();

        // ACT
        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                priceSnapshot,
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        DomainBooking booking =
            result.Value;

        Assert.NotEqual(
            Guid.Empty,
            booking.Id);

        Assert.Equal(
            rentableUnit.PropertyId,
            booking.PropertyId);

        Assert.Equal(
            rentableUnit.Id,
            booking.RentableUnitId);

        Assert.Equal(
            stayPeriod,
            booking.StayPeriod);

        Assert.Equal(
            2,
            booking.GuestCount.Value);

        Assert.Equal(
            priceSnapshot,
            booking.PriceSnapshot);

        Assert.Equal(
            BookingStatus.PendingApproval,
            booking.Status);

        Assert.Equal(
            BookingTestTime.CreatedAtUtc,
            booking.CreatedAtUtc);

        Assert.Equal(
            BookingTestTime.ApprovalDueAtUtc,
            booking.ApprovalDueAtUtc);

        Assert.Null(
            booking.CancellationReason);

        Assert.Null(
            booking.PaymentDueAtUtc);
    }

    [Fact]
    public void Create_WhenGuestCountExceedsCapacity_ShouldReturnFailure()
    {
        RentableUnit rentableUnit =
            CreateRentableUnit(
                maximumCapacity: 4);

        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                CreateStayPeriod(),
                GuestCount.Create(5).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            BookingErrors.GuestCapacityExceeded,
            result.Error);
    }

    [Fact]
    public void Create_WhenGuestCountEqualsCapacity_ShouldReturnSuccess()
    {
        RentableUnit rentableUnit =
            CreateRentableUnit(
                maximumCapacity: 4);

        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                CreateStayPeriod(),
                GuestCount.Create(4).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            4,
            result.Value
                .GuestCount
                .Value);
    }

    [Fact]
    public void Create_WithInactiveRentableUnit_ShouldReturnFailure()
    {
        RentableUnit rentableUnit =
            CreateRentableUnit();

        rentableUnit.Deactivate();

        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                CreateStayPeriod(),
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            BookingErrors.RentableUnitInactive,
            result.Error);
    }

    [Fact]
    public void Create_WithInvalidApprovalDeadline_ShouldReturnFailure()
    {
        Result<DomainBooking> result =
            DomainBooking.Create(
                CreateRentableUnit(),
                CreateStayPeriod(),
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.CreatedAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            BookingDeadlineErrors.InvalidApprovalDeadline,
            result.Error);
    }

    [Fact]
    public void Create_TwiceWithSameData_ShouldCreateDifferentBookings()
    {
        RentableUnit rentableUnit =
            CreateRentableUnit();

        StayPeriod stayPeriod =
            CreateStayPeriod();

        Result<DomainBooking> firstResult =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        Result<DomainBooking> secondResult =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        Assert.True(
            firstResult.IsSuccess);

        Assert.True(
            secondResult.IsSuccess);

        Assert.NotEqual(
            firstResult.Value.Id,
            secondResult.Value.Id);
    }

    [Fact]
    public void Create_WithNullRentableUnit_ShouldThrow()
    {
        void Action()
        {
            DomainBooking.Create(
                null!,
                CreateStayPeriod(),
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);
        }

        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    [Fact]
    public void Create_WithNullStayPeriod_ShouldThrow()
    {
        void Action()
        {
            DomainBooking.Create(
                CreateRentableUnit(),
                null!,
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);
        }

        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    [Fact]
    public void Create_WithNullPriceSnapshot_ShouldThrow()
    {
        void Action()
        {
            DomainBooking.Create(
                CreateRentableUnit(),
                CreateStayPeriod(),
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                null!,
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);
        }

        Assert.Throws<
            ArgumentNullException>(
                Action);
    }

    [Fact]
    public void Approve_WhenPendingApproval_ShouldChangeStatusAndSetPaymentDeadline()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.PaymentDueAtUtc);

        Assert.True(result.IsSuccess,
            $"Approval failed: {result.Error.Code} - {result.Error.Message}. " +
            $"Current status: {booking.Status}. " +
            $"ApprovedAtUtc: {BookingTestTime.ApprovedAtUtc:O}. " +
            $"PaymentDueAtUtc: {BookingTestTime.PaymentDueAtUtc:O}.");

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
            booking.CancellationReason);
    }

    [Fact]
    public void Approve_WithInvalidPaymentDeadline_ShouldReturnFailureWithoutMutation()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.ApprovedAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            BookingDeadlineErrors.InvalidPaymentDeadline,
            result.Error);

        Assert.Equal(
            BookingStatus.PendingApproval,
            booking.Status);

        Assert.Null(
            booking.ApprovedAtUtc);

        Assert.Null(
            booking.PaymentDueAtUtc);
    }

    [Fact]
    public void Approve_WhenNotPendingApproval_ShouldReturnFailure()
    {
        DomainBooking booking =
            CreateBooking();

        Assert.True(
            booking.Approve(
                    BookingTestTime.ApprovedAtUtc,
                    BookingTestTime.PaymentDueAtUtc)
                .IsSuccess);

        Result result =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.PaymentDueAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);
    }

    [Fact]
    public void Reject_WhenPendingApproval_ShouldCancelBooking()
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
    }

    [Fact]
    public void Reject_WhenPendingPayment_ShouldReturnFailure()
    {
        DomainBooking booking =
            CreateBooking();

        Assert.True(
            booking.Approve(
                    BookingTestTime.ApprovedAtUtc,
                    BookingTestTime.PaymentDueAtUtc)
                .IsSuccess);

        Result result =
            booking.Reject(
                BookingTestTime.CancelledAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);
    }

    [Fact]
    public void MarkAsPaid_WhenPendingPayment_ShouldChangeStatusToPaid()
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

        Assert.Null(
            booking.CancellationReason);
    }

    [Fact]
    public void MarkAsPaid_WhenPendingApproval_ShouldReturnFailure()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.PendingApproval,
            booking.Status);
    }

    [Fact]
    public void ExpirePayment_WhenPendingPayment_ShouldCancelBooking()
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
    }

    [Fact]
    public void ExpirePayment_WhenPendingApproval_ShouldReturnFailure()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.ExpirePayment(
                BookingTestTime.CancelledAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.PendingApproval,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);
    }

    [Fact]
    public void Complete_WhenPaid_ShouldChangeStatusToCompleted()
    {
        DomainBooking booking =
            CreatePaidBooking();

        Result result =
            booking.Complete(
                BookingTestTime.CompletedAtUtc);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);
    }

    [Fact]
    public void Complete_WhenPendingPayment_ShouldReturnFailure()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result result =
            booking.Complete(
                BookingTestTime.CompletedAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);
    }

    [Fact]
    public void ValidLifecycle_ShouldReachCompletedStatus()
    {
        DomainBooking booking =
            CreateBooking();

        Result approvalResult =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.PaymentDueAtUtc);

        Result paymentResult =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        Result completionResult =
            booking.Complete(
                BookingTestTime.CompletedAtUtc);

        Assert.True(
            approvalResult.IsSuccess);

        Assert.True(
            paymentResult.IsSuccess);

        Assert.True(
            completionResult.IsSuccess);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);
    }

    [Fact]
    public void CancelledBooking_ShouldNotAllowPayment()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Assert.True(
            booking.ExpirePayment(
                    BookingTestTime.CancelledAtUtc)
                .IsSuccess);

        Result result =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.PaymentExpired,
            booking.CancellationReason);
    }

    [Fact]
    public void CompletedBooking_ShouldNotAllowAnotherTransition()
    {
        DomainBooking booking =
            CreatePaidBooking();

        Assert.True(
            booking.Complete(
                    BookingTestTime.CompletedAtUtc)
                .IsSuccess);

        Result result =
            booking.Complete(
                BookingTestTime.CompletedAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);
    }

    [Fact]
    public void PriceSnapshot_ShouldRemainUnchangedThroughBookingLifecycle()
    {
        PriceSnapshot priceSnapshot =
            BookingTestData.CreatePriceSnapshot();

        DomainBooking booking =
            DomainBooking.Create(
                    CreateRentableUnit(),
                    CreateStayPeriod(),
                    GuestCount.Create(2).Value,
                    BookingTestData.CreateGuestDetails(),
                    priceSnapshot,
                    BookingTestTime.CreatedAtUtc,
                    BookingTestTime.ApprovalDueAtUtc)
                .Value;

        Assert.True(
            booking.Approve(
                    BookingTestTime.ApprovedAtUtc,
                    BookingTestTime.PaymentDueAtUtc)
                .IsSuccess);

        Assert.True(
            booking.MarkAsPaid(
                    BookingTestTime.PaidAtUtc)
                .IsSuccess);

        Assert.True(
            booking.Complete(
                    BookingTestTime.CompletedAtUtc)
                .IsSuccess);

        Assert.Equal(
            priceSnapshot,
            booking.PriceSnapshot);

        Assert.Equal(
            450m,
            booking
                .PriceSnapshot!
                .TotalPrice
                .Amount);
    }

    [Fact]
    public void Cancel_WhenPendingApproval_ShouldCancelBooking()
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

        Assert.False(
            booking.BlocksInventory);
    }

    [Fact]
    public void Cancel_WhenPendingPayment_ShouldCancelBooking()
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
            BookingCancellationReason.CancelledByGuest,
            booking.CancellationReason);

        Assert.False(
            booking.BlocksInventory);
    }

    [Fact]
    public void Cancel_WhenPaid_ShouldReturnFailure()
    {
        DomainBooking booking =
            CreatePaidBooking();

        Result result =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldReturnFailure()
    {
        DomainBooking booking =
            CreateBooking();

        Assert.True(
            booking.Cancel(
                    BookingTestTime.CancelledAtUtc)
                .IsSuccess);

        Result result =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

        AssertInvalidTransition(
            result);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            booking.CancellationReason);
    }

    [Fact]
    public void BlocksInventory_ShouldMatchBookingLifecycle()
    {
        DomainBooking pendingApproval =
            CreateBooking();

        DomainBooking pendingPayment =
            CreatePendingPaymentBooking();

        DomainBooking paid =
            CreatePaidBooking();

        DomainBooking completed =
            CreatePaidBooking();

        DomainBooking cancelled =
            CreateBooking();

        Assert.True(
            completed.Complete(
                    BookingTestTime.CompletedAtUtc)
                .IsSuccess);

        Assert.True(
            cancelled.Reject(
                    BookingTestTime.CancelledAtUtc)
                .IsSuccess);

        Assert.True(
            pendingApproval.BlocksInventory);

        Assert.True(
            pendingPayment.BlocksInventory);

        Assert.True(
            paid.BlocksInventory);

        Assert.True(
            completed.BlocksInventory);

        Assert.False(
            cancelled.BlocksInventory);
    }

    private static void AssertInvalidTransition(
        Result result)
    {
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            "Booking.InvalidStatusTransition",
            result.Error.Code);

        Assert.Equal(
            ErrorType.Conflict,
            result.Error.Type);
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

    private static DomainBooking CreatePaidBooking()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result result =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        Assert.True(
            result.IsSuccess);

        return booking;
    }

    private static RentableUnit CreateRentableUnit(
        int maximumCapacity = 4)
    {
        return RentableUnit.Create(
                PropertyId,
                "Habitación principal",
                RentableUnitType.Room,
                maximumCapacity,
                maxBaseGuests: 2)
            .Value;
    }

    private static StayPeriod CreateStayPeriod()
    {
        return StayPeriod.Create(
                new DateOnly(
                    2026,
                    7,
                    10),
                new DateOnly(
                    2026,
                    7,
                    12))
            .Value;
    }
}
