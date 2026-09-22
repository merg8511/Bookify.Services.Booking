using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Shared;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.Complete;

public sealed class CompleteBookingCommandHandler
    : ICommandHandler<CompleteBookingCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteBookingCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
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
            return Result.Failure(
                CompleteBookingErrors
                .NotFound(command.BookingId));
        }

        Result completionResult = booking.Complete(_clock.UtcNow);

        if (completionResult.IsFailure)
        {
            return completionResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
