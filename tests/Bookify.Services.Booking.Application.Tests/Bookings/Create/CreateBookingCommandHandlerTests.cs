using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Create;

public sealed class CreateBookingCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidCommand_PersistsBooking()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Property property = CreateProperty();

        RentableUnit rentableUnit = CreateRentableUnit(
                property.Id,
                maximumCapacity: 4);

        var propertyRepository = new SpyPropertyRepository(property);
        var rentableUnitRepository = new SpyRentableUnitRepository(rentableUnit);
        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: false);
        var inventoryLock = new StubBookingInventoryLock(acquired: true);
        var transactionManager = new StubTransactionManager();
        var unitOfWork = new SpyUnitOfWork();

        var handler = new CreateBookingCommandHandler(
                propertyRepository,
                rentableUnitRepository,
                bookingRepository,
                availabilityReader,
                inventoryLock,
                unitOfWork,
                transactionManager);

        CreateBookingCommand command =
            CreateValidCommand(
                property.Id,
                rentableUnit.Id,
                guestCount: 2);

        // ACT
        Result<CreateBookingResult> result = await handler.HandleAsync(command, cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.NotEqual(
            Guid.Empty,
            result.Value.Id);

        Assert.NotNull(bookingRepository.AddedBooking);

        DomainBooking booking = bookingRepository.AddedBooking;

        Assert.Equal(
            result.Value.Id,
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

        Assert.NotNull(
            booking.PriceSnapshot);

        Assert.Equal(
    booking.Id,
    result.Value.Id);

        Assert.Equal(
            BookingStatus.PendingApproval,
            result.Value.Status);

        Assert.Equal(
            540m,
            result.Value.AccommodationPrice);

        Assert.Equal(
            125m,
            result.Value.ExtraGuestPrice);

        Assert.Equal(
            665m,
            result.Value.TotalPrice);

        Assert.Equal(
            "USD",
            result.Value.Currency);

        Assert.Equal(
            540m,
            booking
                .PriceSnapshot
                .AccommodationPrice
                .Amount);

        Assert.Equal(
            125m,
            booking
                .PriceSnapshot
                .ExtraGuestPrice
                .Amount);

        Assert.Equal(
            665m,
            booking
                .PriceSnapshot
                .TotalPrice
                .Amount);

        Assert.Equal(
            "USD",
            booking
                .PriceSnapshot
                .TotalPrice
                .Currency);

        Assert.Equal(
            1,
            availabilityReader.CallCount);

        Assert.Equal(
            1,
            transactionManager.BeginCallCount);

        Assert.Equal(
            1,
            inventoryLock.CallCount);

        Assert.Equal(
            1,
            transactionManager.Transaction.CommitCallCount);

        Assert.Equal(
            0,
            transactionManager.Transaction.RollbackCallCount);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPropertyDoesNotExist_ReturnsFailure()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

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
                new StubBookingInventoryLock(),
                unitOfWork,
                new StubTransactionManager());

        CreateBookingCommand command =
            CreateValidCommand(
                propertyId,
                rentableUnit.Id,
                guestCount: 2);

        // ACT
        Result<CreateBookingResult> result = await handler.HandleAsync(command, cancellationToken);

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

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
                new StubBookingInventoryLock(),
                unitOfWork,
                new StubTransactionManager());

        CreateBookingCommand command =
            CreateValidCommand(
                property.Id,
                rentableUnit.Id,
                guestCount: 2);

        // ACT
        Result<CreateBookingResult> result = await handler.HandleAsync(command, cancellationToken);

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

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
                new StubBookingInventoryLock(),
                unitOfWork,
                new StubTransactionManager());

        CreateBookingCommand command =
            CreateValidCommand(
                property.Id,
                rentableUnitId,
                guestCount: 2);

        // ACT
        Result<CreateBookingResult> result = await handler.HandleAsync(command, cancellationToken);

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

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
                new StubBookingInventoryLock(),
                unitOfWork,
                new StubTransactionManager());

        CreateBookingCommand command =
            CreateValidCommand(
                requestedProperty.Id,
                rentableUnit.Id,
                guestCount: 2);

        // ACT
        Result<CreateBookingResult> result = await handler.HandleAsync(command, cancellationToken);

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

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
                new StubBookingInventoryLock(),
                unitOfWork,
                new StubTransactionManager());

        // ACT
        Result<CreateBookingResult> result =
            await handler.HandleAsync(
                CreateValidCommand(
                    property.Id,
                    rentableUnit.Id,
                    guestCount: 2),
                cancellationToken);

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

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
                new StubBookingInventoryLock(),
                unitOfWork,
                new StubTransactionManager());

        // ACT
        Result<CreateBookingResult> result =
            await handler.HandleAsync(
                CreateValidCommand(
                    property.Id,
                    rentableUnit.Id,
                    guestCount: 3),
                cancellationToken);

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Property property = CreateProperty();

        RentableUnit rentableUnit =
            CreateRentableUnit(
                property.Id,
                maximumCapacity: 4);

        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader(hasConflict: true);
        var inventoryLock = new StubBookingInventoryLock(acquired: true);
        var transactionManager = new StubTransactionManager();
        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(property),
                new SpyRentableUnitRepository(rentableUnit),
                bookingRepository,
                availabilityReader,
                inventoryLock,
                unitOfWork,
                transactionManager);

        // ACT
        Result<CreateBookingResult> result =
            await handler.HandleAsync(
                CreateValidCommand(
                    property.Id,
                    rentableUnit.Id,
                    guestCount: 2),
                cancellationToken);

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

        Assert.Equal(
            0,
            transactionManager.Transaction.CommitCallCount);
        Assert.Equal(
            1,
            transactionManager.Transaction.RollbackCallCount);
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
                new StubBookingInventoryLock(),
                new SpyUnitOfWork(),
                new StubTransactionManager());

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

    [Fact]
    public async Task HandleAsync_WhenPricingIsNotConfigured_ReturnsFailureWithoutSaving()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Property property =
            CreateProperty();

        RentableUnit rentableUnit =
            CreateRentableUnitWithoutPricing(
                property.Id,
                maximumCapacity: 4);

        var bookingRepository =
            new SpyBookingRepository();

        var availabilityReader =
            new StubBookingAvailabilityReader(
                hasConflict: false);

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new StubTransactionManager();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property),
                new SpyRentableUnitRepository(
                    rentableUnit),
                bookingRepository,
                availabilityReader,
                new StubBookingInventoryLock(
                    acquired: true),
                unitOfWork,
                transactionManager);

        // ACT
        Result<CreateBookingResult> result =
            await handler.HandleAsync(
                CreateValidCommand(
                    property.Id,
                    rentableUnit.Id,
                    guestCount: 2),
                cancellationToken);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            CreateBookingErrors
                .PricingNotConfigured(
                    rentableUnit.Id),
            result.Error);

        Assert.Null(
            bookingRepository.AddedBooking);

        Assert.Equal(
            0,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            0,
            transactionManager
                .Transaction
                .CommitCallCount);

        Assert.Equal(
            1,
            transactionManager
                .Transaction
                .RollbackCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPricingCalculationFails_ReturnsDomainFailureWithoutSaving()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Property property =
            CreateProperty();

        RentableUnit rentableUnit =
            CreateRentableUnit(
                property.Id,
                maximumCapacity: 4);

        PricingSeason firstSeason =
            PricingSeason.Create(
                Date(10),
                Date(12),
                Money.Create(
                    200m,
                    "USD")
                .Value,
                priority: 20)
            .Value;

        PricingSeason secondSeason =
            PricingSeason.Create(
                Date(10),
                Date(13),
                Money.Create(
                    250m,
                    "USD")
                .Value,
                priority: 20)
            .Value;

        rentableUnit.AddPricingSeason(
            firstSeason);

        rentableUnit.AddPricingSeason(
            secondSeason);

        var bookingRepository =
            new SpyBookingRepository();

        var availabilityReader =
            new StubBookingAvailabilityReader(
                hasConflict: false);

        var unitOfWork =
            new SpyUnitOfWork();

        var transactionManager =
            new StubTransactionManager();

        var handler =
            new CreateBookingCommandHandler(
                new SpyPropertyRepository(
                    property),
                new SpyRentableUnitRepository(
                    rentableUnit),
                bookingRepository,
                availabilityReader,
                new StubBookingInventoryLock(
                    acquired: true),
                unitOfWork,
                transactionManager);

        var command =
            new CreateBookingCommand(
                property.Id,
                rentableUnit.Id,
                Date(10),
                Date(11),
                GuestCount: 1);

        // ACT
        Result<CreateBookingResult> result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PricingSeasonErrors
                .AmbiguousPriority(
                    Date(10),
                    20),
            result.Error);

        Assert.Null(
            bookingRepository.AddedBooking);

        Assert.Equal(
            0,
            availabilityReader.CallCount);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);

        Assert.Equal(
            1,
            transactionManager
                .Transaction
                .RollbackCallCount);
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
        RentableUnit rentableUnit =
            CreateRentableUnitWithoutPricing(
                propertyId,
                maximumCapacity);

        rentableUnit.ConfigurePricing(CreatePricing());

        return rentableUnit;
    }

    private static RentableUnit CreateRentableUnitWithoutPricing(
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

    private static RentableUnitPricing CreatePricing()
    {
        return RentableUnitPricing.Create(
        Money.Create(
            100m,
            "USD").Value,
        Money.Create(
            140m,
            "USD").Value,
        Money.Create(
            25m,
            "USD").Value)
    .Value;
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

    private sealed class StubBookingInventoryLock :
        IBookingInventoryLock
    {
        private readonly bool _acquired;

        public StubBookingInventoryLock(bool acquired = true)
        {
            _acquired = acquired;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public Task<bool> TryAcquireAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_acquired);
        }
    }

    private sealed class SpyTransaction : ITransaction
    {
        public int CommitCallCount
        {
            get;
            private set;
        }

        public int RollbackCallCount
        {
            get;
            private set;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommitCallCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RollbackCallCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubTransactionManager : ITransactionManager
    {
        public SpyTransaction Transaction { get; } = new();
        public int BeginCallCount
        {
            get;
            private set;
        }

        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeginCallCount++;
            return Task.FromResult<ITransaction>(Transaction);
        }
    }
}
