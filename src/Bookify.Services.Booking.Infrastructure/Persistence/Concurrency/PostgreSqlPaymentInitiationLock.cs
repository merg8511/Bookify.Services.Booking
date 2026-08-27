using Bookify.Services.Booking.Application.Payments.Initiate;
using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Concurrency;

internal sealed class PostgreSqlPaymentInitiationLock
    : IPaymentInitiationLock
{
    private const string AcquireLockSql =
        """
        SELECT b.id
        FROM bookings AS b
        WHERE b.id = @BookingId
        FOR UPDATE
        """;

    private readonly BookingDbContext _dbContext;

    public PostgreSqlPaymentInitiationLock(BookingDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<bool> TryAcquireAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        DbTransaction transaction = GetRequiredTransaction();

        DbConnection connection =
            transaction.Connection ??
            throw new InvalidOperationException(
                "The current transaction does not " +
                "have an associated database conection.");

        var command =
            new CommandDefinition(
                AcquireLockSql,
                new
                {
                    BookingId = bookingId
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

        Guid? lockedBookingId = await connection.QuerySingleOrDefaultAsync<Guid?>(command);

        return lockedBookingId.HasValue;
    }

    private DbTransaction GetRequiredTransaction()
    {
        IDbContextTransaction? transaction =
            _dbContext.Database.CurrentTransaction;

        if (transaction is null)
        {
            throw new InvalidOperationException(
                "A database transaction must be active " +
                "before acquiring the payment initiation lock.");
        }

        return transaction.GetDbTransaction();
    }
}
