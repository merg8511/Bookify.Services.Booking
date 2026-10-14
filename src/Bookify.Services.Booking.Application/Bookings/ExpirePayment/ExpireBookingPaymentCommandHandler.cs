using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Observability;
using Bookify.Services.Booking.Application.Payments.Cancellation;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Bookings.ExpirePayment;

public sealed class ExpireBookingPaymentCommandHandler : ICommandHandler<ExpireBookingPaymentCommand>
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
        _bookingRepository = bookingRepository ??
            throw new ArgumentNullException(nameof(bookingRepository));

        _paymentRepository = paymentRepository ??
            throw new ArgumentNullException(nameof(paymentRepository));

        _paymentCancellationCoordinator = paymentCancellationCoordinator ??
            throw new ArgumentNullException(nameof(paymentCancellationCoordinator));

        _unitOfWork = unitOfWork ??
            throw new ArgumentNullException(nameof(unitOfWork));

        _transactionManager = transactionManager ??
            throw new ArgumentNullException(nameof(transactionManager));

        _paymentInitiationLock = paymentInitiationLock ??
            throw new ArgumentNullException(nameof(paymentInitiationLock));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
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
                return await RollbackFailureAsync(
                    transaction,
                    ExpireBookingPaymentErrors.NotFound(command.BookingId),
                    cancellationToken);
            }

            if (booking.Status != BookingStatus.PendingPayment)
            {
                return await RollbackSuccessAsync(
                    transaction,
                    cancellationToken);
            }

            DateTimeOffset nowUtc = _clock.UtcNow;

            if (booking.PaymentDueAtUtc is null ||
                booking.PaymentDueAtUtc > nowUtc)
            {
                return await RollbackSuccessAsync(
                    transaction,
                    cancellationToken);
            }

            Payment? payment = await _paymentRepository.GetByBookingIdAsync(booking.Id, cancellationToken);

            if (payment is null)
            {
                return await ExpireAndCommitAsync(transaction, booking, nowUtc, cancellationToken);
            }

            PaymentStatus paymentStatusBefore = payment.Status;

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

                BookingMetrics.RecordPaymentTransition(paymentStatusBefore, payment.Status);

                return Result.Success();
            }

            return await ExpireAndCommitAsync(
                transaction,
                booking,
                nowUtc,
                cancellationToken,
                payment,
                paymentStatusBefore);
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
        DateTimeOffset expiredAtUtc,
        CancellationToken cancellationToken,
        Payment? payment = null,
        PaymentStatus? paymentStatusBefore = null)
    {
        Result expirationResult = booking.ExpirePayment(expiredAtUtc);

        if (expirationResult.IsFailure)
        {
            return await RollbackFailureAsync(
                transaction,
                expirationResult.Error,
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (payment is not null && paymentStatusBefore.HasValue)
        {
            BookingMetrics.RecordPaymentTransition(paymentStatusBefore.Value, payment.Status);
        }

        return Result.Success();
    }

    private static async Task<Result> RollbackSuccessAsync(
        ITransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);

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
