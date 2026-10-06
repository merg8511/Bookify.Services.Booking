using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Create;

public sealed class CreateBookingGuestDetailsCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidGuestDetails_PersistsNormalizedGuestSnapshot()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Property property = PropertyTestFactory.CreateValidProperty();
        RentableUnit rentableUnit = RentableUnitTestFactory.CreateRentableUnitWithPricing(propertyId: property.Id);

        var propertyRepository = new StubPropertyRepository(property);
        var rentableUnitRepository = new StubRentableUnitRepository(rentableUnit);
        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader();
        var inventoryLock = new StubBookingInventoryLock();
        var unitOfWork = new SpyUnitOfWork();
        var transactionManager = new SpyTransactionManager();

        var handler = new CreateBookingCommandHandler(
            propertyRepository,
            rentableUnitRepository,
            bookingRepository,
            availabilityReader,
            inventoryLock,
            unitOfWork,
            transactionManager,
            new FixedClock(BookingTestTime.CreatedAtUtc),
            BookingTestDeadlinePolicy.Create());

        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(
            propertyId: property.Id,
            rentableUnitId: rentableUnit.Id,
            checkInDate: BookingTestData.CreateDate(10),
            checkOutDate: BookingTestData.CreateDate(15),
            guestCount: 2,
            guestFullName: "  Leonel   Enrique  Alvarenga  ",
            guestEmail: "Leonel@EXAMPLE.COM",
            guestPhone: "+503 (7777) 8888");

        // ACT
        Result<CreateBookingResult> result = await handler.HandleAsync(command, cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(bookingRepository.AddedBooking);

        DomainBooking booking = bookingRepository.AddedBooking;
        Assert.NotNull(booking.GuestDetails);
        Assert.Equal("Leonel Enrique Alvarenga", booking.GuestDetails!.FullName);
        Assert.Equal("Leonel@example.com", booking.GuestDetails!.Email);
        Assert.Equal("+50377778888", booking.GuestDetails!.Phone);
        Assert.Equal(1, transactionManager.BeginCallCount);
        Assert.Equal(1, transactionManager.Transaction.CommitCallCount);
        Assert.Equal(0, transactionManager.Transaction.RollbackCallCount);
        Assert.Equal(1, bookingRepository.AddCallCount);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidGuestDetails_FailsBeforeStartingTransaction()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Property property = PropertyTestFactory.CreateValidProperty();
        RentableUnit rentableUnit = RentableUnitTestFactory.CreateRentableUnitWithPricing(propertyId: property.Id);

        var bookingRepository = new SpyBookingRepository();
        var availabilityReader = new StubBookingAvailabilityReader();
        var inventoryLock = new StubBookingInventoryLock();
        var unitOfWork = new SpyUnitOfWork();
        var transactionManager = new SpyTransactionManager();

        var handler = new CreateBookingCommandHandler(
            new StubPropertyRepository(property),
            new StubRentableUnitRepository(rentableUnit),
            bookingRepository,
            availabilityReader,
            inventoryLock,
            unitOfWork,
            transactionManager,
            new FixedClock(BookingTestTime.CreatedAtUtc),
            BookingTestDeadlinePolicy.Create());

        CreateBookingCommand command = BookingCommandTestFactory.CreateBookingCommand(
            propertyId: property.Id,
            rentableUnitId: rentableUnit.Id,
            checkInDate: BookingTestData.CreateDate(10),
            checkOutDate: BookingTestData.CreateDate(15),
            guestCount: 2,
            guestFullName: "Leonel Alvarenga",
            guestEmail: "not-an-email",
            guestPhone: "+50377778888");

        // ACT
        Result<CreateBookingResult> result = await handler.HandleAsync(command, cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(GuestDetailsErrors.EmailInvalid, result.Error);
        Assert.Null(bookingRepository.AddedBooking);
        Assert.Equal(0, bookingRepository.AddCallCount);
        Assert.Equal(0, transactionManager.BeginCallCount);
        Assert.Equal(0, availabilityReader.CallCount);
        Assert.Equal(0, inventoryLock.CallCount);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    private sealed class StubPropertyRepository : IPropertyRepository
    {
        private readonly Property? _property;

        public StubPropertyRepository(Property? property)
        {
            _property = property;
        }

        public Task<Property?> GetByIdAsync(Guid propertyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_property);
        }

        public void Add(Property property)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubRentableUnitRepository : IRentableUnitRepository
    {
        private readonly RentableUnit? _rentableUnit;

        public StubRentableUnitRepository(RentableUnit? rentableUnit)
        {
            _rentableUnit = rentableUnit;
        }

        public Task<RentableUnit?> GetByIdAsync(Guid rentableUnitId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_rentableUnit);
        }

        public void Add(RentableUnit rentableUnit)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SpyBookingRepository : IBookingRepository
    {
        public DomainBooking? AddedBooking { get; private set; }
        public int AddCallCount { get; private set; }

        public Task<DomainBooking?> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(AddedBooking);
        }

        public void Add(DomainBooking booking)
        {
            AddCallCount++;
            AddedBooking = booking;
        }
    }

    private sealed class StubBookingAvailabilityReader : IBookingAvailabilityReader
    {
        public int CallCount { get; private set; }

        public Task<bool> HasConflictAsync(
            Guid propertyId,
            Guid requestedRentableUnitId,
            DateOnly requestedCheckInDate,
            DateOnly requestedCheckOutDate,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(false);
        }
    }

    private sealed class StubBookingInventoryLock : IBookingInventoryLock
    {
        public int CallCount { get; private set; }

        public Task<bool> TryAcquireAsync(Guid propertyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class SpyTransaction : ITransaction
    {
        public int CommitCallCount { get; private set; }
        public int RollbackCallCount { get; private set; }

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

    private sealed class SpyTransactionManager : ITransactionManager
    {
        public SpyTransaction Transaction { get; } = new();
        public int BeginCallCount { get; private set; }

        public Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeginCallCount++;
            return Task.FromResult<ITransaction>(Transaction);
        }
    }
}
