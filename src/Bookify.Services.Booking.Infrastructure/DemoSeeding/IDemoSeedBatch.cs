using Bookify.Services.Booking.Infrastructure.Persistence;

namespace Bookify.Services.Booking.Infrastructure.DemoSeeding;

public interface IDemoSeedBatch
{
    string Key { get; }
    Task ExecuteAsync(
        BookingDbContext dbContext,
        CancellationToken cancellationToken = default);
}
