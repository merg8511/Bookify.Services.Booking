using Bookify.Services.Booking.Application.Abstractions.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;

internal sealed class EfCoreIdempotencyStore : IIdempotencyStore
{
    private readonly BookingDbContext _dbContext;

    public EfCoreIdempotencyStore(BookingDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<StoredIdempotencyRequest?> GetAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken = default)
    {
        return _dbContext
            .IdempotencyRequests
            .AsNoTracking()
            .Where(
                request =>
                    request.Key == context.Key &&
                    request.HttpMethod == context.HttpMethod &&
                    request.Endpoint == context.Endpoint)
            .Select(
                request =>
                    new StoredIdempotencyRequest(
                        request.RequestHash,
                        request.Status,
                        request.StatusCode,
                        request.ResponseBody,
                        request.ExpiresAt
                    ))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryClaimAsync(
        IdempotencyRequestContext context,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expiresAt, createdAt);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            Guid id = Guid.NewGuid();

            int affectedRows =
                await _dbContext.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"""
                        INSERT INTO idempotency_requests
                        AS current_request
                        (
                            id,
                            key,
                            http_method,
                            endpoint,
                            request_hash,
                            status,
                            status_code,
                            response_body,
                            created_at,
                            expires_at
                        )
                        VALUES
                        (
                            {id},
                            {context.Key},
                            {context.HttpMethod},
                            {context.Endpoint},
                            {context.RequestHash},
                            'InProgress',
                            NULL,
                            NULL,
                            {createdAt},
                            {expiresAt}
                        )
                        ON CONFLICT
                        (
                            http_method,
                            endpoint,
                            key
                        )
                        DO UPDATE SET
                            request_hash = EXCLUDED.request_hash,
                            status = 'InProgress',
                            status_code = NULL,
                            response_body = NULL,
                            created_at = EXCLUDED.created_at,
                            expires_at = EXCLUDED.expires_at
                        WHERE
                            current_request.status = 'Completed'
                                AND current_request.expires_at <= EXCLUDED.created_at;
                        """,
                        cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return affectedRows == 1;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task CompleteAsync(
        IdempotencyRequestContext context,
        int statusCode,
        string? responseBoby,
        CancellationToken cancellationToken = default)
    {
        IdempotencyRequest request =
            await GetRequiredTrackedAsync(context, cancellationToken);

        if (!string.Equals(
            request.RequestHash,
            context.RequestHash,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The idempotency request hash change " +
                "before the operation was completed.");
        }

        request.Complete(statusCode, responseBoby);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IdempotencyRequest> GetRequiredTrackedAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken)
    {
        IdempotencyRequest? request =
            await _dbContext
                .IdempotencyRequests
                .SingleOrDefaultAsync(
                    request =>
                        request.Key == context.Key &&
                        request.HttpMethod == context.HttpMethod &&
                        request.Endpoint == context.Endpoint,
                        cancellationToken);

        return request ??
            throw new InvalidOperationException("The idempotency request was not found.");
    }
}
