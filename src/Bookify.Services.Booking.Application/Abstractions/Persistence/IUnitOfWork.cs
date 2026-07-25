namespace Bookify.Services.Booking.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
