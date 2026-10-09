using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Notifications;

public static class BookingNotificationErrors
{
    public static readonly Error InvalidBookingId = Error.Validation(
        "Notification.InvalidBookingId",
        "The booking identifier must not be empty.");

    public static readonly Error InvalidRecipient = Error.Validation(
        "Notification.InvalidRecipient",
        "The notification recipient must contain a valid email address.");

    public static readonly Error InvalidSubject = Error.Validation(
        "Notification.InvalidSubject",
        "The notification subject must be a non-empty single-line value.");

    public static readonly Error InvalidBody = Error.Validation(
        "Notification.InvalidBody",
        "The notification body must not be empty.");

    public static readonly Error DeliveryDisabled = Error.Failure(
        "Notification.DeliveryDisabled",
        "Email notification delivery is disabled.");

    public static Error DeliveryFailed(Guid bookingId) => Error.Failure(
        "Notification.DeliveryFailed",
        $"The notification for booking '{bookingId}' could not be delivered.");
}
