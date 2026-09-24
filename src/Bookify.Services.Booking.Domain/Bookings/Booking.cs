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
        Reference = null!;
        StayPeriod = null!;
        GuestCount = null!;
    }

    private Booking(
        Guid id,
        BookingReference reference,
        Guid propertyId,
        Guid rentableUnitId,
        StayPeriod stayPeriod,
        GuestCount guestCount,
        GuestDetails guestDetails,
        PriceSnapshot? priceSnapshot,
        DateTimeOffset createdAtUtc,
        BookingStatus status)
    {
        Id = id;
        Reference = reference;
        PropertyId = propertyId;
        RentableUnitId = rentableUnitId;
        StayPeriod = stayPeriod;
        GuestCount = guestCount;
        GuestDetails = guestDetails;
        PriceSnapshot = priceSnapshot;
        CreatedAtUtc = createdAtUtc;
        Status = status;
    }

    public Guid Id { get; private set; }
    public BookingReference Reference { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid RentableUnitId { get; private set; }
    public StayPeriod StayPeriod { get; private set; }
    public GuestCount GuestCount { get; private set; }
    public GuestDetails? GuestDetails { get; private set; }
    public PriceSnapshot? PriceSnapshot { get; private set; }
    public BookingStatus Status { get; private set; }
    public BookingCancellationReason? CancellationReason { get; private set; }
    public DateTimeOffset? CreatedAtUtc { get; private set; }
    public DateTimeOffset? ApprovalDueAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? PaymentDueAtUtc { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public bool BlocksInventory =>
        Status is BookingStatus.PendingApproval
            or BookingStatus.PendingPayment
            or BookingStatus.Paid
            or BookingStatus.Completed;

    public static Result<Booking> Create(
        RentableUnit rentableUnit,
        StayPeriod stayPeriod,
        GuestCount guestCount,
        GuestDetails guestDetails,
        DateTimeOffset createdAtUtc)
    {
        return CreateInternal(
            rentableUnit,
            stayPeriod,
            guestCount,
            guestDetails,
            priceSnapshot: null,
            createdAtUtc);
    }

    public static Result<Booking> Create(
        RentableUnit rentableUnit,
        StayPeriod stayPeriod,
        GuestCount guestCount,
        GuestDetails guestDetails,
        PriceSnapshot priceSnapshot,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(priceSnapshot);

        return CreateInternal(
            rentableUnit,
            stayPeriod,
            guestCount,
            guestDetails,
            priceSnapshot,
            createdAtUtc);
    }

    public Result Approve(DateTimeOffset approvedAtUtc)
    {
        Result result = TransitionTo(
            expectedCurrentStatus: BookingStatus.PendingApproval,
            targetStatus: BookingStatus.PendingPayment);

        if (result.IsFailure)
        {
            return result;
        }

        ApprovedAtUtc = approvedAtUtc;

        RaiseDomainEvent(new BookingApprovedDomainEvent(Id));

        return Result.Success();
    }

    public Result Reject(DateTimeOffset cancelledAtUtc)
    {
        return TransitionToCancelled(
            expectedCurrentStatus: BookingStatus.PendingApproval,
            cancellationReason: BookingCancellationReason.RejectedByOwner,
            cancelledAtUtc);
    }

    public Result MarkAsPaid(DateTimeOffset paidAtUtc)
    {
        Result result = TransitionTo(
            expectedCurrentStatus: BookingStatus.PendingPayment,
            targetStatus: BookingStatus.Paid);

        if (result.IsFailure)
        {
            return result;
        }

        PaidAtUtc = paidAtUtc;

        RaiseDomainEvent(new BookingPaidDomainEvent(Id));

        return Result.Success();
    }

    public Result ExpirePayment(DateTimeOffset cancelledAtUtc)
    {
        return TransitionToCancelled(
            expectedCurrentStatus: BookingStatus.PendingPayment,
            cancellationReason: BookingCancellationReason.PaymentExpired,
            cancelledAtUtc);
    }

    public Result Complete(DateTimeOffset completedAtUtc)
    {
        Result result = TransitionTo(
            expectedCurrentStatus: BookingStatus.Paid,
            targetStatus: BookingStatus.Completed);

        if (result.IsFailure)
        {
            return result;
        }

        CompletedAtUtc = completedAtUtc;
        return Result.Success();
    }

    public Result Cancel(DateTimeOffset cancelledAtUtc)
    {
        if (Status == BookingStatus.PendingApproval)
        {
            return TransitionToCancelled(
                expectedCurrentStatus: BookingStatus.PendingApproval,
                cancellationReason: BookingCancellationReason.CancelledByGuest,
                cancelledAtUtc);
        }

        if (Status == BookingStatus.PendingPayment)
        {
            return TransitionToCancelled(
                expectedCurrentStatus: BookingStatus.PendingPayment,
                cancellationReason: BookingCancellationReason.CancelledByGuest,
                cancelledAtUtc);
        }

        return Result.Failure(
            BookingErrors.InvalidStatusTransition(
                Status,
                BookingStatus.Cancelled));
    }

    public Result ScheduleApprovalDeadline(DateTimeOffset approvalDueAtUtc)
    {
        if (Status != BookingStatus.PendingApproval)
        {
            return Result.Failure(
                BookingDeadlineErrors.InvalidStatusForApprovalDeadline(Status));
        }

        if (ApprovalDueAtUtc is not null)
        {
            return Result.Failure(
                BookingDeadlineErrors.ApprovalDeadlineAlreadyScheduled);
        }

        if (CreatedAtUtc is null || approvalDueAtUtc <= CreatedAtUtc.Value)
        {
            return Result.Failure(
                BookingDeadlineErrors.InvalidApprovalDeadline);
        }

        ApprovalDueAtUtc = approvalDueAtUtc;

        return Result.Success();
    }

    public Result SchedulePaymentDeadline(DateTimeOffset paymentDueAtUtc)
    {
        if (Status != BookingStatus.PendingPayment)
        {
            return Result.Failure(
                BookingDeadlineErrors.InvalidStatusForPaymentDeadline(Status));
        }

        if (PaymentDueAtUtc is not null)
        {
            return Result.Failure(
                BookingDeadlineErrors.PaymentDeadlineAlreadyScheduled);
        }

        if (ApprovedAtUtc is null || paymentDueAtUtc <= ApprovedAtUtc.Value)
        {
            return Result.Failure(
                BookingDeadlineErrors.InvalidPaymentDeadline);
        }

        PaymentDueAtUtc = paymentDueAtUtc;

        return Result.Success();
    }

    private static Result<Booking> CreateInternal(
        RentableUnit rentableUnit,
        StayPeriod stayPeriod,
        GuestCount guestCount,
        GuestDetails guestDetails,
        PriceSnapshot? priceSnapshot,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(rentableUnit);
        ArgumentNullException.ThrowIfNull(stayPeriod);
        ArgumentNullException.ThrowIfNull(guestCount);
        ArgumentNullException.ThrowIfNull(guestDetails);

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

        BookingReference reference = BookingReference.New();

        var booking = new Booking(
            Guid.NewGuid(),
            reference,
            rentableUnit.PropertyId,
            rentableUnit.Id,
            stayPeriod,
            guestCount,
            guestDetails,
            priceSnapshot,
            createdAtUtc,
            BookingStatus.PendingApproval);

        booking.RaiseDomainEvent(new BookingCreatedDomainEvent(booking.Id));

        return Result<Booking>.Success(booking);
    }

    private Result TransitionToCancelled(
        BookingStatus expectedCurrentStatus,
        BookingCancellationReason cancellationReason,
        DateTimeOffset cancelledAtUtc)
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

        CancelledAtUtc = cancelledAtUtc;

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
        bool transitionsToCancelled = targetStatus == BookingStatus.Cancelled;

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
