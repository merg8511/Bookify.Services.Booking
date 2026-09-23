using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Get;

public sealed class GetBookingQueryHandler : IQueryHandler<GetBookingQuery, BookingDetailsReadModel>
{
    private readonly IBookingReadService _bookingReadService;

    public GetBookingQueryHandler(IBookingReadService bookingReadService)
    {
        _bookingReadService = bookingReadService ??
            throw new ArgumentNullException(nameof(bookingReadService));
    }

    public async Task<Result<BookingDetailsReadModel>> HandleAsync(
        GetBookingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Identifier))
        {
            return Result<BookingDetailsReadModel>.Failure(
                GetBookingErrors.InvalidIdentifier);
        }

        string identifier = query.Identifier.Trim();

        BookingDetailsReadModel? booking;

        if (Guid.TryParse(identifier, out Guid bookingId))
        {
            if (bookingId == Guid.Empty)
            {
                return Result<BookingDetailsReadModel>.Failure(
                    GetBookingErrors.InvalidIdentifier);
            }

            booking = await _bookingReadService.GetByIdAsync(bookingId, cancellationToken);
        }
        else
        {
            Result<BookingReference> referenceResult = BookingReference.Create(identifier);

            if (referenceResult.IsFailure)
            {
                return Result<BookingDetailsReadModel>.Failure(GetBookingErrors.InvalidIdentifier);
            }

            booking = await _bookingReadService.GetByReferenceAsync(
                referenceResult.Value.Value, cancellationToken);
        }

        if (booking is null)
        {
            return Result<BookingDetailsReadModel>.Failure(GetBookingErrors.NotFound(identifier));
        }

        return Result<BookingDetailsReadModel>.Success(booking);
    }
}
