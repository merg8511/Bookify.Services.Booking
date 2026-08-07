using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.Bookings.Create;

public class CreateBookingCommandValidator :
    IRequestValidator<CreateBookingCommand>
{
    public Result Validate(CreateBookingCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(
                CreateBookingErrors.InvalidPropertyId);
        }

        if (request.RentableUnitId == Guid.Empty)
        {
            return Result.Failure(
                CreateBookingErrors.InvalidRentableUnitId);
        }

        if (request.CheckInDate is null)
        {
            return Result.Failure(
                CreateBookingErrors.CheckInDateRequired);
        }

        if (request.CheckOutDate is null)
        {
            return Result.Failure(
                CreateBookingErrors.CheckOutDateRequired);
        }

        if (request.GuestCount is null)
        {
            return Result.Failure(
                CreateBookingErrors.GuestCountRequired);
        }

        Result<StayPeriod> stayPeriodResult =
            StayPeriod.Create(
                request.CheckInDate.Value,
                request.CheckOutDate.Value);

        if (stayPeriodResult.IsFailure)
        {
            return Result.Failure(
                stayPeriodResult.Error);
        }

        Result<GuestCount> guestCountResult =
            GuestCount.Create(
                request.GuestCount.Value);

        if (guestCountResult.IsFailure)
        {
            return Result.Failure(
                guestCountResult.Error);
        }

        return Result.Success();
    }
}
