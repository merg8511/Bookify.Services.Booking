using Bookify.Services.Booking.Application.Abstractions.Idempotency;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Idempotency;

public interface IIdempotencyProcessor
{
    Task<Result<IdempotencyProcessingResult>> BeginAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        IdempotencyRequestContext context,
        int statusCode,
        string? responseBody,
        CancellationToken cancellationToken = default);
}
