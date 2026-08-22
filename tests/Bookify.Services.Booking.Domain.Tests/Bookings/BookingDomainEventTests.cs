using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingDomainEventTests
{
    private static readonly Guid PropertyId =
        Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ShouldRaiseBookingCreatedDomainEvent()
    {
        // ARRANGE
        RentableUnit rentableUnit =
            CreateRentableUnit();

        StayPeriod stayPeriod =
            CreateStayPeriod();

        GuestCount guestCount =
            GuestCount.Create(2).Value;

        // ACT
        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount);

        // ASSERT
        Assert.True(result.IsSuccess);

        IDomainEvent domainEvent =
            Assert.Single(
                result.Value
                    .GetDomainEvents());

        BookingCreatedDomainEvent createdEvent =
            Assert.IsType<
                BookingCreatedDomainEvent>(
                    domainEvent);

        Assert.Equal(
            result.Value.Id,
            createdEvent.BookingId);
    }

    [Fact]
    public void Approve_WhenSuccessful_ShouldRaiseBookingApprovedDomainEvent()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.Approve();

        // ASSERT
        Assert.True(result.IsSuccess);

        IDomainEvent domainEvent =
            Assert.Single(
                booking.GetDomainEvents());

        BookingApprovedDomainEvent approvedEvent =
            Assert.IsType<
                BookingApprovedDomainEvent>(
                    domainEvent);

        Assert.Equal(
            booking.Id,
            approvedEvent.BookingId);
    }

    [Fact]
    public void Approve_WhenTransitionIsInvalid_ShouldNotRaiseDomainEvent()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result firstApproval =
            booking.Approve();

        Assert.True(
            firstApproval.IsSuccess);

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.Approve();

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Empty(
            booking.GetDomainEvents());
    }

    [Fact]
    public void MarkAsPaid_WhenSuccessful_ShouldRaiseBookingPaidDomainEvent()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result approvalResult =
            booking.Approve();

        Assert.True(
            approvalResult.IsSuccess);

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.MarkAsPaid();

        // ASSERT
        Assert.True(result.IsSuccess);

        IDomainEvent domainEvent =
            Assert.Single(
                booking.GetDomainEvents());

        BookingPaidDomainEvent paidEvent =
            Assert.IsType<
                BookingPaidDomainEvent>(
                    domainEvent);

        Assert.Equal(
            booking.Id,
            paidEvent.BookingId);
    }

    [Fact]
    public void MarkAsPaid_WhenTransitionIsInvalid_ShouldNotRaiseDomainEvent()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.MarkAsPaid();

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Empty(
            booking.GetDomainEvents());
    }

    [Fact]
    public void Reject_WhenSuccessful_ShouldRaiseCancelledEventWithRejectedByOwnerReason()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.Reject();

        // ASSERT
        Assert.True(result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent =
            AssertCancelledEvent(
                booking);

        Assert.Equal(
            BookingCancellationReason
                .RejectedByOwner,
            cancelledEvent
                .CancellationReason);
    }

    [Fact]
    public void ExpirePayment_WhenSuccessful_ShouldRaiseCancelledEventWithPaymentExpiredReason()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result approvalResult =
            booking.Approve();

        Assert.True(
            approvalResult.IsSuccess);

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.ExpirePayment();

        // ASSERT
        Assert.True(result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent =
            AssertCancelledEvent(
                booking);

        Assert.Equal(
            BookingCancellationReason
                .PaymentExpired,
            cancelledEvent
                .CancellationReason);
    }

    [Fact]
    public void Cancel_WhenPendingApproval_ShouldRaiseCancelledEventWithCancelledByGuestReason()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.Cancel();

        // ASSERT
        Assert.True(result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent =
            AssertCancelledEvent(
                booking);

        Assert.Equal(
            BookingCancellationReason
                .CancelledByGuest,
            cancelledEvent
                .CancellationReason);
    }

    [Fact]
    public void Cancel_WhenPendingPayment_ShouldRaiseCancelledEventWithCancelledByGuestReason()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result approvalResult =
            booking.Approve();

        Assert.True(
            approvalResult.IsSuccess);

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.Cancel();

        // ASSERT
        Assert.True(result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent =
            AssertCancelledEvent(
                booking);

        Assert.Equal(
            BookingCancellationReason
                .CancelledByGuest,
            cancelledEvent
                .CancellationReason);
    }

    [Fact]
    public void Cancel_WhenTransitionIsInvalid_ShouldNotRaiseDomainEvent()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result approvalResult =
            booking.Approve();

        Assert.True(
            approvalResult.IsSuccess);

        Result paymentResult =
            booking.MarkAsPaid();

        Assert.True(
            paymentResult.IsSuccess);

        booking.ClearDomainEvents();

        // ACT
        Result result =
            booking.Cancel();

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Empty(
            booking.GetDomainEvents());
    }

    [Fact]
    public void ValidLifecycle_ShouldPreserveDomainEventOrder()
    {
        // ARRANGE
        DomainBooking booking =
            CreateBooking();

        // ACT
        Result approvalResult =
            booking.Approve();

        Result paymentResult =
            booking.MarkAsPaid();

        // ASSERT
        Assert.True(
            approvalResult.IsSuccess);

        Assert.True(
            paymentResult.IsSuccess);

        Assert.Collection(
            booking.GetDomainEvents(),
            domainEvent =>
            {
                BookingCreatedDomainEvent createdEvent =
                    Assert.IsType<
                        BookingCreatedDomainEvent>(
                            domainEvent);

                Assert.Equal(
                    booking.Id,
                    createdEvent.BookingId);
            },
            domainEvent =>
            {
                BookingApprovedDomainEvent approvedEvent =
                    Assert.IsType<
                        BookingApprovedDomainEvent>(
                            domainEvent);

                Assert.Equal(
                    booking.Id,
                    approvedEvent.BookingId);
            },
            domainEvent =>
            {
                BookingPaidDomainEvent paidEvent =
                    Assert.IsType<
                        BookingPaidDomainEvent>(
                            domainEvent);

                Assert.Equal(
                    booking.Id,
                    paidEvent.BookingId);
            });
    }

    private static BookingCancelledDomainEvent
        AssertCancelledEvent(
            DomainBooking booking)
    {
        IDomainEvent domainEvent =
            Assert.Single(
                booking.GetDomainEvents());

        BookingCancelledDomainEvent cancelledEvent =
            Assert.IsType<
                BookingCancelledDomainEvent>(
                    domainEvent);

        Assert.Equal(
            booking.Id,
            cancelledEvent.BookingId);

        return cancelledEvent;
    }

    private static DomainBooking CreateBooking()
    {
        Result<DomainBooking> result =
            DomainBooking.Create(
                CreateRentableUnit(),
                CreateStayPeriod(),
                GuestCount.Create(2).Value);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    private static RentableUnit CreateRentableUnit()
    {
        Result<RentableUnit> result =
            RentableUnit.Create(
                PropertyId,
                "Test unit",
                RentableUnitType.EntireProperty,
                maximumCapacity: 4,
                maxBaseGuests: 2);

        Assert.True(result.IsSuccess);

        return result.Value;
    }

    private static StayPeriod CreateStayPeriod()
    {
        Result<StayPeriod> result =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 12));

        Assert.True(result.IsSuccess);

        return result.Value;
    }
}
