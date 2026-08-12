using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;

namespace Bookify.Services.Booking.IntegrationTests.Concurrency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Concurrency")]
public sealed class BookingConcurrencyTests
{
    private const string BookingsEndpoint = "/api/v1/bookings";
    private const int ConcurrentRequestCount = 20;
    private readonly BookingApiFactory _factory;
    private readonly HttpClient _client;

    public BookingConcurrencyTests(BookingApiFactory factory)
    {
        _factory = factory;
        _client = factory.Client;
    }

    [Fact]
    public async Task CreateBooking_WhenTwentyRequestCompeteForSameUnit_AllowsExactlyOneBooking()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestProperty data = await SeedPropertyAsync(
            includeSecondRoom: false,
            cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RoomAId,
                Date(10),
                Date(15),
                GuestCount: 2);

        // ACT
        BookingAttempt[] attempts =
            await SendConcurrentlyAsync(
                request,
                ConcurrentRequestCount,
                cancellationToken);

        // ASSERT - HTTP
        BookingAttempt created =
            Assert.Single(
                attempts,
                attempt =>
                    attempt.StatusCode == HttpStatusCode.Created);

        Assert.NotNull(created.BookingId);

        Assert.NotEqual(
            Guid.Empty,
            created.BookingId.Value);

        BookingAttempt[] conflicts =
            attempts
                .Where(
                    attempt =>
                       attempt.StatusCode == HttpStatusCode.Conflict)
                .ToArray();

        Assert.Equal(
            ConcurrentRequestCount - 1,
            conflicts.Length);

        Assert.All(
            conflicts,
            conflict =>
                Assert.Equal(
                    "Booking.NotAvailable",
                    conflict.ErrorCode));

        // ASSERT - POSTGRESQL
        long persistedBookings =
            await CountBlockingBookingsAsync(
                data.PropertyId,
                Date(10),
                Date(15),
                cancellationToken);

        Assert.Equal(
            1,
            persistedBookings);

        long roomABookings =
            await CountBlockingBookingsForUnitAsync(
                data.PropertyId,
                data.RoomAId,
                Date(10),
                Date(15),
                cancellationToken);

