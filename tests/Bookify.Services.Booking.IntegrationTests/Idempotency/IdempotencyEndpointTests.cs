using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Bookify.Services.Booking.IntegrationTests.Idempotency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Idempotency")]
public sealed class IdempotencyEndpointTests
{
    private const string BookingsEndpoint = "/api/v1/bookings";
    private const string IdempotencyHeader = "Idempotency-Key";
    private const int ConcurrentRequestCount = 20;

    private static readonly
        JsonSerializerOptions JsonOptions =
            new(
                JsonSerializerDefaults.Web);

    private readonly BookingApiFactory _factory;
    private readonly HttpClient _client;

    public IdempotencyEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
        _client = factory.Client;
    }

    [Fact]
    public async Task Post_WhenSameKeyAndPayloadAreRepeated_ReplaysOriginalCreatedResponse()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestProperty data =
            await SeedPropertyAsync(
                includeSecondRoom: false,
                cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RoomAId,
                Date(10),
                Date(15),
                GuestCount: 2);

        string requestBody = Serialize(request);

        string key = NewKey();

        // Act
        HttpAttempt first =
            await SendBookingAsync(
                key,
                requestBody,
                cancellationToken);

        HttpAttempt second =
            await SendBookingAsync(
                key,
                requestBody,
                cancellationToken);

        // Assert - HTTP
        Assert.Equal(
            HttpStatusCode.Created,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            second.StatusCode);

        Assert.Equal(
            "application/json",
            first.ContentType);

        Assert.Equal(
            "application/json",
            second.ContentType);

        // Replay must contain exactly the
        // response body produced by attempt #1.
        Assert.Equal(
            first.Body,
            second.Body);

        CreateBookingResponse firstResponse =
            Deserialize<
                CreateBookingResponse>(
                    first.Body);

        CreateBookingResponse secondResponse =
            Deserialize<
                CreateBookingResponse>(
                    second.Body);

        Assert.NotEqual(
            Guid.Empty,
            firstResponse.Id);

        Assert.Equal(
            firstResponse.Id,
            secondResponse.Id);

        Assert.Equal(
            "PendingApproval",
            firstResponse.Status);

        Assert.Equal(
            firstResponse.Status,
            secondResponse.Status);

        Assert.NotNull(first.Location);

        Assert.EndsWith(
            $"/api/v1/bookings/{firstResponse.Id}",
            first.Location,
            StringComparison.Ordinal);

        // Assert - Booking persistence
        long bookings =
            await CountBookingsForUnitAsync(
                data.PropertyId,
                data.RoomAId,
                cancellationToken);

        Assert.Equal(
            1,
            bookings);

        // Assert - Idempotency persistence
        long idempotencyRows =
            await CountIdempotencyRowsAsync(
                key,
                cancellationToken);

        Assert.Equal(
            1,
            idempotencyRows);

        IdempotencySnapshot snapshot = await GetIdempotencySnapshotAsync(key, cancellationToken);

        Assert.Equal(
            "Completed",
            snapshot.Status);

        Assert.Equal(
            201,
            snapshot.StatusCode);

        Assert.Equal(
            first.Body,
            snapshot.ResponseBody);
    }

    [Fact]
    public async Task Post_WhenSameKeyIsUsedWithDifferentPayload_ReturnsPayloadMismatch()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestProperty data =
            await SeedPropertyAsync(
                includeSecondRoom: true,
                cancellationToken);

        Assert.True(data.RoomBId.HasValue);

        Guid roomBId = data.RoomBId.Value;
        string key = NewKey();

        var firstRequest =
            new CreateBookingRequest(
                data.PropertyId,
                data.RoomAId,
                Date(10),
                Date(15),
                GuestCount: 2);

        var secondRequest =
            new CreateBookingRequest(
                data.PropertyId,
                roomBId,
                Date(10),
                Date(15),
                GuestCount: 2);

        // Act
        HttpAttempt first =
            await SendBookingAsync(
                key,
                Serialize(
                    firstRequest),
                cancellationToken);

        HttpAttempt second =
            await SendBookingAsync(
                key,
                Serialize(
                    secondRequest),
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.Created,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            second.StatusCode);

        Assert.Equal(
            "application/problem+json",
            second.ContentType);

        ProblemDetailsResponse problem =
            Deserialize<
                ProblemDetailsResponse>(
                    second.Body);

        Assert.Equal(
            "Idempotency.KeyPayloadMismatch",
            problem.Code);

        // Room B is intentionally compatible
        // with Room A.
        //
        // Therefore, if the second request had
        // reached CreateBooking, it could have
        // created a second Booking.
        long totalBookings =
            await CountBookingsForPropertyAsync(
                data.PropertyId,
                cancellationToken);

        Assert.Equal(
            1,
            totalBookings);

        long roomBBookings =
            await CountBookingsForUnitAsync(
                data.PropertyId,
                roomBId,
                cancellationToken);

        Assert.Equal(
            0,
            roomBBookings);

        // The original stored result must remain
        // unchanged.
        IdempotencySnapshot snapshot =
            await GetIdempotencySnapshotAsync(key, cancellationToken);

        Assert.Equal(
            "Completed",
            snapshot.Status);

        Assert.Equal(
            201,
            snapshot.StatusCode);

        Assert.Equal(
            first.Body,
            snapshot.ResponseBody);
    }

    [Fact]
    public async Task Post_WhenSameKeyAndPayloadArriveConcurrently_ExecutesBookingExactlyOnce()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestProperty data =
            await SeedPropertyAsync(
                includeSecondRoom: false,
                cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RoomAId,
                Date(16),
                Date(20),
                GuestCount: 2);

        string requestBody = Serialize(request);
        string key = NewKey();

        var startGate =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        Task<HttpAttempt>[] tasks =
            Enumerable
                .Range(
                    0,
                    ConcurrentRequestCount)
                .Select(
                    _ =>
                        SendBookingAsync(
                            key,
                            requestBody,
                            cancellationToken,
                            startGate.Task))
                .ToArray();

        // Act
        startGate.SetResult(true);

        HttpAttempt[] attempts =
            await Task.WhenAll(
                tasks);

        // Assert - every result must be part
        // of the expected idempotency protocol.
        HttpAttempt[] unexpected =
            attempts
                .Where(
                    attempt =>
                        attempt.StatusCode !=
                            HttpStatusCode.Created &&
                        attempt.StatusCode !=
                            HttpStatusCode.Conflict)
                .ToArray();

        Assert.Empty(unexpected);

        // Depending on timing:
        //
        // - requests reaching the row while the
        //   owner is InProgress get 409;
        //
        // - requests reaching it after completion
        //   get the cached 201.
        HttpAttempt[] successfulAttempts =
            attempts
                .Where(
                    attempt =>
                        attempt.StatusCode ==
                            HttpStatusCode.Created)
                .ToArray();

        Assert.NotEmpty(successfulAttempts);

        HttpAttempt canonicalResponse = successfulAttempts[0];

        CreateBookingResponse canonicalBooking =
            Deserialize<
                CreateBookingResponse>(canonicalResponse.Body);

        Assert.NotEqual(
            Guid.Empty,
            canonicalBooking.Id);

        // Every 201 must represent exactly the
        // same Booking and exact same body.
        Assert.All(
            successfulAttempts,
            attempt =>
            {
                Assert.Equal(
                    canonicalResponse.Body,
                    attempt.Body);

                CreateBookingResponse booking =
                    Deserialize<
                        CreateBookingResponse>(
                            attempt.Body);

                Assert.Equal(
                    canonicalBooking.Id,
                    booking.Id);

                Assert.Equal(
                    canonicalBooking.Status,
                    booking.Status);
            });

        HttpAttempt[] conflicts =
            attempts
                .Where(
                    attempt =>
                        attempt.StatusCode ==
                            HttpStatusCode.Conflict)
                .ToArray();

        // Any request that loses while the winner
        // is still running must fail only because
        // the same operation is in progress.
        Assert.All(
            conflicts,
            attempt =>
            {
                ProblemDetailsResponse problem =
                    Deserialize<
                        ProblemDetailsResponse>(
                            attempt.Body);

                Assert.Equal(
                    "Idempotency.RequestInProgress",
                    problem.Code);
            });

        // Assert - only one business effect.
        long bookings =
            await CountBookingsForUnitAsync(
                data.PropertyId,
                data.RoomAId,
                cancellationToken);

        Assert.Equal(
            1,
            bookings);

        // Assert - only one operation identity.
        long idempotencyRows =
            await CountIdempotencyRowsAsync(
                key,
                cancellationToken);

        Assert.Equal(
            1,
            idempotencyRows);

        IdempotencySnapshot snapshot =
            await GetIdempotencySnapshotAsync(
                key,
                cancellationToken);

        Assert.Equal(
            "Completed",
            snapshot.Status);

        Assert.Equal(
            201,
            snapshot.StatusCode);

        Assert.Equal(
            canonicalResponse.Body,
            snapshot.ResponseBody);
    }

    [Fact]
    public async Task Post_WhenOriginalBusinessResultIsConflict_ReplaysOriginalStatusAndBody()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestProperty data =
            await SeedPropertyAsync(
                includeSecondRoom: false,
                cancellationToken);

        // First create a real blocking Booking.
        var blockingRequest =
            new CreateBookingRequest(
                data.PropertyId,
                data.RoomAId,
                Date(21),
                Date(26),
                GuestCount: 2);

        HttpAttempt blockingResponse =
            await SendBookingAsync(
                NewKey(),
                Serialize(
                    blockingRequest),
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            blockingResponse.StatusCode);

        // This is another logical operation.
        string conflictKey = NewKey();

        var conflictingRequest =
            new CreateBookingRequest(
                data.PropertyId,
                data.RoomAId,
                Date(22),
                Date(25),
                GuestCount: 2);

        string requestBody = Serialize(conflictingRequest);

        // Act
        HttpAttempt first =
            await SendBookingAsync(
                conflictKey,
                requestBody,
                cancellationToken);

        HttpAttempt retry =
            await SendBookingAsync(
                conflictKey,
                requestBody,
                cancellationToken);

        // Assert - status code original.
        Assert.Equal(
            HttpStatusCode.Conflict,
            first.StatusCode);

        Assert.Equal(
            first.StatusCode,
            retry.StatusCode);

        // Assert - body original.
        Assert.Equal(
            first.Body,
            retry.Body);

        Assert.Equal(
            "application/problem+json",
            first.ContentType);

        Assert.Equal(
            "application/problem+json",
            retry.ContentType);

        ProblemDetailsResponse originalProblem =
            Deserialize<
                ProblemDetailsResponse>(
                    first.Body);

        ProblemDetailsResponse replayedProblem =
            Deserialize<
                ProblemDetailsResponse>(
                    retry.Body);

        Assert.Equal(
            "Booking.NotAvailable",
            originalProblem.Code);

        Assert.Equal(
            originalProblem.Code,
            replayedProblem.Code);

        // No second Booking was created.
        long bookings =
            await CountBookingsForUnitAsync(
                data.PropertyId,
                data.RoomAId,
                cancellationToken);

        Assert.Equal(
            1,
            bookings);

        // PostgreSQL must contain the exact
        // response that was replayed.
        IdempotencySnapshot snapshot =
            await GetIdempotencySnapshotAsync(
                conflictKey,
                cancellationToken);

        Assert.Equal(
            "Completed",
            snapshot.Status);

        Assert.Equal(
            409,
            snapshot.StatusCode);

        Assert.Equal(
            first.Body,
            snapshot.ResponseBody);
    }

    private static void ConfigurePricing(
    RentableUnit rentableUnit)
    {
        rentableUnit.ConfigurePricing(
            RentableUnitPricing.Create(
                Money.Create(
                    100m,
                    "USD")
                .Value,
                Money.Create(
                    140m,
                    "USD")
                .Value,
                Money.Create(
                    25m,
                    "USD")
                .Value)
            .Value);
    }

    private async Task<HttpAttempt>
        SendBookingAsync(
            string key,
            string requestBody,
            CancellationToken cancellationToken,
            Task? startGate = null)
    {
        if (startGate is not null)
        {
            await startGate.WaitAsync(
                cancellationToken);
        }

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                BookingsEndpoint);

        bool headerAdded =
            request.Headers
                .TryAddWithoutValidation(
                    IdempotencyHeader,
                    key);

        if (!headerAdded)
        {
            throw new InvalidOperationException(
                "The Idempotency-Key header could " +
                "not be added to the test request.");
        }

        request.Content =
            new StringContent(
                requestBody,
                Encoding.UTF8,
                "application/json");

        using HttpResponseMessage response =
            await _client.SendAsync(
                request,
                HttpCompletionOption
                    .ResponseContentRead,
                cancellationToken);

        string body =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken);

        return new HttpAttempt(
            response.StatusCode,
            response.Content
                .Headers
                .ContentType?
                .MediaType,
            body,
            response.Headers
                .Location?
                .ToString());
    }

    private async Task<TestProperty>
        SeedPropertyAsync(
            bool includeSecondRoom,
            CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Idempotency Test " +
                    $"{Guid.NewGuid():N}",
                    "America/El_Salvador",
                    new TimeOnly(
                        15,
                        0),
                    new TimeOnly(
                        11,
                        0))
                .Value;

        RentableUnit roomA =
            RentableUnit.Create(
                    property.Id,
                    "Room A",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

        RentableUnit? roomB =
            includeSecondRoom
                ? RentableUnit.Create(
                        property.Id,
                        "Room B",
                        RentableUnitType.Room,
                        maximumCapacity: 4,
                        maxBaseGuests: 2)
                    .Value
                : null;

        ConfigurePricing(roomA);

        if (roomB is not null)
        {
            ConfigurePricing(
                roomB);
        }

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository
            rentableUnitRepository =
                scope.ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(property);

        rentableUnitRepository.Add(roomA);

        if (roomB is not null)
        {
            rentableUnitRepository.Add(roomB);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new TestProperty(
            property.Id,
            roomA.Id,
            roomB?.Id);
    }

    private async Task<long>
        CountBookingsForUnitAsync(
            Guid propertyId,
            Guid rentableUnitId,
            CancellationToken cancellationToken)
    {
        return await ExecuteScalarAsync(
            """
            SELECT COUNT(*)
            FROM bookings
            WHERE property_id =
                  @PropertyId
              AND rentable_unit_id =
                  @RentableUnitId;
            """,
            new
            {
                PropertyId = propertyId,
                RentableUnitId = rentableUnitId
            },
            cancellationToken);
    }

    private async Task<long>
        CountBookingsForPropertyAsync(
            Guid propertyId,
            CancellationToken cancellationToken)
    {
        return await ExecuteScalarAsync(
            """
            SELECT COUNT(*)
            FROM bookings
            WHERE property_id =
                  @PropertyId;
            """,
            new
            {
                PropertyId = propertyId
            },
            cancellationToken);
    }

    private async Task<long>
        CountIdempotencyRowsAsync(
            string key,
            CancellationToken cancellationToken)
    {
        return await ExecuteScalarAsync(
            """
            SELECT COUNT(*)
            FROM idempotency_requests
            WHERE key =
                  @Key
              AND http_method =
                  'POST';
            """,
            new
            {
                Key = key
            },
            cancellationToken);
    }

    private async Task<
        IdempotencySnapshot>
        GetIdempotencySnapshotAsync(
            string key,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IDbConnectionFactory connectionFactory =
            scope.ServiceProvider
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var command =
            new CommandDefinition(
                """
                SELECT
                    status AS "Status",
                    status_code AS "StatusCode",
                    response_body AS "ResponseBody"
                FROM idempotency_requests
                WHERE key =
                      @Key
                  AND http_method =
                      'POST';
                """,
                new
                {
                    Key = key
                },
                cancellationToken: cancellationToken);

        return await connection
            .QuerySingleAsync<
                IdempotencySnapshot>(
                    command);
    }

    private async Task<long>
        ExecuteScalarAsync(
            string sql,
            object parameters,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IDbConnectionFactory connectionFactory =
            scope.ServiceProvider
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        var command =
            new CommandDefinition(
                sql,
                parameters,
                cancellationToken:
                    cancellationToken);

        return await connection
            .ExecuteScalarAsync<long>(
                command);
    }

    private static string Serialize(
        CreateBookingRequest request)
    {
        return JsonSerializer.Serialize(
            request,
            JsonOptions);
    }

    private static T Deserialize<T>(
        string json)
    {
        return JsonSerializer
            .Deserialize<T>(
                json,
                JsonOptions) ??
            throw new InvalidOperationException(
                $"The HTTP response could not be " +
                $"deserialized as " +
                $"'{typeof(T).Name}'.");
    }

    private static string NewKey()
    {
        return Guid.NewGuid()
            .ToString("N");
    }

    private static DateOnly Date(
        int day)
    {
        return new DateOnly(
            2026,
            9,
            day);
    }

    private sealed record TestProperty(
        Guid PropertyId,
        Guid RoomAId,
        Guid? RoomBId);

    private sealed record HttpAttempt(
        HttpStatusCode StatusCode,
        string? ContentType,
        string Body,
        string? Location);

    private sealed record
        IdempotencySnapshot(
            string Status,
            int? StatusCode,
            string? ResponseBody);
}
