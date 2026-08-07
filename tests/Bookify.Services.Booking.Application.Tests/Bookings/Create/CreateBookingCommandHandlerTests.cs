using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Create;

public sealed class CreateBookingCommandHandlerTests
{
    [Fact]
    public async Task
        HandleAsync_WithValidCommand_PersistsBooking()
    {
        // ARRANGE
        Property property = CreateProperty();

        RentableUnit rentableUnit = CreateRentableUnit(
                property.Id,
                maximumCapacity: 4);

        var propertyRepository = new SpyPropertyRepository(property);
        var rentableUnitRepository = new SpyRentableUnitRepository(rentableUnit);
        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: false);
        var unitOfWork = new SpyUnitOfWork();

        var handler = new CreateBookingCommandHandler(
                propertyRepository,
                rentableUnitRepository,
                bookingRepository,
                availabilityReader,
                unitOfWork);

        CreateBookingCommand command =
            CreateValidCommand(
                property.Id,
                rentableUnit.Id,
                guestCount: 2);

        // ACT
        Result<Guid> result = await handler.HandleAsync(command);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.NotEqual(
            Guid.Empty,
            result.Value);

        Assert.NotNull(bookingRepository.AddedBooking);

        DomainBooking booking = bookingRepository.AddedBooking;

        Assert.Equal(
            result.Value,
            booking.Id);

        Assert.Equal(
            property.Id,
            booking.PropertyId);

        Assert.Equal(
            rentableUnit.Id,
            booking.RentableUnitId);

        Assert.Equal(
            Date(10),
            booking.StayPeriod.CheckInDate);

        Assert.Equal(
            Date(15),
            booking.StayPeriod.CheckOutDate);

        Assert.Equal(
            2,
            booking.GuestCount.Value);

        Assert.Equal(
            BookingStatus.PendingApproval,
            booking.Status);

        Assert.Equal(
            1,
            availabilityReader.CallCount);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPropertyDoesNotExist_ReturnsFailure()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();

