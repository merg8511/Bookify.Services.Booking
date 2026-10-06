using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;

namespace Bookify.Services.Booking.Integration.Tests.Idempotency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Idempotency")]
public sealed class IdempotencyCallerScopeTests
{
    private readonly BookingApiFactory _factory;

    public IdempotencyCallerScopeTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateBooking_SameKeyAndPayloadFromDifferentCustomers_ShouldNotReplayOtherCustomerResponse()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        SeedData seed =
            await BookingDatabaseTestSeeder
                .SeedPropertyWithRoomAsync(
                    _factory,
                    propertyName:
                        $"Caller Scope Test {Guid.NewGuid():N}",
                    cancellationToken:
                        cancellationToken);

        CreateBookingRequest request =
            BookingRequestTestFactory
                .CreateBookingRequest(
                    propertyId:
                        seed.PropertyId,

                    rentableUnitId:
                        seed.RentableUnitId,

                    checkInDate:
                        new DateOnly(
                            2026,
                            12,
                            10),

                    checkOutDate:
                        new DateOnly(
                            2026,
                            12,
                            12),

                    guestCount:
                        2);

        string key =
            $"caller-scope-{Guid.NewGuid():N}";

        string firstSubject =
            $"customer-a-{Guid.NewGuid():N}";

        string secondSubject =
            $"customer-b-{Guid.NewGuid():N}";

        using HttpClient firstClient =
            _factory.CreateCustomerClient(
                firstSubject);

        using HttpClient secondClient =
            _factory.CreateCustomerClient(
                secondSubject);

        // Act
        using HttpResponseMessage first =
            await SendAsync(
                firstClient,
                request,
                key,
                cancellationToken);

        using HttpResponseMessage second =
            await SendAsync(
                secondClient,
                request,
                key,
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.Created,
            first.StatusCode);

        // Customer B must execute its own operation.
        // Because customer A already reserved the inventory,
        // the real business result is a conflict.
        Assert.Equal(
            HttpStatusCode.Conflict,
            second.StatusCode);

        ProblemDetailsResponse? problem =
            await second.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        cancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(
            "Booking.NotAvailable",
            problem.Code);

        string[] scopes =
            await LoadCallerScopesAsync(
                key,
                cancellationToken);

        Assert.Equal(
            2,
            scopes.Length);

        Assert.Equal(
            2,
            scopes
                .Distinct(
                    StringComparer.Ordinal)
                .Count());

        Assert.All(
            scopes,
            scope =>
            {
                Assert.Equal(
                    64,
                    scope.Length);

                Assert.DoesNotContain(
                    firstSubject,
                    scope,
                    StringComparison.Ordinal);

                Assert.DoesNotContain(
                    secondSubject,
                    scope,
                    StringComparison.Ordinal);
            });
    }

    private static async Task<HttpResponseMessage>
        SendAsync(
            HttpClient client,
            CreateBookingRequest request,
            string idempotencyKey,
            CancellationToken cancellationToken)
    {
        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/bookings");

        message.Headers.Add(
            "Idempotency-Key",
            idempotencyKey);

        message.Content =
            JsonContent.Create(
                request);

        return await client.SendAsync(
            message,
            cancellationToken);
    }

    private async Task<string[]>
        LoadCallerScopesAsync(
            string key,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IDbConnectionFactory connectionFactory =
            scope.ServiceProvider
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        IEnumerable<string> callerScopes =
            await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT caller_scope
                    FROM idempotency_requests
                    WHERE key = @Key
                    ORDER BY caller_scope;
                    """,
                    new
                    {
                        Key = key
                    },
                    cancellationToken:
                        cancellationToken));

        return callerScopes.ToArray();
    }
}
