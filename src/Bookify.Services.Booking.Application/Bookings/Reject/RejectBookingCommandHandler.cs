using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;
namespace Bookify.Services.Booking.Application.Bookings.Reject;

public sealed class RejectBookingCommandHandler : ICommandHandler<RejectBookingCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectBookingCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        RejectBookingCommand command,
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
                RejectBookingErrors.NotFound(command.BookingId));
        }

        Result rejectionResult = booking.Reject();

        if (rejectionResult.IsFailure)
        {
            return rejectionResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
