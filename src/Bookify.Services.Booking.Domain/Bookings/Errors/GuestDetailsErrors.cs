using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Bookings.Errors;

public static class GuestDetailsErrors
{
    public static readonly Error FullNameRequired =
        Error.Validation(
            "Booking.GuestFullNameRequired",
            "The guest full name is required.");

    public static readonly Error FullNameTooLong =
        Error.Validation(
            "Booking.GuestFullNameTooLong",
            "The guest full name cannot exceed 200 charracters.");

    public static readonly Error EmailRequired =
        Error.Validation(
            "Booking.GuestEmailInvalid",
            "The guest email is not valid.");

    public static readonly Error EmailTooLong =
        Error.Validation(
            "Booking.GuestEmailTooLong",
            "The guest email cannot exceed 254 characters.");

    public static readonly Error EmailInvalid =
       Error.Validation(
           "Booking.GuestEmailInvalid",
           "The guest email is not valid.");

    public static readonly Error PhoneRequired =
        Error.Validation(
            "Booking.GuestPhoneRequired",
            "The guest phone is required.");

    public static readonly Error PhoneInvalid =
        Error.Validation(
            "Booking.GuestPhoneInvalid",
            "The guest phone must contain btween 7 and 15 digits and may optionally start with '+'.");
}
