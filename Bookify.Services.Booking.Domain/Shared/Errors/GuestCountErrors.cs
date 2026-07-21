namespace Bookify.Services.Booking.Domain.Shared.Errors;

public static class GuestCountErrors
{
    public static readonly Error InvalidValue = Error.Validation(
        "GuestCount.InvalidValue",
        "The guest count must be greater than zero.");
}
