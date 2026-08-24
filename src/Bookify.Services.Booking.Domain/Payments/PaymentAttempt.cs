using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Payments;

public sealed class PaymentAttempt
{
    public PaymentAttempt()
    {
        ExternalReference = string.Empty;
        Amount = null!;
    }

    private PaymentAttempt(
        Guid id,
        Guid paymentId,
        string externalReference,
        Money amount,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        PaymentId = paymentId;
        ExternalReference = externalReference;
        Amount = amount;
        Status = PaymentAttemptStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public string ExternalReference { get; private set; }
    public Money Amount { get; private set; }
    public PaymentAttemptStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    internal static Result<PaymentAttempt> Create(
        Guid paymentId,
        string externalReference,
        Money amount,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(amount);

        string normalizedExternalReference = externalReference?.Trim()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedExternalReference))
        {
            return Result<PaymentAttempt>.Failure(
                PaymentAttemptErrors.ExternalReferenceRequired);
        }

        if (amount.Amount <= 0)
        {
            return Result<PaymentAttempt>.Failure(
                PaymentAttemptErrors.AmountMustBePositive);
        }

        return Result<PaymentAttempt>.Success(
            new PaymentAttempt(
                Guid.NewGuid(),
                paymentId,
                normalizedExternalReference,
                amount,
                createdAtUtc));

    }

    internal Result MarkAsSucceeded(DateTimeOffset completedAtUtc)
    {
        return TransitionTo(
            PaymentAttemptStatus.Succeeded,
            completedAtUtc);
    }

    internal Result MarkAsFailed(DateTimeOffset completedAtUtc)
    {
        return TransitionTo(
            PaymentAttemptStatus.Failed, completedAtUtc);
    }

    internal Result Cancel(DateTimeOffset completedAtUtc)
    {
        return TransitionTo(
            PaymentAttemptStatus.Cancelled,
            completedAtUtc);
    }

    private Result TransitionTo(PaymentAttemptStatus targetStatus, DateTimeOffset completedAtUtc)
    {
        if (Status != PaymentAttemptStatus.Pending)
        {
            return Result.Failure(
                PaymentAttemptErrors.InvalidStatusTransition(Status, targetStatus));
        }

        if (completedAtUtc < CreatedAtUtc)
        {
            return Result.Failure(
                PaymentAttemptErrors.CompletionBeforeCreation);
        }

        Status = targetStatus;
        CompletedAtUtc = completedAtUtc;

        return Result.Success();
    }
}
