using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Notifications;

public interface IBookingNotificationSender
{
    Task<Result> SendAsync(
        BookingNotificationMessage message,
        CancellationToken cancellationToken = default);
}
