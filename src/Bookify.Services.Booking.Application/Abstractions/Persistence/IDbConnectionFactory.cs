using System.Data.Common;

namespace Bookify.Services.Booking.Application.Abstractions.Persistence;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default);
}
