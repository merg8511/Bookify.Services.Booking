using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Payments.Cancellation;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.ExpirePayment;

public sealed class ExpireBookingPaymentCommandHandler
    : ICommandHandler<ExpireBookingPaymentCommand>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly PaymentCancellationCoordinator _paymentCancellationCoordinator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionManager _transactionManager;
    private readonly IPaymentInitiationLock _paymentInitiationLock;
    private readonly IClock _clock;

    public ExpireBookingPaymentCommandHandler(
        IBookingRepository bookingRepository,
        IPaymentRepository paymentRepository,
        PaymentCancellationCoordinator paymentCancellationCoordinator,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IPaymentInitiationLock paymentInitiationLock,
        IClock clock)
    {
        _bookingRepository = bookingRepository;
        _paymentRepository = paymentRepository;
        _paymentCancellationCoordinator = paymentCancellationCoordinator;
        _unitOfWork = unitOfWork;
        _transactionManager = transactionManager;
        _paymentInitiationLock = paymentInitiationLock;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        ExpireBookingPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using ITransaction transaction = await _transactionManager.BeginAsync(cancellationToken);

        try
        {
            bool bookingLocked = await _paymentInitiationLock
                .TryAcquireAsync(command.BookingId, cancellationToken);

            if (!bookingLocked)
            {
                return await RollbackFailureAsync(
                    transaction,
                    ExpireBookingPaymentErrors.NotFound(command.BookingId), cancellationToken);
            }

            DomainBooking? booking = await _bookingRepository.GetByIdAsync(
                command.BookingId,
                cancellationToken);

            if (booking is null)
            {
                return Result.Failure(
                    ExpireBookingPaymentErrors.NotFound(command.BookingId));
            }

            if (booking.Status != BookingStatus.PendingPayment)
            {
                return await ExpireAndCommitAsync(
                    transaction,
                    booking,
                    cancellationToken);
            }

            Payment? payment = await _paymentRepository.GetByBookingIdAsync(booking.Id, cancellationToken);

            if (payment is null)
            {
                return await ExpireAndCommitAsync(transaction, booking, cancellationToken);
            }

            Result<PaymentCancellationOutcome> paymentCancellationResult =
                await _paymentCancellationCoordinator.EnsurePaymentCannotSucceedAsync(
                    payment,
                    booking,
                    cancellationToken);

            if (paymentCancellationResult.IsFailure)
            {
                return await RollbackFailureAsync(transaction, paymentCancellationResult.Error, cancellationToken);
            }

            PaymentCancellationOutcome outcome = paymentCancellationResult.Value;

            if (outcome == PaymentCancellationOutcome.PaymentSucceeded)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result.Failure(ExpireBookingPaymentErrors.PaymentAlreadySucceeded(booking.Id));
            }

            return await ExpireAndCommitAsync(transaction, booking, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Result> ExpireAndCommitAsync(
        ITransaction transaction,
        DomainBooking booking,
        CancellationToken cancellationToken)
    {
        Result expirationResult = booking.ExpirePayment(_clock.UtcNow);

        if (expirationResult.IsFailure)
        {
            return await RollbackFailureAsync(
                transaction,
                expirationResult.Error,
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    private static async Task<Result> RollbackFailureAsync(
        ITransaction transaction,
        Error error,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);

        return Result.Failure(error);
    }
}
