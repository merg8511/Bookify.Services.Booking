using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings;

public sealed class Booking
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
        BookingStatus status)
    {
        Id = id;
        PropertyId = propertyId;
        RentableUnitId = rentableUnitId;
        StayPeriod = stayPeriod;
        GuestCount = guestCount;
        Status = status;
    }

    public Guid Id { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid RentableUnitId { get; private set; }
    public StayPeriod StayPeriod { get; private set; }
    public GuestCount GuestCount { get; private set; }
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
            BookingStatus.PendingApproval);

        return Result<Booking>.Success(booking);
    }

    public Result Approve()
    {
        return TransitionTo(
            expectedCurrentStatus: BookingStatus.PendingApproval,
            targetStatus: BookingStatus.PendingPayment);
    }

    public Result Reject()
    {
        return TransitionTo(
            expectedCurrentStatus: BookingStatus.PendingApproval,
            targetStatus: BookingStatus.Cancelled,
            cancellationReason: BookingCancellationReason.RejectedByOwner);
    }

    public Result MarkAsPaid()
    {
        return TransitionTo(
            expectedCurrentStatus: BookingStatus.PendingPayment,
            targetStatus: BookingStatus.Paid);
    }

    public Result ExpirePayment()
    {
        return TransitionTo(
            expectedCurrentStatus: BookingStatus.PendingPayment,
            targetStatus: BookingStatus.Cancelled,
            cancellationReason: BookingCancellationReason.PaymentExpired);
    }

    public Result Complete()
    {
        return TransitionTo(
            expectedCurrentStatus: BookingStatus.Paid,
            targetStatus: BookingStatus.Completed);
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
