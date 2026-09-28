using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

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
        Result<DomainBooking> result =
            DomainBooking.Create(
                CreateRentableUnit(),
                CreateStayPeriod(),
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        Assert.True(
            result.IsSuccess);

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
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result result =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.PaymentDueAtUtc);

        Assert.True(
            result.IsSuccess);

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
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Assert.True(
            booking.Approve(
                    BookingTestTime.ApprovedAtUtc,
                    BookingTestTime.PaymentDueAtUtc)
                .IsSuccess);

        booking.ClearDomainEvents();

        Result result =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.PaymentDueAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Empty(
            booking.GetDomainEvents());
    }

    [Fact]
    public void MarkAsPaid_WhenSuccessful_ShouldRaiseBookingPaidDomainEvent()
    {
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Assert.True(
            booking.Approve(
                    BookingTestTime.ApprovedAtUtc,
                    BookingTestTime.PaymentDueAtUtc)
                .IsSuccess);

        booking.ClearDomainEvents();

        Result result =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        Assert.True(
            result.IsSuccess);

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
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result result =
            booking.MarkAsPaid(
                BookingTestTime.PaidAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Empty(
            booking.GetDomainEvents());
    }

    [Fact]
    public void Reject_WhenSuccessful_ShouldRaiseCancelledEventWithRejectedByOwnerReason()
    {
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result result =
            booking.Reject(
                BookingTestTime.CancelledAtUtc);

        Assert.True(
            result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent =
            AssertCancelledEvent(
                booking);

        Assert.Equal(
            BookingCancellationReason.RejectedByOwner,
            cancelledEvent.CancellationReason);
    }

    [Fact]
    public void ExpirePayment_WhenSuccessful_ShouldRaiseCancelledEventWithPaymentExpiredReason()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        booking.ClearDomainEvents();

        Result result =
            booking.ExpirePayment(
                BookingTestTime.CancelledAtUtc);

        Assert.True(
            result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent =
            AssertCancelledEvent(
                booking);

        Assert.Equal(
            BookingCancellationReason.PaymentExpired,
            cancelledEvent.CancellationReason);
    }

    [Fact]
    public void Cancel_WhenPendingApproval_ShouldRaiseCancelledEventWithCancelledByGuestReason()
    {
        DomainBooking booking =
            CreateBooking();

        booking.ClearDomainEvents();

        Result result =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

        Assert.True(
            result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent =
            AssertCancelledEvent(
                booking);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            cancelledEvent.CancellationReason);
    }

    [Fact]
    public void Cancel_WhenPendingPayment_ShouldRaiseCancelledEventWithCancelledByGuestReason()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        booking.ClearDomainEvents();

        Result result =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

        Assert.True(
            result.IsSuccess);

        BookingCancelledDomainEvent cancelledEvent =
            AssertCancelledEvent(
                booking);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            cancelledEvent.CancellationReason);
    }

    [Fact]
    public void Cancel_WhenTransitionIsInvalid_ShouldNotRaiseDomainEvent()
    {
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Assert.True(
            booking.MarkAsPaid(
                    BookingTestTime.PaidAtUtc)
                .IsSuccess);

        booking.ClearDomainEvents();

        Result result =
            booking.Cancel(
                BookingTestTime.CancelledAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Empty(
            booking.GetDomainEvents());
    }

    [Fact]
    public void ValidLifecycle_ShouldPreserveDomainEventOrder()
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

    private static BookingCancelledDomainEvent AssertCancelledEvent(
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

    private static DomainBooking CreatePendingPaymentBooking()
    {
        DomainBooking booking =
            CreateBooking();

        Assert.True(
            booking.Approve(
                    BookingTestTime.ApprovedAtUtc,
                    BookingTestTime.PaymentDueAtUtc)
                .IsSuccess);

        return booking;
    }

    private static DomainBooking CreateBooking()
    {
        Result<DomainBooking> result =
            DomainBooking.Create(
                CreateRentableUnit(),
                CreateStayPeriod(),
                GuestCount.Create(2).Value,
                BookingTestData.CreateGuestDetails(),
                BookingTestData.CreatePriceSnapshot(),
                BookingTestTime.CreatedAtUtc,
                BookingTestTime.ApprovalDueAtUtc);

        Assert.True(
            result.IsSuccess);

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

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }

    private static StayPeriod CreateStayPeriod()
    {
        Result<StayPeriod> result =
            StayPeriod.Create(
                new DateOnly(
                    2026,
                    9,
                    10),
                new DateOnly(
                    2026,
                    9,
                    12));

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }
}
