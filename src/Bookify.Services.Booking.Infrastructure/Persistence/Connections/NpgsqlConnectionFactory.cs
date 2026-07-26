using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Npgsql;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Connections;

internal sealed class NpgsqlConnectionFactory
    : IDbConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlConnectionFactory(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ??
            throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await _dataSource.OpenConnectionAsync(cancellationToken);
    }
}
