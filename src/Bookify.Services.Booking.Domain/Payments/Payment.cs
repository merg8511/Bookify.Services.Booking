using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Payments;

public sealed class Payment
{
    private readonly List<PaymentAttempt> _attempts = [];

    private Payment()
    {
        Amount = null!;
    }

    public Payment(
        Guid id,
        Guid bookingId,
        Money amount,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        BookingId = bookingId;
        Amount = amount;
        Status = PaymentStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public Money Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public IReadOnlyCollection<PaymentAttempt> Attempts => _attempts.AsReadOnly();

    public static Result<Payment> Create(
        Guid bookingId,
        Money amount,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (bookingId == Guid.Empty)
        {
            return Result<Payment>.Failure(
                PaymentErrors.BookingIdRequired);
        }

        if (amount.Amount <= 0)
        {
            return Result<Payment>.Failure(
                PaymentErrors.AmountMustBePositive);
        }

        return Result<Payment>.Success(
            new Payment(
                Guid.NewGuid(),
                bookingId,
                amount,
                createdAtUtc));
    }

    public Result<PaymentAttempt> AddAttempt(
        string externalReference,
        DateTimeOffset createdAtUtc)
    {
        if (Status is PaymentStatus.Succeeded or PaymentStatus.Cancelled)
        {
            return Result<PaymentAttempt>.Failure(
                PaymentErrors.CannotAddAttempt(Status));
        }

        if (_attempts.Any(
            attempt =>
                attempt.Status ==
                PaymentAttemptStatus.Pending))
        {
            return Result<PaymentAttempt>.Failure(
                PaymentErrors.ActiveAttemptAlreadyExists);
        }

        string normalizedExternalReference = externalReference?.Trim() ?? string.Empty;

        bool externalReferenceExists =
            _attempts.Any(
                attempt =>
                    string.Equals(
                        attempt.ExternalReference,
                        normalizedExternalReference,
                        StringComparison.Ordinal));

        if (externalReferenceExists)
        {
            return Result<PaymentAttempt>.Failure(
                PaymentErrors.DuplicateExternalReference(normalizedExternalReference));
        }

        if (createdAtUtc < CreatedAtUtc)
        {
            return Result<PaymentAttempt>.Failure(
                PaymentErrors.AttemptBeforePaymentCreation);
        }

        Result<PaymentAttempt> attemptResult =
            PaymentAttempt.Create(
                Id,
                normalizedExternalReference,
                Amount,
                createdAtUtc);

        if (attemptResult.IsFailure)
        {
            return attemptResult;
        }

        PaymentAttempt attempt = attemptResult.Value;

        _attempts.Add(attempt);

        Status = PaymentStatus.Pending;
        UpdatedAtUtc = createdAtUtc;
        CompletedAtUtc = null;

        return Result<PaymentAttempt>.Success(attempt);
    }

    public Result MarkAttemptAsSucceeded(
        string externalReference,
        DateTimeOffset completedAtUtc)
    {
        PaymentAttempt? attempt = FindAttempt(externalReference);

        if (attempt is null)
        {
            return Result.Failure(
                PaymentErrors.AttemptNotFound(externalReference));
        }

        Result result = attempt.MarkAsSucceeded(completedAtUtc);

        if (result.IsFailure)
        {
            return result;
        }

        Status = PaymentStatus.Succeeded;
        UpdatedAtUtc = completedAtUtc;
        CompletedAtUtc = completedAtUtc;

        return Result.Success();
    }

    public Result MarkAttemptAsFailed(
        string externalReference,
        DateTimeOffset completedAtUtc)
    {
        PaymentAttempt? attempt = FindAttempt(externalReference);

        if (attempt is null)
        {
            return Result.Failure(
                PaymentErrors.AttemptNotFound(externalReference));
        }

        Result result = attempt.MarkAsFailed(completedAtUtc);

        if (result.IsFailure)
        {
            return result;
        }

        Status = PaymentStatus.Failed;
        UpdatedAtUtc = completedAtUtc;
        CompletedAtUtc = null;

        return Result.Success();
    }

    public Result CancelAttempt(
        string externalReference,
        DateTimeOffset completedAtUtc)
    {
        PaymentAttempt? attempt = FindAttempt(externalReference);

        if (attempt is null)
        {
            return Result.Failure(
                PaymentErrors.AttemptNotFound(externalReference));
        }

        Result result = attempt.Cancel(completedAtUtc);

        if (result.IsFailure)
        {
            return result;
        }

        Status = PaymentStatus.Cancelled;
        UpdatedAtUtc = completedAtUtc;
        CompletedAtUtc = completedAtUtc;

        return Result.Success();
    }

    private PaymentAttempt? FindAttempt(string externalReference)
    {
        string normalizedExternalReference = externalReference?.Trim() ?? string.Empty;

        return _attempts.FirstOrDefault(
            attempt =>
                string.Equals(
                    attempt.ExternalReference,
                    normalizedExternalReference,
                    StringComparison.Ordinal));
    }
}
