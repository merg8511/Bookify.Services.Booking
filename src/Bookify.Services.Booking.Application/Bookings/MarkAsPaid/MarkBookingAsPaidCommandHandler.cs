using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.MarkAsPaid;

public sealed class MarkBookingAsPaidCommandHandler
    : ICommandHandler<MarkBookingAsPaidCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkBookingAsPaidCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        MarkBookingAsPaidCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DomainBooking? booking =
            await _bookingRepository
                .GetByIdAsync(
                    command.BookingId,
                    cancellationToken);

        if (booking is null)
        {
            return Result.Failure(
                MarkBookingAsPaidErrors.NotFound(
                    command.BookingId));
        }

        Result markAsPaidResult = booking.MarkAsPaid();

        if (markAsPaidResult.IsFailure)
        {
            return markAsPaidResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
