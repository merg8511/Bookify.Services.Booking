using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Properties.Management;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.Update;

public sealed class UpdatePropertyCommandHandler : ICommandHandler<UpdatePropertyCommand>
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IBookingInventoryLock _inventoryLock;

    public UpdatePropertyCommandHandler(
        IPropertyRepository propertyRepository,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IBookingInventoryLock inventoryLock)
    {
        _propertyRepository = propertyRepository;
        _unitOfWork = unitOfWork;
        _transactionManager = transactionManager;
        _inventoryLock = inventoryLock;
    }

    public async Task<Result> HandleAsync(
        UpdatePropertyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using ITransaction transaction = await _transactionManager
            .BeginAsync(cancellationToken);

        bool propertyLocked = await _inventoryLock
            .TryAcquireAsync(command.PropertyId, cancellationToken);

        if (!propertyLocked)
        {
            await transaction.RollbackAsync(cancellationToken);

            return Result.Failure(
                PropertyManagementErrors.NotFound(command.PropertyId));
        }

        Property? property = await _propertyRepository
            .GetByIdAsync(command.PropertyId, cancellationToken);

        if (property is null)
        {
            await transaction.RollbackAsync(cancellationToken);

            return Result.Failure(
                PropertyManagementErrors.NotFound(command.PropertyId));
        }

        Result updateResult = property.UpdateDetails(
            command.Name,
            command.TimeZoneId,
            command.CheckInTime,
            command.CheckOutTime);

        if (updateResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);

            return updateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
