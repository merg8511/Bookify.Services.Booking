namespace Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;

public sealed record AvailabilityQuoteResponse(
    decimal AccommodationPrice,
    decimal ExtraGuestPrice,
    decimal TotalPrice,
    string Currency);
