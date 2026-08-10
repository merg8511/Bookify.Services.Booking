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

    public async Task CreateAsync(
        IdempotencyRequestContext context,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        IdempotencyRequest request =
            IdempotencyRequest.Create(
                context.Key,
                context.HttpMethod,
                context.Endpoint,
                context.RequestHash,
                createdAt,
                expiresAt);

        _dbContext.IdempotencyRequests.Add(request);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestartAsync(
        IdempotencyRequestContext context,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        IdempotencyRequest request =
            await GetRequiredTrackedAsync(context, cancellationToken);

        request.Restart(
            context.RequestHash,
            createdAt,
            expiresAt);

        await _dbContext.SaveChangesAsync(cancellationToken);
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

        return request ?? throw new InvalidOperationException("The idempotency request was not found.");
    }




}
