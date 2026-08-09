namespace Bookify.Services.Booking.Application.Abstractions.Persistence;

public interface ITransactionManager
{
    Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
}
