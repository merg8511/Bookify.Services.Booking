namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetPaged;

public sealed record PropertyListItemResponse(
    Guid Id,
    string Name,
    bool IsActive);
