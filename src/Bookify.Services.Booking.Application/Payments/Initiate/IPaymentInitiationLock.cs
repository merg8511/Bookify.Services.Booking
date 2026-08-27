namespace Bookify.Services.Booking.Application.Payments.Initiate;

public interface IPaymentInitiationLock
{
    Task<bool> TryAcquireAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
