using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Tests.Infrastructure;

internal static class PropertyTestFactory
{
    public static readonly TimeOnly DefaultCheckInTime = new(15, 0);
    public static readonly TimeOnly DefaultCheckOutTime = new(11, 0);

    public static Property CreateValidProperty(
        string name = "Rancho Costa Azul",
        string timeZoneId = "America/El_Salvador",
        TimeOnly? checkInTime = null,
        TimeOnly? checkOutTime = null,
        string ownerSubjectId = "test-owner-subject")
    {
        Result<Property> result = Property.Create(
            name,
            timeZoneId,
            checkInTime ?? DefaultCheckInTime,
            checkOutTime ?? DefaultCheckOutTime,
            ownerSubjectId);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failed to create valid property: {result.Error.Code} - {result.Error.Message}");
        }

        return result.Value;
    }
}
