using Bookify.Services.Booking.Application.Abstractions.Idempotency;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Idempotency;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Bookify.Services.Booking.Integration.Tests.Idempotency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Concurrency")]
public sealed class IdempotencyConcurrencyTests
{
    private const int ConcurrentRequestCount = 20;

    private readonly BookingApiFactory _factory;

    public IdempotencyConcurrencyTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BeginAsync_WhenTwentyRequestsUseSameKey_AllowsExactlyOneExecution()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string key =
            $"idempotency-{Guid.NewGuid():N}";

        var context =
            new IdempotencyRequestContext(
                CallerScope: new string('A', 64),
                Key: key,
                HttpMethod: "POST",
                Endpoint: "/api/v1/bookings",
                RequestHash: "HASH-A");

        var startGate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<Result<IdempotencyProcessingResult>>[] tasks =
            Enumerable
                .Range(
                    0,
                    ConcurrentRequestCount)
                .Select(_ =>
                    BeginInNewScopeAsync(
                        context,
                        startGate.Task,
                        cancellationToken))
                .ToArray();

        // Act
        startGate.SetResult(true);

        Result<IdempotencyProcessingResult>[] results =
            await Task.WhenAll(tasks);

        // Assert - Application result
        Result<IdempotencyProcessingResult> execute =
            Assert.Single(
                results,
                result =>
                    result.IsSuccess &&
                    result.Value.Action ==
                        IdempotencyProcessingAction.Execute);

        Assert.True(
            execute.IsSuccess);

        Result<IdempotencyProcessingResult>[] conflicts =
            results
                .Where(result => result.IsFailure)
                .ToArray();

        Assert.Equal(
            ConcurrentRequestCount - 1,
            conflicts.Length);

        Assert.All(
            conflicts,
            conflict =>
                Assert.Equal(
                    IdempotencyErrors.RequestInProgress,
                    conflict.Error));

        // Assert - PostgreSQL
        long storedRequests =
            await CountAsync(
                context,
                cancellationToken);

        Assert.Equal(
            1,
            storedRequests);
    }

    [Fact]
    public async Task BeginAsync_WhenExpiredCompletedKeyIsReclaimedConcurrently_AllowsExactlyOneExecution()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string key =
            $"expired-{Guid.NewGuid():N}";

        var context =
            new IdempotencyRequestContext(
                CallerScope: new string('A', 64),
                Key: key,
                HttpMethod: "POST",
                Endpoint: "/api/v1/bookings",
                RequestHash: "NEW-HASH");

        await InsertExpiredCompletedAsync(
            context,
            cancellationToken);

        var startGate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<Result<IdempotencyProcessingResult>> firstTask =
            BeginInNewScopeAsync(
                context,
                startGate.Task,
                cancellationToken);

        Task<Result<IdempotencyProcessingResult>> secondTask =
            BeginInNewScopeAsync(
                context,
                startGate.Task,
                cancellationToken);

        // Act
        startGate.SetResult(true);

        Result<IdempotencyProcessingResult>[] results =
            await Task.WhenAll(
                firstTask,
                secondTask);

        // Assert
        Assert.Single(
            results,
            result =>
                result.IsSuccess &&
                result.Value.Action ==
                    IdempotencyProcessingAction.Execute);

        Result<IdempotencyProcessingResult> conflict =
            Assert.Single(
                results,
                result => result.IsFailure);

        Assert.Equal(
            IdempotencyErrors.RequestInProgress,
            conflict.Error);

        long storedRequests =
            await CountAsync(
                context,
                cancellationToken);

        Assert.Equal(
            1,
            storedRequests);

        StoredRow row =
            await GetStoredRowAsync(
                context,
                cancellationToken);

        Assert.Equal(
            "NEW-HASH",
            row.RequestHash);

        Assert.Equal(
            "InProgress",
            row.Status);
    }

    private async Task<Result<IdempotencyProcessingResult>>
        BeginInNewScopeAsync(
            IdempotencyRequestContext context,
            Task startGate,
            CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(
            cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IIdempotencyProcessor processor =
            scope.ServiceProvider
                .GetRequiredService<IIdempotencyProcessor>();

        return await processor.BeginAsync(
            context,
            cancellationToken);
    }

    private async Task<long> CountAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var command =
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM idempotency_requests
                WHERE http_method = @HttpMethod
                  AND caller_scope = @CallerScope
                  AND endpoint = @Endpoint
                  AND key = @Key;
                """,
                new
                {
                    context.CallerScope,
                    context.HttpMethod,
                    context.Endpoint,
                    context.Key
                },
                cancellationToken:
                    cancellationToken);

        return await connection
            .ExecuteScalarAsync<long>(
                command);
    }

    private async Task InsertExpiredCompletedAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        DateTimeOffset createdAt =
            DateTimeOffset.UtcNow.AddDays(-2);

        DateTimeOffset expiresAt =
            DateTimeOffset.UtcNow.AddDays(-1);

        var command =
            new CommandDefinition(
                """
                INSERT INTO idempotency_requests
                (
                    id,
                    caller_scope,
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
                    @Id,
                    @CallerScope,
                    @Key,
                    @HttpMethod,
                    @Endpoint,
                    'OLD-HASH',
                    'Completed',
                    201,
                    '{}',
                    @CreatedAt,
                    @ExpiresAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    context.CallerScope,
                    context.Key,
                    context.HttpMethod,
                    context.Endpoint,
                    CreatedAt = createdAt,
                    ExpiresAt = expiresAt
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            command);
    }

    private async Task<StoredRow> GetStoredRowAsync(
        IdempotencyRequestContext context,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var command =
            new CommandDefinition(
                """
                SELECT
                    request_hash AS "RequestHash",
                    status AS "Status"
                FROM idempotency_requests
                WHERE http_method = @HttpMethod
                  AND caller_scope = @CallerScope
                  AND endpoint = @Endpoint
                  AND key = @Key;
                """,
                new
                {
                    context.CallerScope,
                    context.HttpMethod,
                    context.Endpoint,
                    context.Key
                },
                cancellationToken:
                    cancellationToken);

        return await connection
            .QuerySingleAsync<StoredRow>(
                command);
    }

    private sealed record StoredRow(
        string RequestHash,
        string Status);
}
