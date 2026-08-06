using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Create;

public static class CreateBookingErrors
{
    public static readonly Error InvalidPropertyId =
        Error.Validation(
            "Booking.InvalidPropertyId",
            "The property identifier cannot be empty.");

    public static readonly Error InvalidRentableUnitId =
        Error.Validation(
            "Booking.InvalidRentableUnitId",
            "The rentable unit identifier cannot be empty.");

    public static readonly Error CheckInDateRequired =
        Error.Validation(
            "Booking.CheckInDateRequired",
            "The check-in date is required.");

    public static readonly Error CheckOutDateRequired =
        Error.Validation(
            "Booking.CheckOutDateRequired",
            "The check-out date is required.");

    public static readonly Error GuestCountRequired =
        Error.Validation(
            "Booking.GuestCountRequired",
            "The guest count is required.");
}