        RentableUnit rentableUnit = CreateRentableUnit(
                propertyId,
                maximumCapacity: 4);

        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: false);
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property: null),
                new SpyRentableUnitRepository(
                    rentableUnit),
                bookingRepository,
                availabilityReader,
                unitOfWork);

        CreateBookingCommand command =
            CreateValidCommand(
                propertyId,
                rentableUnit.Id,
                guestCount: 2);

        // ACT
        Result<Guid> result = await handler.HandleAsync(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            CreateBookingErrors
                .PropertyNotFound(
                    propertyId),
            result.Error);

        Assert.Null(bookingRepository.AddedBooking);

        Assert.Equal(
            0,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPropertyIsInactive_ReturnsFailure()
    {
        // ARRANGE
        Property property = CreateProperty();
        property.Deactivate();

        RentableUnit rentableUnit =
            CreateRentableUnit(
                property.Id,
                maximumCapacity: 4);

        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: false);
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property),
                new SpyRentableUnitRepository(
                    rentableUnit),
                bookingRepository,
                availabilityReader,
                unitOfWork);

        CreateBookingCommand command =
            CreateValidCommand(
                property.Id,
                rentableUnit.Id,
                guestCount: 2);

        // ACT
        Result<Guid> result = await handler.HandleAsync(command);

        // ASSERT
        Assert.Equal(
            CreateBookingErrors
                .PropertyInactive(
                    property.Id),
            result.Error);

        Assert.Null(bookingRepository.AddedBooking);

        Assert.Equal(
            0,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRentableUnitDoesNotExist_ReturnsFailure()
    {
        // ARRANGE
        Property property = CreateProperty();
        Guid rentableUnitId = Guid.NewGuid();
        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: false);
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property),
                new SpyRentableUnitRepository(
                    rentableUnit: null),
                bookingRepository,
                availabilityReader,
                unitOfWork);

        CreateBookingCommand command =
            CreateValidCommand(
                property.Id,
                rentableUnitId,
                guestCount: 2);

        // ACT
        Result<Guid> result = await handler.HandleAsync(command);

        // ASSERT
        Assert.Equal(
            CreateBookingErrors
                .RentableUnitNotFound(
                    rentableUnitId),
            result.Error);

        Assert.Null(bookingRepository.AddedBooking);

        Assert.Equal(
            0,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRentableUnitBelongsToAnotherProperty_ReturnsFailure()
    {
        // ARRANGE
        Property requestedProperty = CreateProperty();
        Property ownerProperty = CreateProperty();

        RentableUnit rentableUnit =
            CreateRentableUnit(
                ownerProperty.Id,
                maximumCapacity: 4);

        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: false);
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    requestedProperty),
                new SpyRentableUnitRepository(
                    rentableUnit),
                bookingRepository,
                availabilityReader,
                unitOfWork);

        CreateBookingCommand command =
            CreateValidCommand(
                requestedProperty.Id,
                rentableUnit.Id,
                guestCount: 2);

        // ACT
        Result<Guid> result = await handler.HandleAsync(command);

        // ASSERT
        Assert.Equal(
            CreateBookingErrors
                .RentableUnitPropertyMismatch(
                    rentableUnit.Id,
                    requestedProperty.Id),
            result.Error);

        Assert.Null(bookingRepository.AddedBooking);

        Assert.Equal(
            0,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRentableUnitIsInactive_ReturnsDomainFailure()
    {
        // ARRANGE
        Property property = CreateProperty();

        RentableUnit rentableUnit = CreateRentableUnit(
                property.Id,
                maximumCapacity: 4);

        rentableUnit.Deactivate();
        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: false);
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property),
                new SpyRentableUnitRepository(
                    rentableUnit),
                bookingRepository,
                availabilityReader,
                unitOfWork);

        // ACT
        Result<Guid> result =
            await handler.HandleAsync(
                CreateValidCommand(
                    property.Id,
                    rentableUnit.Id,
                    guestCount: 2));

        // ASSERT
        Assert.Equal(
            BookingErrors.RentableUnitInactive,
            result.Error);

        Assert.Null(bookingRepository.AddedBooking);

        Assert.Equal(
            0,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenGuestCapacityIsExceeded_ReturnsDomainFailure()
    {
        // ARRANGE
        Property property = CreateProperty();

        RentableUnit rentableUnit =
            CreateRentableUnit(
                property.Id,
                maximumCapacity: 2);

        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: false);
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property),
                new SpyRentableUnitRepository(
                    rentableUnit),
                bookingRepository,
                availabilityReader,
                unitOfWork);

        // ACT
        Result<Guid> result =
            await handler.HandleAsync(
                CreateValidCommand(
                    property.Id,
                    rentableUnit.Id,
                    guestCount: 3));

        // ASSERT
        Assert.Equal(
            BookingErrors.GuestCapacityExceeded,
            result.Error);

        Assert.Null(bookingRepository.AddedBooking);

        Assert.Equal(
            0,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenInventoryHasConflict_ReturnsFailureWithoutSaving()
    {
        // ARRANGE
        Property property = CreateProperty();

        RentableUnit rentableUnit =
            CreateRentableUnit(
                property.Id,
                maximumCapacity: 4);

        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: true);
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property),
                new SpyRentableUnitRepository(
                    rentableUnit),
                bookingRepository,
                availabilityReader,
                unitOfWork);

        // ACT
        Result<Guid> result =
            await handler.HandleAsync(
                CreateValidCommand(
                    property.Id,
                    rentableUnit.Id,
                    guestCount: 2));

        // ASSERT
        Assert.Equal(
            CreateBookingErrors.NotAvailable,
            result.Error);

        Assert.Null(bookingRepository.AddedBooking);

        Assert.Equal(
            1,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        // ARRANGE
        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property: null),
                new SpyRentableUnitRepository(
                    rentableUnit: null),
                new SpyBookingRepository(),
                new StubBookingAvailabilityReader(
                    hasConflict: false),
                new SpyUnitOfWork());

        // ACT
        Task Action()
        {
            return handler.HandleAsync(
                null!);
        }

        // ASSERT
        await Assert.ThrowsAsync<
            ArgumentNullException>(
                Action);
    }

    private static CreateBookingCommand
        CreateValidCommand(
            Guid propertyId,
            Guid rentableUnitId,
            int guestCount)
    {
        return new CreateBookingCommand(
            propertyId,
            rentableUnitId,
            Date(10),
            Date(15),
            guestCount);
    }

    private static Property CreateProperty()
    {
        return Property.Create(
            "Rancho Costa Azul",
            "America/El_Salvador",
            new TimeOnly(
                15,
                0),
            new TimeOnly(
                11,
                0)).Value;
    }

    private static RentableUnit
        CreateRentableUnit(
            Guid propertyId,
            int maximumCapacity)
    {
        return RentableUnit.Create(
            propertyId,
            "Room A",
            RentableUnitType.Room,
            maximumCapacity,
            maxBaseGuests: 1).Value;
    }

    private static DateOnly Date(int day)
    {
        return new DateOnly(
            2026,
            8,
            day);
    }

    private sealed class SpyPropertyRepository : IPropertyRepository
    {
        private readonly Property? _property;

        public SpyPropertyRepository(Property? property)
        {
            _property = property;
        }

        public Task<Property?> GetByIdAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_property);
        }

        public void Add(Property property)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class
        SpyRentableUnitRepository :
        IRentableUnitRepository
    {
        private readonly RentableUnit? _rentableUnit;

        public SpyRentableUnitRepository(RentableUnit? rentableUnit)
        {
            _rentableUnit = rentableUnit;
        }

        public Task<RentableUnit?>
            GetByIdAsync(
                Guid rentableUnitId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_rentableUnit);
        }

        public void Add(
            RentableUnit rentableUnit)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class
        SpyBookingRepository :
        IBookingRepository
    {
        public DomainBooking? AddedBooking
        {
            get;
            private set;
        }

        public Task<DomainBooking?> GetByIdAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(AddedBooking);
        }

        public void Add(DomainBooking booking)
        {
            AddedBooking = booking;
        }
    }

    private sealed class
        StubBookingAvailabilityReader :
        IBookingAvailabilityReader
    {
        private readonly bool _hasConflict;

        public StubBookingAvailabilityReader(bool hasConflict)
        {
            _hasConflict = hasConflict;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public Task<bool> HasConflictAsync(
            Guid propertyId,
            Guid requestedRentableUnitId,
            DateOnly requestedCheckInDate,
            DateOnly requestedCheckOutDate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            return Task.FromResult(_hasConflict);
        }
    }

    private sealed class SpyUnitOfWork :
        IUnitOfWork
    {
        public int SaveChangesCallCount
        {
            get;
            private set;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveChangesCallCount++;

            return Task.CompletedTask;
        }
    }
}
