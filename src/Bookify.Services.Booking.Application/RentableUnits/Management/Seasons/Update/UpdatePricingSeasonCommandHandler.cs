using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Update;

public sealed class UpdatePricingSeasonCommandHandler :
    ICommandHandler<UpdatePricingSeasonCommand>
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRentableUnitRepository _unitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IBookingInventoryLock _inventoryLock;

    public UpdatePricingSeasonCommandHandler(
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

    public async Task<Result> HandleAsync(
        UpdatePricingSeasonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Money> moneyResult = PricingSeasonMoneyFactory
            .Create(command.NightlyRate, command.Currency);

        if (moneyResult.IsFailure)
        {
            return Result.Failure(moneyResult.Error);
        }

        await using ITransaction transaction = await _transactionManager.BeginAsync(cancellationToken);

        bool propertyLocked = await _inventoryLock.TryAcquireAsync(command.PropertyId, cancellationToken);

        if (!propertyLocked)
        {
            return Result.Failure(RentableUnitManagementErrors.PropertyNotFound(command.PropertyId));
        }

        Property? property = await _propertyRepository.GetByIdAsync(command.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result.Failure(RentableUnitManagementErrors.PropertyNotFound(command.PropertyId));
        }

        RentableUnit? unit = await _unitRepository.GetByIdAsync(command.UnitId, cancellationToken);

        if (unit is null || unit.PropertyId != command.PropertyId)
        {
            return Result.Failure(RentableUnitManagementErrors.UnitNotFound(command.UnitId));
        }

        Result updateResult = unit.UpdateManagedPricingSeason(
            command.SeasonId,
            command.StartDate,
            command.EndDate,
            moneyResult.Value,
            command.Priority);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
