namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;

public sealed record GetAvailabilityResponse(
    Guid PropertyId,
    string PropertyName,
    DateOnly CheckInDate,
    DateOnly CheckOutDate,
    int NumberOfNights,
    int GuestCount,
    IReadOnlyList<
        AvailableRentableUnitResponse> AvailableUnits);
