using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.Calendar;

public sealed class GetAdminBookingCalendarQueryHandler :
    IQueryHandler<GetAdminBookingCalendarQuery, IReadOnlyList<BookingCalendarItemReadModel>>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IBookingReadService _bookingReadService;

    public GetAdminBookingCalendarQueryHandler(
        IPropertyReadService propertyReadService,
        IBookingReadService bookingReadService)
    {
        _propertyReadService = propertyReadService ??
            throw new ArgumentNullException(nameof(propertyReadService));

        _bookingReadService = bookingReadService ??
            throw new ArgumentNullException(nameof(bookingReadService));
    }

    public async Task<Result<IReadOnlyList<BookingCalendarItemReadModel>>> HandleAsync(
        GetAdminBookingCalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        DateOnly rangeStart = query.RangeStart!.Value;
        DateOnly rangeEnd = query.RangeEnd!.Value;

        var property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<IReadOnlyList<BookingCalendarItemReadModel>>.Failure(
                AdminBookingErrors.PropertyNotFound(query.PropertyId));
        }

        IReadOnlyList<BookingCalendarItemReadModel> calendar = await _bookingReadService.GetCalendarAsync(
            query.PropertyId,
            rangeStart,
            rangeEnd,
            cancellationToken);

        IEnumerable<BookingCalendarItemReadModel> filtered = calendar;

        if (query.UnitId.HasValue)
        {
            Guid unitId = query.UnitId.Value;
            filtered = filtered.Where(booking => booking.RentableUnitId == unitId);
        }

        if (query.BlocksInventory.HasValue)
        {
            bool blocksInventory = query.BlocksInventory.Value;
            filtered = filtered.Where(booking => booking.BlocksInventory == blocksInventory);
        }

        BookingCalendarItemReadModel[] items = filtered.ToArray();

        return Result<IReadOnlyList<BookingCalendarItemReadModel>>.Success(items);
    }
}
