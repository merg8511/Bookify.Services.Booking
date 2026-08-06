using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Availability.Get;

internal sealed class GetAvailabilityQueryHandler :
    IQueryHandler<
        GetAvailabilityQuery,
        AvailabilityReadModel>
{

    private readonly IPropertyReadService _propertyReadService;
    private readonly IAvailabilityReadService _availabilityReadService;
    public GetAvailabilityQueryHandler(
        IPropertyReadService propertyReadService,
        IAvailabilityReadService availabilityReadService)
    {
        _availabilityReadService = availabilityReadService
            ?? throw new ArgumentNullException(nameof(availabilityReadService));
        _propertyReadService = propertyReadService
            ?? throw new ArgumentNullException(nameof(propertyReadService));
    }

    public async Task<Result<AvailabilityReadModel>>
        HandleAsync(
        GetAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        PropertyDetailsReadModel? property =
            await _propertyReadService.GetByIdAsync(
                query.PropertyId,
                cancellationToken);

        if (property is null)
        {
            return Result<AvailabilityReadModel>.Failure(
                GetAvailabilityErrors
                    .PropertyNotFound(query.PropertyId));
        }

        if (!property.IsActive)
        {
            return Result<AvailabilityReadModel>.Failure(
                GetAvailabilityErrors
                    .PropertyInactive(property.Id));
        }

        DateOnly checkInDate = query.CheckInDate!.Value;
        DateOnly checkOutDate = query.CheckOutDate!.Value;
        int guestCount = query.GuestCount!.Value;

        IReadOnlyList<
            AvailableRentableUnitReadModel>
            availableUnits =
                await _availabilityReadService
                    .GetAvailableUnitsAsync(
                        query.PropertyId,
                        checkInDate,
                        checkOutDate,
                        guestCount,
                        cancellationToken);

        int numberOfNights = checkOutDate.DayNumber - checkInDate.DayNumber;

        var response =
            new AvailabilityReadModel(
                property.Id,
                property.Name,
                checkInDate,
                checkOutDate,
                numberOfNights,
                guestCount,
                availableUnits);

        return Result<AvailabilityReadModel>.Success(response);
    }
}
