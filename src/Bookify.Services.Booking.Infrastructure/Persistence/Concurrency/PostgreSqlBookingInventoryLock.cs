using Bookify.Services.Booking.Application.Bookings.Create;
using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Concurrency;

internal sealed class PostgreSqlBookingInventoryLock : IBookingInventoryLock
{
    private const string AcquireLockSql =
        """
        SELECT p.id
        FROM properties AS p
        WHERE p.id = @PropertyId
        FOR UPDATE
        """;

    private readonly BookingDbContext _dbContext;

    public PostgreSqlBookingInventoryLock(BookingDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<bool> TryAcquireAsync(
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        DbTransaction transaction = GetRequiredTransaction();

        DbConnection connection = transaction.Connection ??
            throw new InvalidOperationException("The current transaction does not " +
            "have an associated database connection.");

        var command =
            new CommandDefinition(
                AcquireLockSql,
                new
                {
                    PropertyId = propertyId
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

        Guid? lockedPropertyId = await connection.QuerySingleOrDefaultAsync<Guid?>(command);

        return lockedPropertyId.HasValue;
    }

    private DbTransaction GetRequiredTransaction()
    {
        IDbContextTransaction? transaction = _dbContext.Database.CurrentTransaction;

        if (transaction is null)
        {
            throw new InvalidOperationException(
                "A database transaction must be active " +
                "before acquiring the booking inventory lock.");
        }

        return transaction.GetDbTransaction();
    }
}
