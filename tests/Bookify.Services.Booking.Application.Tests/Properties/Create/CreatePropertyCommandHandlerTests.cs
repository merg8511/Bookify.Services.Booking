using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Properties.Create;

public sealed class CreatePropertyCommandHandlerTests
{
    private static readonly TimeOnly CheckInTime = new(15, 0);
    private static readonly TimeOnly CheckOutTime = new(11, 0);

    [Fact]
    public async Task HandleAsync_WithValidCommand_ShouldAddPropertyAndSaveChanges()
    {
        // ARRANGE
        var propertyRepository = new SpyPropertyRepository();
        var unitOfWork = new SpyUnitOfWork();

        var handler = new CreatePropertyCommandHandler(
            propertyRepository,
            unitOfWork);

        var command = new CreatePropertyCommand(
            "  Rancho Costa Azul  ",
            "  America/El_Salvador  ",
            CheckInTime,
            CheckOutTime);

        // ACT
        Result<Guid> result = await handler.HandleAsync(command);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        Assert.NotNull(
            propertyRepository.AddedProperty);

        Assert.Equal(
            result.Value,
            propertyRepository.AddedProperty.Id);

        Assert.Equal(
            "Rancho Costa Azul",
            propertyRepository.AddedProperty.Name);

        Assert.Equal(
            "America/El_Salvador",
            propertyRepository.AddedProperty.TimeZoneId);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidName_ShouldReturnFailureWithoutSaving()
    {
        // ARRANGE
        var propertyRepository = new SpyPropertyRepository();
        var unitOfWork = new SpyUnitOfWork();

        var handler = new CreatePropertyCommandHandler(
            propertyRepository,
            unitOfWork);

        var command = new CreatePropertyCommand(
            "     ",
            "America/El_Salvador",
            CheckInTime,
            CheckOutTime);

        // ACT
        Result<Guid> result = await handler.HandleAsync(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PropertyErrors.InvalidName,
            result.Error);

        Assert.Null(
            propertyRepository.AddedProperty);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidTimeZone_ShouldReturnFailureWithoutSaving()
    {
        // ARRANGE
        var propertyRepository = new SpyPropertyRepository();
        var unitOfWork = new SpyUnitOfWork();

        var handler = new CreatePropertyCommandHandler(propertyRepository, unitOfWork);

        var command = new CreatePropertyCommand(
            "Rancho Costa Azul",
            "    ",
            CheckInTime,
            CheckOutTime);

        // ACT
        Result<Guid> result = await handler.HandleAsync(command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PropertyErrors.InvalidTimeZoneId,
            result.Error);

        Assert.Null(
            propertyRepository.AddedProperty);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_ShouldThrow()
    {
        // ARRANGE
        var handler = new CreatePropertyCommandHandler(
            new SpyPropertyRepository(),
            new SpyUnitOfWork());

        // ACT

        Task Action()
        {
            return handler.HandleAsync(null!);
        }

        // ASSERT
        await Assert.ThrowsAsync<
            ArgumentNullException>(Action);
    }

    private sealed class SpyPropertyRepository : IPropertyRepository
    {
        public Property? AddedProperty { get; private set; }
        public void Add(Property property)
        {
            AddedProperty = property;
        }

        public Task<Property?> GetByIdAsync(Guid propertyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Property?>(null);
        }
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SaveChangesCallCount++;

            return Task.CompletedTask;

        }
    }
}
