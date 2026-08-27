using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Bookings.Create;
using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;

namespace Bookify.Services.Booking.Infrastructure.Persistence.ReadServices;

internal sealed class DapperBookingAvailabilityReader :
    IBookingAvailabilityReader
{
    private const string HasConflictSql =
        """
        WITH requested_unit AS
        (
            SELECT
                ru.id,
                ru.property_id,
                ru.type
            FROM rentable_units AS ru
            WHERE ru.id = @RequestedRentableUnitId
                AND ru.property_id = @PropertyId
        )
        SELECT EXISTS
        (
            SELECT 1
            FROM requested_unit
            INNER JOIN bookings AS b
                ON b.property_id = requested_unit.property_id
            INNER JOIN rentable_units
                AS existing_unit
                ON existing_unit.id = b.rentable_unit_id
                AND existing_unit.property_id = b.property_id
            WHERE b.status IN
            (
                'PendingApproval',
                'PendingPayment',
                'Paid',
                'Completed'
            )
            AND b.check_in_date < @RequestedCheckOutDate
            AND b.check_out_date > @RequestedCheckInDate
            AND
            (
                existing_unit.id = requested_unit.id
                    OR existing_unit.type = 'EntireProperty'
                    OR requested_unit.type = 'EntireProperty'
            )
        )
        """;

    private readonly BookingDbContext _dbContext;

    public DapperBookingAvailabilityReader(BookingDbContext dbContext)
    {
        _dbContext = dbContext ?? throw
            new ArgumentNullException(nameof(dbContext));
    }

    public async Task<bool> HasConflictAsync(
        Guid propertyId,
        Guid requestedRentableUnitId,
        DateOnly requestedCheckInDate,
        DateOnly requestedCheckOutDate,
        CancellationToken cancellationToken = default)
    {
        DbTransaction transaction = GetRequiredTransaction();

        DbConnection connection = transaction.Connection ??
            throw new InvalidOperationException(
                "The current transaction does not " +
                "have an associated database connection.");

        var command =
            new CommandDefinition(
                HasConflictSql,
                new
                {
                    PropertyId = propertyId,
                    RequestedRentableUnitId = requestedRentableUnitId,
                    RequestedCheckInDate = requestedCheckInDate,
                    RequestedCheckOutDate = requestedCheckOutDate
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<bool>(command);
    }

    private DbTransaction GetRequiredTransaction()
    {
        IDbContextTransaction? transaction = _dbContext.Database.CurrentTransaction;

        if (transaction is null)
        {
            throw new InvalidOperationException(
                "A database transaction must be active " +
                "before checking booking availability.");
        }

        return transaction.GetDbTransaction();
    }
}
