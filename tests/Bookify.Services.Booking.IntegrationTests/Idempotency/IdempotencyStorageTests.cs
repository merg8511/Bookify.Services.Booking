using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data.Common;

namespace Bookify.Services.Booking.IntegrationTests.Idempotency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class IdempotencyStorageTests
{
    private readonly BookingApiFactory _factory;

    public IdempotencyStorageTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Database_AllowsSingleIdempotencyRequest()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string key = $"idempotency-{Guid.NewGuid():N}";

        // ACT
        await InsertAsync(key, cancellationToken);

        // ASSERT
        long count = await CountAsync(key, cancellationToken);

        Assert.Equal(
            1,
            count);
    }

    [Fact]
    public async Task Database_RejectsDuplicateKeyWithinSameMethodAndEndpoint()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string key = $"idempotency-{Guid.NewGuid():N}";

        await InsertAsync(key, cancellationToken);

        // ACT
        async Task Action()
        {
            await InsertAsync(key, cancellationToken);
        }

        // ASSERT
        await Assert.ThrowsAsync<PostgresException>(Action);
    }

    [Fact]
    public async Task Database_AllowsSameKeyForDifferentEndpoint()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string key = $"idempotency-{Guid.NewGuid():N}";

        // ACT
        await InsertAsync(key, cancellationToken);

        await InsertAsync(key, cancellationToken, endpoint: "/api/v1/payments");

        // ASSERT
        long count = await CountAsync(key, cancellationToken);

        Assert.Equal(
            2,
            count);
    }

    private async Task InsertAsync(
        string key,
        CancellationToken cancellationToken,
        string endpoint = "/api/v1/bookings")
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        var command =
            new CommandDefinition(
                """
                INSERT INTO idempotency_requests
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
                    @Id,
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
                    Id = Guid.NewGuid(),
                    Key = key,
                    Endpoint = endpoint,
                    RequestHash = Guid.NewGuid().ToString("N"),
                    CreatedAt = createdAt,
                    ExpiresAt = createdAt.AddHours(24)
                },
                cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    private async Task<long> CountAsync(
        string key,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

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
                cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<long>(command);
    }
}
