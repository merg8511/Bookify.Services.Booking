using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Tests.Infrastructure;

internal static class RentableUnitTestFactory
{
    public static RentableUnit CreateValidRentableUnit(
        Guid? propertyId = null,
        string name = "Habitación principal",
        RentableUnitType type = RentableUnitType.Room,
        int maximumCapacity = 4,
        int maxBaseGuests = 2)
    {
        Result<RentableUnit> result = RentableUnit.Create(
            propertyId ?? Guid.NewGuid(),
            name,
            type,
            maximumCapacity,
            maxBaseGuests);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to create valid rentable unit: {result.Error.Code} - {result.Error.Message}");
        }

        return result.Value;
    }

    public static RentableUnit CreateEntireProperty(
        Guid? propertyId = null,
        string name = "Rancho Completo",
        int maximumCapacity = 20,
        int maxBaseGuests = 10)
    {
        return CreateValidRentableUnit(
            propertyId,
            name,
            RentableUnitType.EntireProperty,
            maximumCapacity,
            maxBaseGuests);
    }
}
