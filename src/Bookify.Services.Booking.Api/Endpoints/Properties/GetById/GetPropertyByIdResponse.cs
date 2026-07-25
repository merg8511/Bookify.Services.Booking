namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetById;

public sealed record GetPropertyByIdResponse(
    Guid Id,
    string Name,
    string TimeZoneId,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime,
    bool IsActive);
