using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Availability.Get;

public sealed class GetAvailabilityQueryValidator :
    IRequestValidator<GetAvailabilityQuery>
{
    public Result Validate(GetAvailabilityQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PropertyId == Guid.Empty)
        {
            return Result.Failure(
                GetAvailabilityErrors.InvalidPropertyId);
        }

        if (request.CheckInDate is null)
        {
            return Result.Failure(
                GetAvailabilityErrors.CheckInDateRequired);
        }

        if (request.CheckOutDate is null)
        {
            return Result.Failure(
                GetAvailabilityErrors.CheckOutDateRequired);
        }

        if (request.GuestCount is null)
        {
            return Result.Failure(
                GetAvailabilityErrors.GuestCountRequired);
        }

        if (request.GuestCount <= 0)
        {
            return Result.Failure(
                GetAvailabilityErrors.InvalidGuestCount);
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

        return Result.Success();
    }
}
