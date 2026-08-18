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

    public static readonly Error NotAvailable =
        Error.Conflict(
            "Booking.NotAvailable",
            "The requested rentable unit is not available " +
            "for the selected stay period.");

    public static Error PricingNotConfigured(Guid rentableUnitId) =>
        Error.Conflict(
            "Booking.PricingNotConfigured",
            $"The rentable unit with identifier " +
            $"'{rentableUnitId}' does not have pricing " +
            $"configured and cannot be booked");

    public static Error PropertyNotFound(
        Guid propertyId) =>
        Error.NotFound(
            "Property.NotFound",
            $"The property with identifier " +
            $"'{propertyId}' was not found.");

    public static Error PropertyInactive(
        Guid propertyId) =>
        Error.Conflict(
            "Booking.PropertyInactive",
            $"The property with identifier " +
            $"'{propertyId}' is inactive and cannot receive new bookings.");

    public static Error RentableUnitNotFound(
        Guid rentableUnitId) =>
        Error.NotFound(
            "RentableUnit.NotFound",
            $"The rentable unit with identifier " +
            $"'{rentableUnitId}' was not found.");

    public static Error RentableUnitPropertyMismatch(
        Guid rentableUnitId,
        Guid propertyId) =>
        Error.Validation(
            "Booking.RentableUnitPropertyMismatch",
            $"The rentable unit with identifier " +
            $"'{rentableUnitId}' does not belong to " +
            $"the property with identifier " +
            $"'{propertyId}'");
}
