using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.RentableUnits.Management.Update;

public sealed class UpdateRentableUnitCommandHandler : ICommandHandler<UpdateRentableUnitCommand>
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRentableUnitRepository _unitRepositoy;
    private readonly IRentableUnitReadService _unitReadService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IBookingInventoryLock _inventoryLock;

    public UpdateRentableUnitCommandHandler(
        IPropertyRepository propertyRepository,
        IRentableUnitRepository unitRepository,
        IRentableUnitReadService unitReadService,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IBookingInventoryLock inventoryLock)
    {
        _propertyRepository = propertyRepository;
        _unitRepositoy = unitRepository;
        _unitReadService = unitReadService;
        _unitOfWork = unitOfWork;
        _transactionManager = transactionManager;
        _inventoryLock = inventoryLock;
    }

    public async Task<Result> HandleAsync(
        UpdateRentableUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<RentableUnitType> typeResult = RentableUnitTypeParser.Parse(command.Type);

        if (typeResult.IsFailure)
        {
            return Result.Failure(typeResult.Error);
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

        RentableUnit? unit = await _unitRepositoy.GetByIdAsync(command.UnitId, cancellationToken);

        if (unit is null || unit.PropertyId != command.PropertyId)
        {
            return Result.Failure(RentableUnitManagementErrors.UnitNotFound(command.UnitId));
        }

        RentableUnitType originalType = unit.Type;
        int originalCapacity = unit.MaximumCapacity;

        if (originalType != typeResult.Value)
        {
            bool hasBookings = await _unitReadService.HasAnyBookingsAsync(unit.Id, cancellationToken);

            if (hasBookings)
            {
                return Result.Failure(RentableUnitManagementErrors.TypeChangeHasBookings);
            }
        }

        if (command.MaximumCapacity < originalCapacity)
        {
            int maximumBookedGuests = await _unitReadService.GetMaximumBookedGuestCountAsync(unit.Id, cancellationToken);

            if (command.MaximumCapacity < maximumBookedGuests)
            {
                return Result.Failure(RentableUnitManagementErrors.CapacityBelowBookedGuestCount);
            }
        }

        Result updateResult = unit.UpdateDetails(
            command.Name,
            typeResult.Value,
            command.MaximumCapacity,
            command.MaxBaseGuests);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
