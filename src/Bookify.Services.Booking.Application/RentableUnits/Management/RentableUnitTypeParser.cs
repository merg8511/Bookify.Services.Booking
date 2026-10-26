using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management;

public static class RentableUnitTypeParser
{
    public static Result<RentableUnitType> Parse(string? value)
    {
        if (value is null ||
            !Enum.TryParse(value, ignoreCase: false, out RentableUnitType type) ||
            !Enum.IsDefined(type) ||
            !string.Equals(value, type.ToString(), StringComparison.Ordinal))
        {
            return Result<RentableUnitType>.Failure(RentableUnitErrors.InvalidType);
        }

        return Result<RentableUnitType>.Success(type);
    }
}
