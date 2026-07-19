using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;

namespace Bookify.Services.Booking.Domain.Bookings.Services
{
    public static class BookingConflictPolicy
    {
        public static bool HasConflict(
            RentableUnit requestedUnit,
            StayPeriod requestedPeriod,
            Booking existingBooking,
            RentableUnit existingUnit)
        {
            ArgumentNullException.ThrowIfNull(requestedUnit);
            ArgumentNullException.ThrowIfNull(requestedPeriod);
            ArgumentNullException.ThrowIfNull(existingBooking);
            ArgumentNullException.ThrowIfNull(existingUnit);

            EnsureBookingMatchesUnit(
                existingBooking,
                existingUnit);

            if (!existingBooking.BlocksInvetory)
                return false;

            if (!requestedPeriod.Overlaps(existingBooking.StayPeriod))
                return false;

            return requestedUnit.SharesInventoryWith(existingUnit);
        }

        private static void EnsureBookingMatchesUnit(
            Booking booking,
            RentableUnit unit)
        {
            bool hasMatchingUnit = booking.RentableUnitId == unit.Id;
            bool hasMatchingProperty = booking.PropertyId == unit.PropertyId;

            if (!hasMatchingUnit || !hasMatchingProperty)
                throw new InvalidOperationException(
                    "The existing booking does not belong to the provided rentable unit.");
        }
    }
}
