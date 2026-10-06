using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingDomainEventTests
{
    private static readonly Guid PropertyId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldRaiseBookingCreatedDomainEvent()
    {
        RentableUnit unit = RentableUnitTestFactory.CreateValidRentableUnit(
            propertyId: PropertyId,
            name: "Test unit",
            type: RentableUnitType.EntireProperty);

        Result<DomainBooking> result = DomainBooking.Create(
            unit,
            BookingTestData.CreateStayPeriod(),
            BookingTestData.CreateGuestCount(),
            BookingTestData.CreateGuestDetails(),
            BookingTestData.CreatePriceSnapshot(),
            BookingTestTime.CreatedAtUtc,
            BookingTestTime.ApprovalDueAtUtc);

        Assert.True(result.IsSuccess);

        IDomainEvent domainEvent = Assert.Single(result.Value.GetDomainEvents());
        var createdEvent = Assert.IsType<BookingCreatedDomainEvent>(domainEvent);

        Assert.Equal(result.Value.Id, createdEvent.BookingId);
    }

    [Fact]
    public void Approve_WhenSuccessful_ShouldRaiseBookingApprovedDomainEvent()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();
        booking.ClearDomainEvents();

        Result result = booking.Approve(
            BookingTestTime.ApprovedAtUtc,
            BookingTestTime.PaymentDueAtUtc);

        Assert.True(result.IsSuccess);

        IDomainEvent domainEvent = Assert.Single(booking.GetDomainEvents());
        var approvedEvent = Assert.IsType<BookingApprovedDomainEvent>(domainEvent);

        Assert.Equal(booking.Id, approvedEvent.BookingId);
    }

    [Fact]
    public void Approve_WhenTransitionIsInvalid_ShouldNotRaiseDomainEvent()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();
        booking.ClearDomainEvents();

        Assert.True(booking.Approve(
            BookingTestTime.ApprovedAtUtc,
            BookingTestTime.PaymentDueAtUtc).IsSuccess);

        booking.ClearDomainEvents();

        Result result = booking.Approve(
            BookingTestTime.ApprovedAtUtc,
            BookingTestTime.PaymentDueAtUtc);

        Assert.True(result.IsFailure);
        Assert.Empty(booking.GetDomainEvents());
    }

    [Fact]
    public void MarkAsPaid_WhenSuccessful_ShouldRaiseBookingPaidDomainEvent()
    {
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        booking.ClearDomainEvents();

        Result result = booking.MarkAsPaid(BookingTestTime.PaidAtUtc);

        Assert.True(result.IsSuccess);

        IDomainEvent domainEvent = Assert.Single(booking.GetDomainEvents());
        var paidEvent = Assert.IsType<BookingPaidDomainEvent>(domainEvent);

        Assert.Equal(booking.Id, paidEvent.BookingId);
    }

    [Fact]
    public void MarkAsPaid_WhenTransitionIsInvalid_ShouldNotRaiseDomainEvent()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();
        booking.ClearDomainEvents();

        Result result = booking.MarkAsPaid(BookingTestTime.PaidAtUtc);

        Assert.True(result.IsFailure);
        Assert.Empty(booking.GetDomainEvents());
    }

    [Fact]
    public void Reject_WhenSuccessful_ShouldRaiseCancelledEventWithRejectedByOwnerReason()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();
        booking.ClearDomainEvents();

        Result result = booking.Reject(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent = AssertCancelledEvent(booking);
        Assert.Equal(BookingCancellationReason.RejectedByOwner, cancelledEvent.CancellationReason);
    }

    [Fact]
    public void ExpirePayment_WhenSuccessful_ShouldRaiseCancelledEventWithPaymentExpiredReason()
    {
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        booking.ClearDomainEvents();

        Result result = booking.ExpirePayment(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent = AssertCancelledEvent(booking);
        Assert.Equal(BookingCancellationReason.PaymentExpired, cancelledEvent.CancellationReason);
    }

    [Fact]
    public void Cancel_WhenPendingApproval_ShouldRaiseCancelledEventWithCancelledByGuestReason()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();
        booking.ClearDomainEvents();

        Result result = booking.Cancel(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent = AssertCancelledEvent(booking);
        Assert.Equal(BookingCancellationReason.CancelledByGuest, cancelledEvent.CancellationReason);
    }

    [Fact]
    public void Cancel_WhenPendingPayment_ShouldRaiseCancelledEventWithCancelledByGuestReason()
    {
        DomainBooking booking = BookingTestFactory.CreatePendingPaymentBooking();
        booking.ClearDomainEvents();

        Result result = booking.Cancel(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent = AssertCancelledEvent(booking);
        Assert.Equal(BookingCancellationReason.CancelledByGuest, cancelledEvent.CancellationReason);
    }

    [Fact]
    public void Cancel_WhenTransitionIsInvalid_ShouldNotRaiseDomainEvent()
    {
        DomainBooking booking = BookingTestFactory.CreatePaidBooking();
        booking.ClearDomainEvents();

        Result result = booking.Cancel(BookingTestTime.CancelledAtUtc);

        Assert.True(result.IsFailure);
        Assert.Empty(booking.GetDomainEvents());
    }

    [Fact]
    public void ValidLifecycle_ShouldPreserveDomainEventOrder()
    {
        DomainBooking booking = BookingTestFactory.CreateValidBooking();

        Result approvalResult = booking.Approve(
            BookingTestTime.ApprovedAtUtc,
            BookingTestTime.PaymentDueAtUtc);

        Result paymentResult = booking.MarkAsPaid(BookingTestTime.PaidAtUtc);

        Assert.True(approvalResult.IsSuccess);
        Assert.True(paymentResult.IsSuccess);

        Assert.Collection(
            booking.GetDomainEvents(),
            domainEvent =>
            {
                var createdEvent = Assert.IsType<BookingCreatedDomainEvent>(domainEvent);
                Assert.Equal(booking.Id, createdEvent.BookingId);
            },
            domainEvent =>
            {
                var approvedEvent = Assert.IsType<BookingApprovedDomainEvent>(domainEvent);
                Assert.Equal(booking.Id, approvedEvent.BookingId);
            },
            domainEvent =>
            {
                var paidEvent = Assert.IsType<BookingPaidDomainEvent>(domainEvent);
                Assert.Equal(booking.Id, paidEvent.BookingId);
            });
    }

    private static BookingCancelledDomainEvent AssertCancelledEvent(DomainBooking booking)
    {
        IDomainEvent domainEvent = Assert.Single(booking.GetDomainEvents());
        var cancelledEvent = Assert.IsType<BookingCancelledDomainEvent>(domainEvent);

        Assert.Equal(booking.Id, cancelledEvent.BookingId);

        return cancelledEvent;
    }
}
