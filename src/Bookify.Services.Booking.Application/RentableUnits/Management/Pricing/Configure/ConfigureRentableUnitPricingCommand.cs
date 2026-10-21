using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Configure;

public sealed record ConfigureRentableUnitPricingCommand(
    Guid PropertyId,
    Guid UnitId,
    decimal RegularNightlyRate,
    decimal WeekendNightlyRate,
    decimal ExtraGuestNightlyRate,
    string Currency) : ICommand;
