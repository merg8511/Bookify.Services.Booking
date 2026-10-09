using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.Complete;

public sealed class CompleteBookingCommandHandler : ICommandHandler<CompleteBookingCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteBookingCommandHandler(
        IBookingRepository bookingRepository,
        IPropertyRepository propertyRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _bookingRepository = bookingRepository ??
            throw new ArgumentNullException(nameof(bookingRepository));

        _propertyRepository = propertyRepository ??
            throw new ArgumentNullException(nameof(propertyRepository));

        _unitOfWork = unitOfWork ??
            throw new ArgumentNullException(nameof(unitOfWork));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> HandleAsync(
        CompleteBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DomainBooking? booking = await _bookingRepository
            .GetByIdAsync(command.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(CompleteBookingErrors.NotFound(command.BookingId));
        }

        if (booking.Status != BookingStatus.Paid)
        {
            return Result.Success();
        }

        Property? property = await _propertyRepository.GetByIdAsync(booking.PropertyId, cancellationToken);

        if (property is null)
        {
            return Result.Failure(CompleteBookingErrors.PropertyNotFound(booking.PropertyId));
        }

        Result<DateTimeOffset> checkoutAtUtcResult = GetCheckoutAtUtc(booking, property);

        if (checkoutAtUtcResult.IsFailure)
        {
            return Result.Failure(checkoutAtUtcResult.Error);
        }

        DateTimeOffset nowUtc = _clock.UtcNow;
        DateTimeOffset checkoutAtUtc = checkoutAtUtcResult.Value;

        if (checkoutAtUtc > nowUtc)
        {
            return Result.Success();
        }

        Result completionResult = booking.Complete(nowUtc);

        if (completionResult.IsFailure)
        {
            return completionResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static Result<DateTimeOffset> GetCheckoutAtUtc(DomainBooking booking, Property property)
    {
        try
        {
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);

            DateTime checkoutLocal = DateTime.SpecifyKind(
                booking.StayPeriod.CheckOutDate.ToDateTime(property.CheckOutTime),
                DateTimeKind.Unspecified);

            DateTime checkoutUtc = TimeZoneInfo.ConvertTimeToUtc(checkoutLocal, timeZone);

            return Result<DateTimeOffset>.Success(new DateTimeOffset(checkoutUtc));
        }
        catch (TimeZoneNotFoundException)
        {
            return Result<DateTimeOffset>.Failure(
                CompleteBookingErrors.InvalidPropertyTimeZone(property.Id, property.TimeZoneId));
        }
        catch (InvalidTimeZoneException)
        {
            return Result<DateTimeOffset>.Failure(
                CompleteBookingErrors.InvalidPropertyTimeZone(property.Id, property.TimeZoneId));
        }
        catch (ArgumentException)
        {
            return Result<DateTimeOffset>.Failure(
                CompleteBookingErrors.InvalidPropertyTimeZone(property.Id, property.TimeZoneId));
        }
    }
}
