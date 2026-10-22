using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Create;

public sealed class CreatePricingSeasonCommandHandler : ICommandHandler<CreatePricingSeasonCommand, int>
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRentableUnitRepository _unitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IBookingInventoryLock _inventoryLock;

    public CreatePricingSeasonCommandHandler(
        IPropertyRepository propertyRepository,
        IRentableUnitRepository unitRepository,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IBookingInventoryLock inventoryLock)
    {
        _propertyRepository = propertyRepository;
        _unitRepository = unitRepository;
        _unitOfWork = unitOfWork;
        _transactionManager = transactionManager;
        _inventoryLock = inventoryLock;
    }

    public async Task<Result<int>> HandleAsync(
        CreatePricingSeasonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Money> moneyResult = PricingSeasonMoneyFactory.Create(
            command.NightlyRate, command.Currency);

        if (moneyResult.IsFailure)
        {
            return Result<int>.Failure(moneyResult.Error);
        }

        Result<PricingSeason> seasonResult = PricingSeason.Create(
            command.StartDate, command.EndDate, moneyResult.Value, command.Priority);

        await using ITransaction transaction = await _transactionManager.BeginAsync(cancellationToken);

        bool propertyLock = await _inventoryLock.TryAcquireAsync(command.PropertyId, cancellationToken);

        if (!propertyLock)
        {
            return Result<int>.Failure(RentableUnitManagementErrors.PropertyNotFound(command.PropertyId));
        }

        Property? property = await _propertyRepository.GetByIdAsync(command.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<int>.Failure(RentableUnitManagementErrors.PropertyNotFound(command.PropertyId));
        }

        RentableUnit? unit = await _unitRepository.GetByIdAsync(command.UnitId, cancellationToken);

        if (unit is null || unit.PropertyId != command.PropertyId)
        {
            return Result<int>.Failure(RentableUnitManagementErrors.UnitNotFound(command.UnitId));
        }

        PricingSeason season = seasonResult.Value;

        Result addResult = unit.AddManagedPricingSeason(season);

        if (addResult.IsFailure)
        {
            return Result<int>.Failure(addResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (season.Id <= 0)
        {
            throw new InvalidOperationException("The pricing season identifier was not generated.");
        }

        await transaction.CommitAsync(cancellationToken);

        return Result<int>.Success(season.Id);
    }
}
