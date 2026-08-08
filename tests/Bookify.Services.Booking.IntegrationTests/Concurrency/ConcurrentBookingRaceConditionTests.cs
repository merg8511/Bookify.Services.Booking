using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Concurrency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "ConcurrencyDiagnostic")]
public sealed class
    ConcurrentBookingRaceConditionTests
{
    private readonly BookingApiFactory _factory;

    public ConcurrentBookingRaceConditionTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateBooking_WhenTwoRequestsCheckBeforeEitherWrites_AllowsDoubleBooking()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        SeedData data = await SeedAsync(cancellationToken);

        using IServiceScope readerScope = _factory.Services.CreateScope();

        IBookingAvailabilityReader
            realAvailabilityReader =
                readerScope.ServiceProvider
                    .GetRequiredService<
                        IBookingAvailabilityReader>();

        var coordinatedReader =
            new CoordinatedBookingAvailabilityReader(
                realAvailabilityReader,
                participantCount: 2);

        using IServiceScope firstScope = _factory.Services.CreateScope();

        using IServiceScope secondScope = _factory.Services.CreateScope();

        CreateBookingCommandHandler firstHandler =
            CreateHandler(
                firstScope,
                coordinatedReader);

        CreateBookingCommandHandler secondHandler =
            CreateHandler(
                secondScope,
                coordinatedReader);

        var firstCommand =
            new CreateBookingCommand(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2);

        var secondCommand =
            new CreateBookingCommand(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2);

        // ACT
        Task<Result<Guid>> firstTask =
            firstHandler.HandleAsync(
                firstCommand,
                cancellationToken);

        Task<Result<Guid>> secondTask =
            secondHandler.HandleAsync(
                secondCommand,
                cancellationToken);

        Result<Guid>[] results =
            await Task.WhenAll(
                firstTask,
                secondTask);

        // ASSERT
        Assert.Equal(
            2,
            coordinatedReader.CompletedChecks);

        Assert.All(
            results,
            result =>
                Assert.True(
                    result.IsSuccess));

        Assert.Equal(
            2,
            results
                .Select(
                    result =>
                        result.Value)
                .Distinct()
                .Count());

        long blockingBookings =
            await CountBlockingBookingsAsync(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                cancellationToken);

        // IMPORTANT:
        // This assertion intentionally documents
        // the current race condition.
        //
        // The desired production invariant is 1.
        Assert.Equal(
            2,
            blockingBookings);
    }

    private static CreateBookingCommandHandler
        CreateHandler(
            IServiceScope scope,
            IBookingAvailabilityReader
                availabilityReader)
    {
        IServiceProvider services = scope.ServiceProvider;

        return new CreateBookingCommandHandler(
            services.GetRequiredService<
                IPropertyRepository>(),
            services.GetRequiredService<
                IRentableUnitRepository>(),
            services.GetRequiredService<
                IBookingRepository>(),
            availabilityReader,
            services.GetRequiredService<
                IUnitOfWork>());
    }

    private async Task<SeedData> SeedAsync(CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Concurrency Property " +
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
                    "Room A",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

        using IServiceScope scope = _factory.Services.CreateScope();

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

        rentableUnitRepository.Add(rentableUnit);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedData(property.Id, rentableUnit.Id);
    }

    private async Task<long>
        CountBlockingBookingsAsync(
            Guid propertyId,
            Guid rentableUnitId,
            DateOnly checkInDate,
            DateOnly checkOutDate,
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
                FROM bookings AS b
                WHERE b.property_id =
                      @PropertyId
                  AND b.rentable_unit_id =
                      @RentableUnitId
                  AND b.status IN
                  (
                      'PendingApproval',
                      'PendingPayment',
                      'Paid',
                      'Completed'
                  )
                  AND b.check_in_date <
                      @CheckOutDate
                  AND b.check_out_date >
                      @CheckInDate;
                """,
                new
                {
                    PropertyId = propertyId,

                    RentableUnitId = rentableUnitId,

                    CheckInDate = checkInDate,

                    CheckOutDate = checkOutDate
                },
                cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<long>(command);
    }

    private static DateOnly Date(
        int day)
    {
        return new DateOnly(
            2026,
            8,
            day);
    }

    private sealed record SeedData(
        Guid PropertyId,
        Guid RentableUnitId);

    private sealed class
        CoordinatedBookingAvailabilityReader :
        IBookingAvailabilityReader
    {
        private readonly IBookingAvailabilityReader _inner;

        private readonly int _participantCount;

        private readonly
            TaskCompletionSource<bool>
                _allChecksCompleted =
                    new(
                        TaskCreationOptions
                            .RunContinuationsAsynchronously);

        private int _completedChecks;

        public CoordinatedBookingAvailabilityReader(
            IBookingAvailabilityReader inner,
            int participantCount)
        {
            ArgumentNullException.ThrowIfNull(inner);

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantCount);

            _inner = inner;
            _participantCount = participantCount;
        }

        public int CompletedChecks =>
            Volatile.Read(ref _completedChecks);

        public async Task<bool> HasConflictAsync(
            Guid propertyId,
            Guid requestedRentableUnitId,
            DateOnly requestedCheckInDate,
            DateOnly requestedCheckOutDate,
            CancellationToken cancellationToken = default)
        {
            bool hasConflict =
                await _inner.HasConflictAsync(
                    propertyId,
                    requestedRentableUnitId,
                    requestedCheckInDate,
                    requestedCheckOutDate,
                    cancellationToken);

            int completedChecks = Interlocked.Increment(ref _completedChecks);

            if (completedChecks == _participantCount)
            {
                _allChecksCompleted.TrySetResult(true);
            }

            await _allChecksCompleted
                .Task
                .WaitAsync(
                    cancellationToken);

            return hasConflict;
        }
    }
}
