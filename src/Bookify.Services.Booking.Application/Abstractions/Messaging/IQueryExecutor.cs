using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Messaging;

public interface IQueryExecutor<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(
        TQuery query,
        CancellationToken cancellationToken = default);
}
