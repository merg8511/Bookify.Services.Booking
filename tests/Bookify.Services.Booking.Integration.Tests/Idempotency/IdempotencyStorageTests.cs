using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data.Common;

namespace Bookify.Services.Booking.Integration.Tests.Idempotency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class IdempotencyStorageTests
{
    private static readonly string DefaultCallerScope =
        new('A', 64);

    private static readonly string AlternateCallerScope =
        new('B', 64);

    private readonly BookingApiFactory _factory;

    public IdempotencyStorageTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Database_AllowsSingleIdempotencyRequest()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string key =
            $"idempotency-{Guid.NewGuid():N}";

        await InsertAsync(
            key,
            DefaultCallerScope,
            cancellationToken);

        long count =
            await CountAsync(
                key,
                cancellationToken);

        Assert.Equal(
            1,
            count);
    }

    [Fact]
    public async Task Database_RejectsDuplicateKeyWithinSameCallerMethodAndEndpoint()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string key =
            $"idempotency-{Guid.NewGuid():N}";

        await InsertAsync(
            key,
            DefaultCallerScope,
            cancellationToken);

        async Task Action() =>
            await InsertAsync(
                key,
                DefaultCallerScope,
                cancellationToken);

        await Assert.ThrowsAsync<
            PostgresException>(
                Action);
    }

    [Fact]
    public async Task Database_AllowsSameKeyForDifferentEndpoint()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string key =
            $"idempotency-{Guid.NewGuid():N}";

        await InsertAsync(
            key,
            DefaultCallerScope,
            cancellationToken);

        await InsertAsync(
            key,
            DefaultCallerScope,
            cancellationToken,
            endpoint:
                "/api/v1/payments");

        long count =
            await CountAsync(
                key,
                cancellationToken);

        Assert.Equal(
            2,
            count);
    }

    [Fact]
    public async Task Database_AllowsSameKeyForDifferentCallerScope()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string key =
            $"idempotency-{Guid.NewGuid():N}";

        await InsertAsync(
            key,
            DefaultCallerScope,
            cancellationToken);

        await InsertAsync(
            key,
            AlternateCallerScope,
            cancellationToken);

        long count =
            await CountAsync(
                key,
                cancellationToken);

        Assert.Equal(
            2,
            count);
    }

    private async Task InsertAsync(
        string key,
        string callerScope,
        CancellationToken cancellationToken,
        string endpoint =
            "/api/v1/bookings")
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        DateTimeOffset createdAt =
            DateTimeOffset.UtcNow;

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
                    'POST',
                    @Endpoint,
                    @RequestHash,
                    'InProgress',
                    NULL,
                    NULL,
                    @CreatedAt,
                    @ExpiresAt
                );
                """,
                new
                {
                    Id =
                        Guid.NewGuid(),

                    CallerScope =
                        callerScope,

                    Key =
                        key,

                    Endpoint =
                        endpoint,

                    RequestHash =
                        Guid.NewGuid()
                            .ToString("N"),

                    CreatedAt =
                        createdAt,

                    ExpiresAt =
                        createdAt.AddHours(24)
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            command);
    }

    private async Task<long> CountAsync(
        string key,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var command =
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM idempotency_requests
                WHERE key = @Key;
                """,
                new
                {
                    Key = key
                },
                cancellationToken:
                    cancellationToken);

        return await connection
            .ExecuteScalarAsync<long>(
                command);
    }
}
