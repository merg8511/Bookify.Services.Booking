using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.Approve;

public sealed class ApproveBookingCommandHandler
    : ICommandHandler<ApproveBookingCommand>
{

    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveBookingCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }
    public async Task<Result> HandleAsync(
        ApproveBookingCommand command,
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
                ApproveBookingErrors.NotFound(command.BookingId));
        }

        Result approvalResult = booking.Approve();

        if (approvalResult.IsFailure)
        {
            return approvalResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
