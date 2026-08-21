using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Shared;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.ExpirePayment;

public sealed class ExpireBookingPaymentCommandHandler
    : ICommandHandler<ExpireBookingPaymentCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ExpireBookingPaymentCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        ExpireBookingPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DomainBooking? booking =
            await _bookingRepository.GetByIdAsync(
                command.BookingId,
                cancellationToken);

        if (booking is null)
        {
            return Result.Failure(
                ExpireBookingPaymentErrors.NotFound(command.BookingId));
        }

        Result expirationResult = booking.ExpirePayment();

        if (expirationResult.IsFailure)
        {
            return expirationResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
