using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Bookings.Policies;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.Approve;

public sealed class ApproveBookingCommandHandler
    : ICommandHandler<ApproveBookingCommand>
{

    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IBookingDeadlinePolicy _deadlinePolicy;

    public ApproveBookingCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        IBookingDeadlinePolicy deadlinePolicy)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _deadlinePolicy = deadlinePolicy;
    }
    public async Task<Result> HandleAsync(
        ApproveBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DomainBooking? booking = await _bookingRepository.GetByIdAsync(command.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(ApproveBookingErrors.NotFound(command.BookingId));
        }

        DateTimeOffset approvedAtUtc = _clock.UtcNow;
        Result approvalResult = booking.Approve(approvedAtUtc);

        if (approvalResult.IsFailure)
        {
            return approvalResult;
        }

        DateTimeOffset paymentDueAtUtc = _deadlinePolicy.GetPaymentDueAtUtc(approvedAtUtc);
        Result paymentDeadlineResult = booking.SchedulePaymentDeadline(paymentDueAtUtc);

        if (paymentDeadlineResult.IsFailure)
        {
            return paymentDeadlineResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
