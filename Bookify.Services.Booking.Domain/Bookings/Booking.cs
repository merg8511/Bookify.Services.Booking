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
    }
}