        Assert.Equal(
            1,
            roomABookings);

    }

    [Fact]
    public async Task CreateBooking_WhenRoomAndEntirePropertyCompete_AllowsExactlyOneBooking()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestProperty data =
            await SeedPropertyAsync(
                includeSecondRoom: false,
                cancellationToken);

        var roomRequest =
            new CreateBookingRequest(
                data.PropertyId,
                data.RoomAId,
                Date(20),
                Date(25),
                GuestCount: 2);

        var entirePropertyRequest =
            new CreateBookingRequest(
                data.PropertyId,
                data.EntirePropertyId,
                Date(20),
                Date(25),
                GuestCount: 2);

        // ACT
        BookingAttempt[] attempts =
            await SendConcurrentlyAsync(
                [
                    roomRequest,
                    entirePropertyRequest
                ],
                cancellationToken);

        // ASSERT - HTTP
        BookingAttempt created =
            Assert.Single(
                attempts,
                attempt =>
                    attempt.StatusCode ==
                    HttpStatusCode.Created);

        BookingAttempt conflict =
            Assert.Single(
                attempts,
                attempt =>
                    attempt.StatusCode ==
                    HttpStatusCode.Conflict);

        Assert.NotNull(created.BookingId);

        Assert.Equal(
            "Booking.NotAvailable",
            conflict.ErrorCode);

        // ASSERT - PostgreSQL
        long persistedBookings =
            await CountBlockingBookingsAsync(
                data.PropertyId,
                Date(20),
                Date(25),
                cancellationToken);

        Assert.Equal(
            1,
            persistedBookings);
    }

    [Fact]
    public async Task CreateBooking_WhenDifferentRoomsCompete_AllowsBothBookings()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestProperty data =
            await SeedPropertyAsync(
                includeSecondRoom: true,
                cancellationToken);

        Assert.True(data.RoomBId.HasValue);

        Guid roomBId = data.RoomBId.Value;

        var roomARequest =
            new CreateBookingRequest(
                data.PropertyId,
                data.RoomAId,
                Date(26),
                Date(30),
                GuestCount: 2);

        var roomBRequest =
            new CreateBookingRequest(
                data.PropertyId,
                roomBId,
                Date(26),
                Date(30),
                GuestCount: 2);

        // ACT
        BookingAttempt[] attempts =
            await SendConcurrentlyAsync(
                [
                    roomARequest,
                    roomBRequest
                ],
                cancellationToken);

        // ASSERT - HTTP
        Assert.All(
            attempts,
            attempt =>
                Assert.Equal(
                    HttpStatusCode.Created,
                    attempt.StatusCode));

        Assert.Equal(
            2,
            attempts
                .Select(
                    attempt =>
                        attempt.BookingId)
                .Distinct()
                .Count());

        // ASSERT - PostgreSQL
        long persistedBookings =
            await CountBlockingBookingsAsync(
                data.PropertyId,
                Date(26),
                Date(30),
                cancellationToken);

        Assert.Equal(
            2,
            persistedBookings);

        long roomABookings =
            await CountBlockingBookingsForUnitAsync(
                data.PropertyId,
                data.RoomAId,
                Date(26),
                Date(30),
                cancellationToken);

        long roomBBookings =
            await CountBlockingBookingsForUnitAsync(
                data.PropertyId,
                roomBId,
                Date(26),
                Date(30),
                cancellationToken);

        Assert.Equal(
            1,
            roomABookings);

        Assert.Equal(
            1,
            roomBBookings);
    }

    [Fact]
    public async Task Post_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestProperty data = await SeedPropertyAsync(
            includeSecondRoom: false,
            cancellationToken);

        var request = new CreateBookingRequest(
            data.PropertyId,
            data.RoomAId,
            Date(10),
            Date(15),
            GuestCount: 2);

        using var message = new HttpRequestMessage(HttpMethod.Post, BookingsEndpoint);
        message.Content = JsonContent.Create(request);

        // ACT
        using HttpResponseMessage response =
            await _client.SendAsync(message, cancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(cancellationToken));

        Assert.Equal("Idempotency.KeyRequired", problem.Code);

        long bookings = await CountBlockingBookingsForUnitAsync(
            data.PropertyId,
            data.RoomAId,
            Date(10),
            Date(15),
            cancellationToken);

        Assert.Equal(0, bookings);
    }

    private async Task<BookingAttempt[]> SendConcurrentlyAsync(
        CreateBookingRequest request,
        int requestCount,
        CancellationToken cancellationToken)
    {
        CreateBookingRequest[] requests =
            Enumerable
                .Repeat(
                    request,
                    requestCount)
                .ToArray();

        return await SendConcurrentlyAsync(
            requests,
            cancellationToken);
    }

    private async Task<BookingAttempt[]> SendConcurrentlyAsync(
        IReadOnlyCollection<
            CreateBookingRequest> requests,
        CancellationToken cancellationToken)
    {
        var startGate = new TaskCompletionSource<
            bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        Task<BookingAttempt>[] tasks =
            requests
                .Select(
                    request =>
                        SendAfterGateAsync(
                            request,
                            Guid.NewGuid().ToString("N"),
                            startGate.Task,
                            cancellationToken))
                .ToArray();

        startGate.SetResult(true);

        return await Task.WhenAll(tasks);
    }

    private async Task<BookingAttempt> SendAfterGateAsync(
        CreateBookingRequest request,
        string idempotencyKey,
        Task startGate,
        CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(cancellationToken);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BookingsEndpoint);

        requestMessage.Headers.TryAddWithoutValidation("Idempotency-key", idempotencyKey);
        requestMessage.Content = JsonContent.Create(request);

        using HttpResponseMessage response =
            await _client.SendAsync(requestMessage, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            CreateBookingResponse body =
                Assert.IsType<
                    CreateBookingResponse>(
                        await response.Content
                            .ReadFromJsonAsync<
                                CreateBookingResponse>(cancellationToken));

            return new BookingAttempt(
                response.StatusCode,
                body.Id,
                ErrorCode: null);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            ProblemDetailsResponse problem =
                Assert.IsType<
                    ProblemDetailsResponse>(
                        await response.Content
                            .ReadFromJsonAsync<
                                ProblemDetailsResponse>(cancellationToken));

            return new BookingAttempt(
                response.StatusCode,
                BookingId: null,
                problem.Code);
        }

        string unexpectedBody = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new InvalidOperationException(
            $"Unexpected HTTP status code " +
            $"{(int)response.StatusCode} " +
            $"({response.StatusCode}). " +
            $"Body: {unexpectedBody}");
    }

    private async Task<TestProperty> SeedPropertyAsync(
        bool includeSecondRoom,
        CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                $"Concurrency Test {Guid.NewGuid():N}",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0)).Value;

        RentableUnit roomA =
            RentableUnit.Create(
                property.Id,
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2).Value;

        RentableUnit entireProperty =
            RentableUnit.Create(
                property.Id,
                "Entire Property",
                RentableUnitType.EntireProperty,
                maximumCapacity: 10,
                maxBaseGuests: 6).Value;

        RentableUnit? roomB =
            includeSecondRoom
                ? RentableUnit.Create(
                    property.Id,
                    "Room B",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2).Value
                : null;

        using IServiceScope scope = _factory.Services.CreateScope();

        IPropertyRepository propertyRepository = scope
            .ServiceProvider
                .GetRequiredService<IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository = scope
            .ServiceProvider
                .GetRequiredService<IRentableUnitRepository>();

        IUnitOfWork unitOfWork = scope
            .ServiceProvider
                .GetRequiredService<IUnitOfWork>();

        propertyRepository.Add(property);
        rentableUnitRepository.Add(roomA);
        rentableUnitRepository.Add(entireProperty);

        if (roomB is not null)
        {
            rentableUnitRepository.Add(roomB);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new TestProperty(
            property.Id,
            roomA.Id,
            roomB?.Id,
            entireProperty.Id);
    }

    private async Task<long> CountBlockingBookingsAsync(
        Guid propertyId,
        DateOnly checkInDate,
        DateOnly checkOutDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteCountAsync(
            """
            SELECT COUNT(*)
            FROM bookings AS b
            WHERE b.property_id = @PropertyId
                AND b.status IN
                (
                    'PendingApproval',
                    'PendingPayment',
                    'Paid',
                    'Completed'
                )
                AND b.check_in_date < @CheckOutDate
                AND b.check_out_date > @CheckInDate;
            """,
            new
            {
                PropertyId = propertyId,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate
            },
            cancellationToken);
    }

    private async Task<long> CountBlockingBookingsForUnitAsync(
        Guid propertyId,
        Guid rentableUnitId,
        DateOnly checkInDate,
        DateOnly checkOutDate,
        CancellationToken cancellationToken)
    {
        return await ExecuteCountAsync(
            """
            SELECT COUNT (*)
            FROM bookings AS b
            WHERE b.property_id = @PropertyId
                AND b.rentable_unit_id = @RentableUnitId
                AND b.status IN
                (
                    'PendingApproval',
                    'PendingPayment',
                    'Paid',
                    'Completed'
                )
                AND b.check_in_date < @CheckOutDate
                AND b.check_out_date > @CheckInDate;
            """,
            new
            {
                PropertyId = propertyId,
                RentableUnitId = rentableUnitId,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate
            },
            cancellationToken);
    }

    private async Task<long> ExecuteCountAsync(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(cancellationToken);

        var command =
            new CommandDefinition(
                sql,
                parameters,
                cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<long>(command);
    }

    private static DateOnly Date(int day)
    {
        return new DateOnly(
            2026,
            8,
            day);
    }

    private sealed record TestProperty(
        Guid PropertyId,
        Guid RoomAId,
        Guid? RoomBId,
        Guid EntirePropertyId);

    private sealed record BookingAttempt(
        HttpStatusCode StatusCode,
        Guid? BookingId,
        string? ErrorCode);
}
