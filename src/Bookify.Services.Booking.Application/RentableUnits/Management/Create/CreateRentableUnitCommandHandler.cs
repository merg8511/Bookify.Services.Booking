using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Create;

public sealed class CreateRentableUnitCommandHandler : ICommandHandler<CreateRentableUnitCommand, Guid>
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRentableUnitRepository _unitRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IBookingInventoryLock _inventoryLock;

    public CreateRentableUnitCommandHandler(
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

    public async Task<Result<Guid>> HandleAsync(
        CreateRentableUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<RentableUnitType> typeResult = RentableUnitTypeParser.Parse(command.Type);

        if (typeResult.IsFailure)
        {
            return Result<Guid>.Failure(typeResult.Error);
        }

        Result<RentableUnit> unitResult = RentableUnit.Create(
            command.PropertyId,
            command.Name,
            typeResult.Value,
            command.MaximumCapacity,
            command.MaxBaseGuests);

        if (unitResult.IsFailure)
        {
            return Result<Guid>.Failure(unitResult.Error);
        }

        await using ITransaction transaction = await _transactionManager.BeginAsync(cancellationToken);

        bool propertyLocked = await _inventoryLock.TryAcquireAsync(command.PropertyId, cancellationToken);

        if (!propertyLocked)
        {
            return Result<Guid>.Failure(RentableUnitManagementErrors.PropertyNotFound(command.PropertyId));
        }

        Property? property = await _propertyRepository.GetByIdAsync(command.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<Guid>.Failure(RentableUnitManagementErrors.PropertyNotFound(command.PropertyId));
        }

        RentableUnit unit = unitResult.Value;

        _unitRepository.Add(unit);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<Guid>.Success(unit.Id);
    }
}
