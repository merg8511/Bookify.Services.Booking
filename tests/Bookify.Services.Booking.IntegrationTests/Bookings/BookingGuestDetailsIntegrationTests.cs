using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Bookings;

[Collection(
    BookingApiTestFixture.Name)]
[Trait(
    "Category",
    "Integration")]
public sealed class
    BookingGuestDetailsIntegrationTests
{
    private readonly
        BookingApiFactory _factory;

    public BookingGuestDetailsIntegrationTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task CreateCommand_PersistsNormalizedGuestDetails()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        var command =
            new CreateBookingCommand(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2,
                GuestFullName:
                    "  Leonel   Enrique  Alvarenga  ",
                GuestEmail:
                    "Leonel@EXAMPLE.COM",
                GuestPhone:
                    "+503 (7777) 8888");

        // ACT
        Result<CreateBookingResult> result =
            await ExecuteCreateBookingAsync(
                command,
                cancellationToken);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IBookingRepository repository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        DomainBooking? booking =
            await repository.GetByIdAsync(
                result.Value.Id,
                cancellationToken);

        Assert.NotNull(
            booking);

        Assert.NotNull(
            booking.GuestDetails);

        Assert.Equal(
            "Leonel Enrique Alvarenga",
            booking
                .GuestDetails!
                .FullName);

        Assert.Equal(
            "Leonel@example.com",
            booking
                .GuestDetails!
                .Email);

        Assert.Equal(
            "+50377778888",
            booking
                .GuestDetails!
                .Phone);
    }

    [Fact]
    public async Task BookingReadService_ProjectsGuestDetails()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        var command =
            new CreateBookingCommand(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2,
                GuestFullName:
                    "Leonel Alvarenga",
                GuestEmail:
                    "leonel@example.com",
                GuestPhone:
                    "+50377778888");

        Result<CreateBookingResult>
            creationResult =
                await ExecuteCreateBookingAsync(
                    command,
                    cancellationToken);

        Assert.True(
            creationResult.IsSuccess);

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IBookingReadService
            bookingReadService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IBookingReadService>();

        // ACT
        BookingDetailsReadModel?
            readModel =
                await bookingReadService
                    .GetByIdAsync(
                        creationResult
                            .Value
                            .Id,
                        cancellationToken);

        // ASSERT
        Assert.NotNull(
            readModel);

        Assert.Equal(
            "Leonel Alvarenga",
            readModel.GuestFullName);

        Assert.Equal(
            "leonel@example.com",
            readModel.GuestEmail);

        Assert.Equal(
            "+50377778888",
            readModel.GuestPhone);
    }

    [Fact]
    public async Task LegacyBooking_WithoutGuestDetails_CanBeLoadedByEfAndDapper()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        Guid legacyBookingId =
            Guid.NewGuid();

        await InsertLegacyBookingAsync(
            legacyBookingId,
            data,
            cancellationToken);

        // ACT + ASSERT - EF CORE
        using (
            IServiceScope efScope =
                _factory.Services
                    .CreateScope())
        {
            IBookingRepository repository =
                efScope.ServiceProvider
                    .GetRequiredService<
                        IBookingRepository>();

            DomainBooking? booking =
                await repository.GetByIdAsync(
                    legacyBookingId,
                    cancellationToken);

            Assert.NotNull(
                booking);

            Assert.Null(
                booking.GuestDetails);
        }

        // ACT + ASSERT - DAPPER
        using IServiceScope dapperScope =
            _factory.Services
                .CreateScope();

        IBookingReadService readService =
            dapperScope.ServiceProvider
                .GetRequiredService<
                    IBookingReadService>();

        BookingDetailsReadModel?
            readModel =
                await readService
                    .GetByIdAsync(
                        legacyBookingId,
                        cancellationToken);

        Assert.NotNull(
            readModel);

        Assert.Null(
            readModel.GuestFullName);

        Assert.Null(
            readModel.GuestEmail);

        Assert.Null(
            readModel.GuestPhone);
    }

    [Fact]
    public async Task Post_WithValidGuest_PersistsNormalizedGuestDetails()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2,
                Guest:
                    new CreateBookingGuestRequest(
                        "  Leonel   Enrique  Alvarenga  ",
                        "Leonel@EXAMPLE.COM",
                        "+503 (7777) 8888"));

        // ACT
        HttpResponseMessage response =
            await PostBookingAsync(
                request,
                cancellationToken);

        // ASSERT - HTTP
        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        CreateBookingResponse? body =
            await response.Content
                .ReadFromJsonAsync<
                    CreateBookingResponse>(
                        cancellationToken);

        Assert.NotNull(
            body);

        Assert.NotEqual(
            Guid.Empty,
            body.Id);

        // ASSERT - POSTGRESQL / EF
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IBookingRepository repository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        DomainBooking? booking =
            await repository.GetByIdAsync(
                body.Id,
                cancellationToken);

        Assert.NotNull(
            booking);

        Assert.NotNull(
            booking.GuestDetails);

        Assert.Equal(
            "Leonel Enrique Alvarenga",
            booking
                .GuestDetails!
                .FullName);

        Assert.Equal(
            "Leonel@example.com",
            booking
                .GuestDetails!
                .Email);

        Assert.Equal(
            "+50377778888",
            booking
                .GuestDetails!
                .Phone);
    }

    [Fact]
    public async Task Post_WithoutGuest_ReturnsBadRequest()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2,
                Guest: null);

        // ACT
        HttpResponseMessage response =
            await PostBookingAsync(
                request,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content
                .Headers
                .ContentType?
                .MediaType);
    }

    [Fact]
    public async Task Post_WithInvalidGuestEmail_ReturnsBadRequest()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2,
                Guest:
                    new CreateBookingGuestRequest(
                        "Leonel Alvarenga",
                        "not-an-email",
                        "+50377778888"));

        // ACT
        HttpResponseMessage response =
            await PostBookingAsync(
                request,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content
                .Headers
                .ContentType?
                .MediaType);
    }

    [Fact]
    public async Task Post_WithInvalidGuestPhone_ReturnsBadRequest()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2,
                Guest:
                    new CreateBookingGuestRequest(
                        "Leonel Alvarenga",
                        "leonel@example.com",
                        "+503/7777/8888"));

        // ACT
        HttpResponseMessage response =
            await PostBookingAsync(
                request,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content
                .Headers
                .ContentType?
                .MediaType);
    }

    private async Task<
        Result<CreateBookingResult>>
        ExecuteCreateBookingAsync(
            CreateBookingCommand command,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        ICommandExecutor<
            CreateBookingCommand,
            CreateBookingResult> executor =
                scope.ServiceProvider
                    .GetRequiredService<
                        ICommandExecutor<
                            CreateBookingCommand,
                            CreateBookingResult>>();

        return await executor.ExecuteAsync(
            command,
            cancellationToken);
    }

    private async Task<
        HttpResponseMessage>
        PostBookingAsync(
            CreateBookingRequest request,
            CancellationToken cancellationToken)
    {
        HttpClient client =
            _factory.CreateClient();

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/bookings");

        message.Headers.Add(
            "Idempotency-Key",
            Guid.NewGuid()
                .ToString("N"));

        message.Content =
            JsonContent.Create(
                request);

        return await client.SendAsync(
            message,
            cancellationToken);
    }

    private async Task<SeedData>
        SeedPropertyWithRoomAsync(
            CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Guest Details Test " +
                    $"{Guid.NewGuid():N}",
                    "America/El_Salvador",
                    new TimeOnly(
                        15,
                        0),
                    new TimeOnly(
                        11,
                        0))
                .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                    property.Id,
                    $"Room " +
                    $"{Guid.NewGuid():N}",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

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

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository
            propertyRepository =
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

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        await unitOfWork
            .SaveChangesAsync(
                cancellationToken);

        return new SeedData(
            property.Id,
            rentableUnit.Id);
    }

    private async Task
        InsertLegacyBookingAsync(
            Guid bookingId,
            SeedData data,
            CancellationToken cancellationToken)
    {
        IDbConnectionFactory
            connectionFactory =
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
                INSERT INTO bookings
                (
                    id,
                    property_id,
                    rentable_unit_id,
                    check_in_date,
                    check_out_date,
                    guest_count,
                    status,
                    cancellation_reason
                )
                VALUES
                (
                    @BookingId,
                    @PropertyId,
                    @RentableUnitId,
                    @CheckInDate,
                    @CheckOutDate,
                    @GuestCount,
                    'PendingApproval',
                    NULL
                );
                """,
                new
                {
                    BookingId =
                        bookingId,

                    data.PropertyId,

                    data.RentableUnitId,

                    CheckInDate =
                        Date(20),

                    CheckOutDate =
                        Date(22),

                    GuestCount =
                        2
                },
                cancellationToken:
                    cancellationToken);

        await connection.ExecuteAsync(
            command);
    }

    private static DateOnly
        Date(
            int day)
    {
        return new DateOnly(
            2026,
            10,
            day);
    }

    private sealed record
        SeedData(
            Guid PropertyId,
            Guid RentableUnitId);
}
