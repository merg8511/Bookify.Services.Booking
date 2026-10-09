using System.Collections.Concurrent;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

public sealed class FakeBookingNotificationSender
    : IBookingNotificationSender
{
    private readonly ConcurrentQueue<
        BookingNotificationMessage>
        _sentMessages = new();

    public IReadOnlyCollection<
        BookingNotificationMessage>
        SentMessages =>
            _sentMessages.ToArray();

    public Error? ErrorToReturn
    {
        get;
        set;
    }

    public Task<Result> SendAsync(
        BookingNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        cancellationToken.ThrowIfCancellationRequested();

        Error? error =
            ErrorToReturn;

        if (error is not null)
        {
            return Task.FromResult(
                Result.Failure(error));
        }

        _sentMessages.Enqueue(
            message);

        return Task.FromResult(
            Result.Success());
    }

    public void Reset()
    {
        while (_sentMessages.TryDequeue(out _))
        {
        }

        ErrorToReturn = null;
    }
}
