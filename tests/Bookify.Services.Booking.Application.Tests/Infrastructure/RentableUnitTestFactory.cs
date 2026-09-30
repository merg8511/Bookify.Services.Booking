using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.Tests.Infrastructure;

internal static class RentableUnitTestFactory
{
    public static RentableUnit CreateValidRentableUnit(
        Guid? propertyId = null,
        string name = "Room A",
        RentableUnitType type = RentableUnitType.Room,
        int maximumCapacity = 4,
        int maxBaseGuests = 1)
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

    public static RentableUnit CreateRentableUnitWithPricing(
        Guid? propertyId = null,
        string name = "Room A",
        RentableUnitType type = RentableUnitType.Room,
        int maximumCapacity = 4,
        int maxBaseGuests = 1,
        decimal price = 100m,
        decimal weekendPrice = 140m,
        decimal extraGuestPrice = 25m,
        string currency = "USD")
    {
        RentableUnit unit = CreateValidRentableUnit(propertyId, name, type, maximumCapacity, maxBaseGuests);
        unit.ConfigurePricing(CreatePricing(price, weekendPrice, extraGuestPrice, currency));
        return unit;
    }

    public static RentableUnitPricing CreatePricing(
        decimal price = 100m,
        decimal weekendPrice = 140m,
        decimal extraGuestPrice = 25m,
        string currency = "USD")
    {
        Result<RentableUnitPricing> result = RentableUnitPricing.Create(
            Money.Create(price, currency).Value,
            Money.Create(weekendPrice, currency).Value,
            Money.Create(extraGuestPrice, currency).Value);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to create pricing: {result.Error.Code} - {result.Error.Message}");
        }

        return result.Value;
    }
}
