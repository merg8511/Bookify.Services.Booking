using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Administrative.GetById;

public sealed class GetAdminBookingByIdQueryHandler :
    IQueryHandler<GetAdminBookingByIdQuery, BookingDetailsReadModel>
{
    private readonly IPropertyReadService _propertyReadService;
    private readonly IBookingReadService _bookingReadService;

    public GetAdminBookingByIdQueryHandler(
        IPropertyReadService propertyReadService,
        IBookingReadService bookingReadService)
    {
        _propertyReadService = propertyReadService ??
            throw new ArgumentNullException(nameof(propertyReadService));

        _bookingReadService = bookingReadService ??
            throw new ArgumentNullException(nameof(bookingReadService));
    }

    public async Task<Result<BookingDetailsReadModel>> HandleAsync(
        GetAdminBookingByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var property = await _propertyReadService.GetByIdAsync(query.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result<BookingDetailsReadModel>.Failure(
                AdminBookingErrors.PropertyNotFound(query.PropertyId));
        }

        BookingDetailsReadModel? booking = await _bookingReadService.GetByIdAsync(query.BookingId, cancellationToken);

        if (booking is null || booking.PropertyId != query.PropertyId)
        {
            return Result<BookingDetailsReadModel>.Failure(
                AdminBookingErrors.BookingNotFound(query.BookingId));
        }

        return Result<BookingDetailsReadModel>.Success(booking);
    }
}
