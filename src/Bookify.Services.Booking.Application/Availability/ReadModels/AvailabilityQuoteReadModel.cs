namespace Bookify.Services.Booking.Application.Availability.ReadModels;

public sealed record AvailabilityQuoteReadModel(
    decimal AccommodationPrice,
    decimal ExtraGuestPrice,
    decimal TotalPrice,
    string Currency);
