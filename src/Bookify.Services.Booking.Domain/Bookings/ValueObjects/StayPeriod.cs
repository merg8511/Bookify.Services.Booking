using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.ValueObjects;

public sealed record StayPeriod
{
    private StayPeriod()
    {
    }

    private StayPeriod(
        DateOnly checkInDate,
        DateOnly checkOutDate)
    {
        CheckInDate = checkInDate;
        CheckOutDate = checkOutDate;

    }

    public DateOnly CheckInDate { get; private set; }
    public DateOnly CheckOutDate { get; private set; }

    public int NumberOfNights =>
        CheckOutDate.DayNumber - CheckInDate.DayNumber;

    public static Result<StayPeriod> Create(
        DateOnly checkInDate,
        DateOnly checkOutDate)
    {
        if (checkOutDate <= checkInDate)
        {
            return Result<StayPeriod>.Failure(
                StayPeriodErrors.InvalidDateRange);
        }

        return Result<StayPeriod>.Success(
            new StayPeriod(
                checkInDate,
                checkOutDate));
    }

    public bool Overlaps(StayPeriod other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return CheckInDate < other.CheckOutDate
            && CheckOutDate > other.CheckInDate;
    }
}
