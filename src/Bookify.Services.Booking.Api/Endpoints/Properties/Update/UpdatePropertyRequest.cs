using System.Text.Json.Serialization;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.Update;

public sealed record UpdatePropertyRequest(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string TimeZoneId,
    [property: JsonRequired] TimeOnly CheckInTime,
    [property: JsonRequired] TimeOnly CheckOutTime);
