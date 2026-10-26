using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Properties;
using Dapper;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperPropertyOwnershipReadService : IPropertyOwnershipReadService
{
    private const string GetOwnerSubjectIdSql =
        """
        SELECT
            p.owner_subject_id
        FROM properties AS p
        WHERE p.id = @PropertyId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPropertyOwnershipReadService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<string?> GetOwnerSubjectIdAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        await using DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            GetOwnerSubjectIdSql,
            new
            {
                PropertyId = propertyId
            },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<string>(command);
    }
}
