using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Bookify.Services.Booking.Application.Messaging;

public sealed class QueryExecutor<TQuery, TResponse>
    : IQueryExecutor<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    private readonly IQueryHandler<
        TQuery,
        TResponse> _handler;

    private readonly IEnumerable<
        IRequestValidator<TQuery>> _validators;

    private readonly ILogger<
        QueryExecutor<TQuery, TResponse>> _logger;

    public QueryExecutor(
        IQueryHandler<TQuery, TResponse> handler,
        IEnumerable<IRequestValidator<TQuery>> validators,
        ILogger<QueryExecutor<TQuery, TResponse>> logger)
    {
        _handler = handler;
        _validators = validators.ToArray();
        _logger = logger;
    }

    public async Task<Result<TResponse>> ExecuteAsync(
        TQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        string queryName = typeof(TQuery).Name;

        long startedAt = Stopwatch.GetTimestamp();

        Result validationResult =
            RequestValidation.Validate(
                query,
                _validators);

        if (validationResult.IsFailure)
        {
            Result<TResponse> failureResult =
                Result<TResponse>.Failure(
                    validationResult.Error);

            LogCompleted(
                queryName,
                failureResult,
                startedAt);

            return failureResult;
        }

        Result<TResponse> result = await _handler.HandleAsync(query, cancellationToken);

        LogCompleted(
            queryName,
            result,
            startedAt);

        return result;
    }

    private void LogCompleted(
        string queryName,
        Result<TResponse> result,
        long startedAt)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);

        string? errorCode = result.IsFailure
                                ? result.Error.Code
                                : null;

        _logger.LogInformation(
            "Completed query {QueryName}. " +
            "Success: {IsSuccess}. " +
            "ErrorCode: {ErrorCode}. " +
            "DurationMs: {DurationMs}",
            queryName,
            result.IsSuccess,
            errorCode,
            elapsed.TotalMicroseconds);
    }
}

