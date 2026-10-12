namespace Bookify.Services.Booking.Application.Bookings.ExpireApproval;

using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

public sealed class ExpireBookingApprovalCommandHandler : ICommandHandler<ExpireBookingApprovalCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExpireBookingApprovalCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _bookingRepository = bookingRepository ??
            throw new ArgumentNullException(nameof(bookingRepository));

        _unitOfWork = unitOfWork ??
            throw new ArgumentNullException(nameof(unitOfWork));

        _clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result> HandleAsync(
        ExpireBookingApprovalCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        DomainBooking? booking = await _bookingRepository
            .GetByIdAsync(command.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(
                ExpireBookingApprovalErrors.NotFound(command.BookingId));
        }

        if (booking.Status != BookingStatus.PendingApproval)
        {
            return Result.Success();
        }

        DateTimeOffset nowUtc = _clock.UtcNow;

        if (booking.ApprovalDueAtUtc is null ||
            booking.ApprovalDueAtUtc > nowUtc)
        {
            return Result.Success();
        }

        Result expirationResult = booking.ExpireApproval(nowUtc);

        if (expirationResult.IsFailure)
        {
            return expirationResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
