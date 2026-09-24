using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Bookings.Get;

public sealed class GetBookingQueryValidator : IRequestValidator<GetBookingQuery>
{
    public Result Validate(GetBookingQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return Result.Failure(GetBookingErrors.InvalidIdentifier);
        }

        string identifier = request.Identifier.Trim();

        if (Guid.TryParse(identifier, out Guid bookingId))
        {
            return bookingId != Guid.Empty
                ? Result.Success()
                : Result.Failure(GetBookingErrors.InvalidIdentifier);
        }

        Result<BookingReference> referenceResult = BookingReference.Create(identifier);

        if (referenceResult.IsFailure)
        {
            return Result.Failure(GetBookingErrors.InvalidIdentifier);
        }

        return Result.Success();
    }
}
