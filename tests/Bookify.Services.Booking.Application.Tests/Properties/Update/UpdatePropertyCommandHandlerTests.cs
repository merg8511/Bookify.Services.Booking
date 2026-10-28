
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Properties.Management;
using Bookify.Services.Booking.Application.Properties.Update;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Properties.Update;

public sealed class UpdatePropertyCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidDetails_ShouldSaveAndCommit()
    {
        // ARRANGE
        Property property = PropertyTestFactory.CreateValidProperty();

        var repository = new StubPropertyRepository(property);
        var unitOfWork = new SpyUnitOfWork();
        var transactions = new SpyTransactionManager();
        var inventoryLock = new StubInventoryLock(true);

        var handler = CreateHandler(
            repository,
            unitOfWork,
            transactions,
            inventoryLock);

        var command = new UpdatePropertyCommand(
            property.Id,
            "Casa Renovada",
            "America/El_Salvador",
            new TimeOnly(14, 0),
            new TimeOnly(10, 0));

        // ACT
        Result result = await handler.HandleAsync(
            command,
            TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal("Casa Renovada", property.Name);
        Assert.Equal(new TimeOnly(14, 0), property.CheckInTime);
        Assert.Equal(new TimeOnly(10, 0), property.CheckOutTime);

        Assert.Equal(property.Id, inventoryLock.RequestedPropertyId);
        Assert.Equal(property.Id, repository.RequestedPropertyId);

        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, transactions.Transaction.CommitCount);
        Assert.Equal(0, transactions.Transaction.RollbackCount);
        Assert.True(transactions.Transaction.IsDisposed);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidDetails_ShouldRollbackWithoutSaving()
    {
        // ARRANGE
        Property property = PropertyTestFactory.CreateValidProperty();

        string originalName = property.Name;
        string originalTimeZone = property.TimeZoneId;
        TimeOnly originalCheckIn = property.CheckInTime;
        TimeOnly originalCheckOut = property.CheckOutTime;

        var repository = new StubPropertyRepository(property);
        var unitOfWork = new SpyUnitOfWork();
        var transactions = new SpyTransactionManager();
        var inventoryLock = new StubInventoryLock(true);

        var handler = CreateHandler(
            repository,
            unitOfWork,
            transactions,
            inventoryLock);

        var command = new UpdatePropertyCommand(
            property.Id,
            "Nombre Nuevo",
            "Invalid/Nonexistent_TimeZone",
            new TimeOnly(13, 0),
            new TimeOnly(9, 0));

        // ACT
        Result result = await handler.HandleAsync(
            command,
            TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PropertyErrors.InvalidTimeZoneId,
            result.Error);

        Assert.Equal(originalName, property.Name);
        Assert.Equal(originalTimeZone, property.TimeZoneId);
        Assert.Equal(originalCheckIn, property.CheckInTime);
        Assert.Equal(originalCheckOut, property.CheckOutTime);

        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, transactions.Transaction.CommitCount);
        Assert.Equal(1, transactions.Transaction.RollbackCount);
        Assert.True(transactions.Transaction.IsDisposed);
    }

    [Fact]
    public async Task HandleAsync_WhenLockCannotBeAcquired_ShouldNotReadOrSave()
    {
        // ARRANGE
        Property property = PropertyTestFactory.CreateValidProperty();

        var repository = new StubPropertyRepository(property);
        var unitOfWork = new SpyUnitOfWork();
        var transactions = new SpyTransactionManager();
        var inventoryLock = new StubInventoryLock(false);

        var handler = CreateHandler(
            repository,
            unitOfWork,
            transactions,
            inventoryLock);

        var command = new UpdatePropertyCommand(
            property.Id,
            "Nombre Nuevo",
            property.TimeZoneId,
            property.CheckInTime,
            property.CheckOutTime);

        // ACT
        Result result = await handler.HandleAsync(
            command,
            TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PropertyManagementErrors.NotFound(property.Id),
            result.Error);

        Assert.Equal(0, repository.ReadCount);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, transactions.Transaction.CommitCount);
        Assert.Equal(1, transactions.Transaction.RollbackCount);
        Assert.True(transactions.Transaction.IsDisposed);
    }

    [Fact]
    public async Task HandleAsync_WhenPropertyDoesNotExist_ShouldRollback()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();

        var repository = new StubPropertyRepository(null);
        var unitOfWork = new SpyUnitOfWork();
        var transactions = new SpyTransactionManager();
        var inventoryLock = new StubInventoryLock(true);

        var handler = CreateHandler(
            repository,
            unitOfWork,
            transactions,
            inventoryLock);

        var command = new UpdatePropertyCommand(
            propertyId,
            "Casa",
            "America/El_Salvador",
            new TimeOnly(15, 0),
            new TimeOnly(11, 0));

        // ACT
        Result result = await handler.HandleAsync(
            command,
            TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PropertyManagementErrors.NotFound(propertyId),
            result.Error);

        Assert.Equal(1, repository.ReadCount);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Equal(0, transactions.Transaction.CommitCount);
        Assert.Equal(1, transactions.Transaction.RollbackCount);
        Assert.True(transactions.Transaction.IsDisposed);
    }

    private static UpdatePropertyCommandHandler CreateHandler(
        IPropertyRepository repository,
        IUnitOfWork unitOfWork,
        ITransactionManager transactions,
        IBookingInventoryLock inventoryLock)
    {
        return new UpdatePropertyCommandHandler(
            repository,
            unitOfWork,
            transactions,
            inventoryLock);
    }

    private sealed class StubPropertyRepository : IPropertyRepository
    {
        private readonly Property? _property;

        public StubPropertyRepository(Property? property)
        {
            _property = property;
        }

        public int ReadCount { get; private set; }
        public Guid? RequestedPropertyId { get; private set; }

        public Task<Property?> GetByIdAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReadCount++;
            RequestedPropertyId = propertyId;

            return Task.FromResult(_property);
        }

        public void Add(Property property)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubInventoryLock : IBookingInventoryLock
    {
        private readonly bool _acquired;

        public StubInventoryLock(bool acquired)
        {
            _acquired = acquired;
        }

        public Guid? RequestedPropertyId { get; private set; }

        public Task<bool> TryAcquireAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RequestedPropertyId = propertyId;

            return Task.FromResult(_acquired);
        }
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class SpyTransactionManager : ITransactionManager
    {
        public SpyTransaction Transaction { get; } = new();

        public Task<ITransaction> BeginAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<ITransaction>(Transaction);
        }
    }

    private sealed class SpyTransaction : ITransaction
    {
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public bool IsDisposed { get; private set; }

        public Task CommitAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CommitCount++;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RollbackCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
