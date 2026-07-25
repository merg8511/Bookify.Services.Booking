namespace Bookify.Services.Booking.Api.Endpoints.Properties.Create;

public sealed record CreatePropertyRequest(
    string Name,
    string TimeZoneId,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime);


