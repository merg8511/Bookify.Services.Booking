using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings;

public sealed class Booking : AggregateRoot
{
    private Booking()
    {
        StayPeriod = null!;
        GuestCount = null!;
    }

    private Booking(
        Guid id,
        Guid propertyId,
        Guid rentableUnitId,
        StayPeriod stayPeriod,
        GuestCount guestCount,
        PriceSnapshot? priceSnapshot,
        BookingStatus status)
    {
        Id = id;
        PropertyId = propertyId;
        RentableUnitId = rentableUnitId;
        StayPeriod = stayPeriod;
        GuestCount = guestCount;
        PriceSnapshot = priceSnapshot;
        Status = status;
    }

    public Guid Id { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid RentableUnitId { get; private set; }
    public StayPeriod StayPeriod { get; private set; }
    public GuestCount GuestCount { get; private set; }
    public PriceSnapshot? PriceSnapshot { get; private set; }
    public BookingStatus Status { get; private set; }
    public BookingCancellationReason? CancellationReason { get; private set; }

    public bool BlocksInventory =>
        Status is BookingStatus.PendingApproval
            or BookingStatus.PendingPayment
            or BookingStatus.Paid
            or BookingStatus.Completed;

    public static Result<Booking> Create(
        RentableUnit rentableUnit,
        StayPeriod stayPeriod,
        GuestCount guestCount)
    {
        return CreateInternal(
            rentableUnit,
            stayPeriod,
            guestCount,
            priceSnapshot: null);
    }

    public static Result<Booking> Create(
        RentableUnit rentableUnit,
        StayPeriod stayPeriod,
        GuestCount guestCount,
        PriceSnapshot priceSnapshot)
    {
        ArgumentNullException.ThrowIfNull(priceSnapshot);

        return CreateInternal(
            rentableUnit,
            stayPeriod,
            guestCount,
            priceSnapshot);
    }

    public Result Approve()
    {
        Result result = TransitionTo(
            expectedCurrentStatus: BookingStatus.PendingApproval,
            targetStatus: BookingStatus.PendingPayment);

        if (result.IsFailure)
        {
            return result;
        }

        RaiseDomainEvent(
            new BookingApprovedDomainEvent(Id));

        return Result.Success();
    }

    public Result Reject()
    {
        return TransitionToCancelled(
            expectedCurrentStatus: BookingStatus.PendingApproval,
            cancellationReason: BookingCancellationReason.RejectedByOwner);
    }

    public Result MarkAsPaid()
    {
        Result result = TransitionTo(
            expectedCurrentStatus: BookingStatus.PendingPayment,
            targetStatus: BookingStatus.Paid);

        if (result.IsFailure)
        {
            return result;
        }

        RaiseDomainEvent(new BookingPaidDomainEvent(Id));

        return Result.Success();
    }

    public Result ExpirePayment()
    {
        return TransitionToCancelled(
            expectedCurrentStatus: BookingStatus.PendingPayment,
            cancellationReason: BookingCancellationReason.PaymentExpired);
    }

    public Result Complete()
    {
        return TransitionTo(
            expectedCurrentStatus: BookingStatus.Paid,
            targetStatus: BookingStatus.Completed);
    }

    public Result Cancel()
    {
        if (Status == BookingStatus.PendingApproval)
        {
            return TransitionToCancelled(
                expectedCurrentStatus: BookingStatus.PendingApproval,
                cancellationReason: BookingCancellationReason.CancelledByGuest);
        }

        if (Status == BookingStatus.PendingPayment)
        {
            return TransitionToCancelled(
                expectedCurrentStatus: BookingStatus.PendingPayment,
                cancellationReason: BookingCancellationReason.CancelledByGuest);
        }

        return Result.Failure(
            BookingErrors.InvalidStatusTransition(
                Status,
                BookingStatus.Cancelled));
    }

    private static Result<Booking> CreateInternal(
        RentableUnit rentableUnit,
        StayPeriod stayPeriod,
        GuestCount guestCount,
        PriceSnapshot? priceSnapshot)
    {
        ArgumentNullException.ThrowIfNull(rentableUnit);
        ArgumentNullException.ThrowIfNull(stayPeriod);
        ArgumentNullException.ThrowIfNull(guestCount);

        if (!rentableUnit.IsActive)
        {
            return Result<Booking>.Failure(
                BookingErrors.RentableUnitInactive);
        }

        if (!rentableUnit.CanAccommodate(guestCount))
        {
            return Result<Booking>.Failure(
                BookingErrors.GuestCapacityExceeded);
        }

        var booking = new Booking(
            Guid.NewGuid(),
            rentableUnit.PropertyId,
            rentableUnit.Id,
            stayPeriod,
            guestCount,
            priceSnapshot,
            BookingStatus.PendingApproval);

        booking.RaiseDomainEvent(new BookingCreatedDomainEvent(booking.Id));

        return Result<Booking>.Success(booking);
    }

    private Result TransitionToCancelled(
        BookingStatus expectedCurrentStatus,
        BookingCancellationReason cancellationReason)
    {
        Result result =
            TransitionTo(
                expectedCurrentStatus,
                BookingStatus.Cancelled,
                cancellationReason);

        if (result.IsFailure)
        {
            return result;
        }

        RaiseDomainEvent(
            new BookingCancelledDomainEvent(
                Id,
                cancellationReason));

        return Result.Success();
    }

    private Result TransitionTo(
        BookingStatus expectedCurrentStatus,
        BookingStatus targetStatus,
        BookingCancellationReason? cancellationReason = null)
    {
        EnsureCancellationReasonIsConsistent(
            targetStatus,
            cancellationReason);

        if (Status != expectedCurrentStatus)
        {
            return Result.Failure(
                BookingErrors.InvalidStatusTransition(
                    Status,
                    targetStatus));
        }

        Status = targetStatus;
        CancellationReason = cancellationReason;

        return Result.Success();
    }

    private static void EnsureCancellationReasonIsConsistent(
        BookingStatus targetStatus,
        BookingCancellationReason? cancellationReason)
    {
        bool transitionsToCancelled =
            targetStatus == BookingStatus.Cancelled;

        if (transitionsToCancelled && cancellationReason is null)
        {
            throw new InvalidOperationException(
                "A transition to Cancelled must include a cancellation reason.");
        }

        if (!transitionsToCancelled && cancellationReason is not null)
        {
            throw new InvalidOperationException(
                "A cancellation reason can only be assigned when transitioning to Cancelled.");
        }
    }
}
