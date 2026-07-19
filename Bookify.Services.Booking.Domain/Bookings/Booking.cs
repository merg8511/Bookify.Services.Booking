using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings
{
    public sealed class Booking
    {
        private Booking(
            Guid id,
            Guid propertyId,
            Guid rentableUnitId,
            StayPeriod stayPeriod,
            int guestCount,
            BookingStatus status)
        {
            Id = id;
            PropertyId = propertyId;
            RentableUnitId = rentableUnitId;
            StayPeriod = stayPeriod;
            GuestCount = guestCount;
            Status = status;
        }

        public Guid Id { get; }
        public Guid PropertyId { get; }
        public Guid RentableUnitId { get; }
        public StayPeriod StayPeriod { get; }
        public int GuestCount { get; }
        public BookingStatus Status { get; private set; }
        public BookingCancellationReason? CancellationReason { get; private set; }

        public bool BlocksInvetory =>
            Status is BookingStatus.PendingApproval
                or BookingStatus.PendingPayment
                or BookingStatus.Paid
                or BookingStatus.Completed;

        public static Result<Booking> Create(
            RentableUnit rentableUnit,
            StayPeriod stayPeriod,
            int guestCount)
        {
            ArgumentNullException.ThrowIfNull(rentableUnit);
            ArgumentNullException.ThrowIfNull(stayPeriod);

            if (!rentableUnit.IsActive)
                return Result<Booking>.Failure(
                    BookingErrors.RentableUnitInactive);

            if (guestCount <= 0)
                return Result<Booking>.Failure(
                    BookingErrors.InvalidGuestCount);

            if (!rentableUnit.CanAccommodate(guestCount))
                return Result<Booking>.Failure(
                    BookingErrors.GuestCapacityExceeded);

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
                return Result.Failure(
                    BookingErrors.InvalidStatusTransition(
                        Status,
                        targetStatus));

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
                throw new InvalidOperationException(
                    "A transition to Cancelled must include a cancellation reason.");

            if (!transitionsToCancelled && cancellationReason is not null)
                throw new InvalidOperationException(
                    "A cancellation reason can only be assigned when transitioning to Cancelled.");
        }
    }
}
