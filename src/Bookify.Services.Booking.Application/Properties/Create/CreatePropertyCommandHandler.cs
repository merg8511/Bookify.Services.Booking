using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Properties.Create;

public sealed class CreatePropertyCommandHandler
    : ICommandHandler<CreatePropertyCommand, Guid>
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePropertyCommandHandler(
        IPropertyRepository propertyRepository,
        IUnitOfWork unitOfWork)
    {
        _propertyRepository = propertyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> HandleAsync(
        CreatePropertyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Property> propertyResult = Property.Create(
            command.Name,
            command.TimeZoneId,
            command.CheckInTime,
            command.CheckOutTime);

        if (propertyResult.IsFailure)
        {
            return Result<Guid>.Failure(
                propertyResult.Error);
        }

        Property property = propertyResult.Value;

        _propertyRepository.Add(property);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(
            property.Id);
    }
}
