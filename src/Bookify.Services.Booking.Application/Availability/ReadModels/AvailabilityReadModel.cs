namespace Bookify.Services.Booking.Application.Availability.ReadModels;

public sealed record AvailabilityReadModel(
       Guid PropertyId,
       string PropertyName,
       DateOnly CheckInDate,
       DateOnly CheckOutDate,
       int NumberOfNights,
       int GuestCount,
       IReadOnlyList<
           AvailableRentableUnitReadModel> AvailableUnits);
