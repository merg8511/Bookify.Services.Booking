
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Payments;

public sealed class PaymentTerminalInvariantTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MarkAttemptAsFailed_AfterSuccess_ShouldPreserveSucceededState()
    {
        // ARRANGE
        (Payment payment, PaymentAttempt attempt) =
            CreatePendingAttempt();

        DateTimeOffset succeededAt =
            CreatedAtUtc.AddMinutes(2);

        Assert.True(payment.MarkAttemptAsSucceeded(
            attempt.ExternalReference,
            succeededAt).IsSuccess);

        // ACT
        Result result = payment.MarkAttemptAsFailed(
            attempt.ExternalReference,
            succeededAt.AddMinutes(1));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentAttemptErrors.InvalidStatusTransition(
                PaymentAttemptStatus.Succeeded,
                PaymentAttemptStatus.Failed),
            result.Error);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(succeededAt, payment.CompletedAtUtc);
        Assert.Equal(succeededAt, payment.UpdatedAtUtc);
        Assert.Equal(succeededAt, attempt.CompletedAtUtc);
    }

    [Fact]
    public void CancelAttempt_AfterSuccess_ShouldPreserveSucceededState()
    {
        // ARRANGE
        (Payment payment, PaymentAttempt attempt) =
            CreatePendingAttempt();

        DateTimeOffset succeededAt =
            CreatedAtUtc.AddMinutes(2);

        Assert.True(payment.MarkAttemptAsSucceeded(
            attempt.ExternalReference,
            succeededAt).IsSuccess);

        // ACT
        Result result = payment.CancelAttempt(
            attempt.ExternalReference,
            succeededAt.AddMinutes(1));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentAttemptErrors.InvalidStatusTransition(
                PaymentAttemptStatus.Succeeded,
                PaymentAttemptStatus.Cancelled),
            result.Error);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(succeededAt, payment.CompletedAtUtc);
        Assert.Equal(succeededAt, payment.UpdatedAtUtc);
        Assert.Equal(succeededAt, attempt.CompletedAtUtc);
    }

    [Fact]
    public void CancelAttempt_AfterFailure_ShouldPreserveFailedState()
    {
        // ARRANGE
        (Payment payment, PaymentAttempt attempt) =
            CreatePendingAttempt();

        DateTimeOffset failedAt =
            CreatedAtUtc.AddMinutes(2);

        Assert.True(payment.MarkAttemptAsFailed(
            attempt.ExternalReference,
            failedAt).IsSuccess);

        // ACT
        Result result = payment.CancelAttempt(
            attempt.ExternalReference,
            failedAt.AddMinutes(1));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentAttemptErrors.InvalidStatusTransition(
                PaymentAttemptStatus.Failed,
                PaymentAttemptStatus.Cancelled),
            result.Error);

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Equal(failedAt, payment.UpdatedAtUtc);
        Assert.Equal(failedAt, attempt.CompletedAtUtc);
    }

    [Fact]
    public void MarkAttemptAsFailed_AfterCancellation_ShouldPreserveCancelledState()
    {
        // ARRANGE
        (Payment payment, PaymentAttempt attempt) =
            CreatePendingAttempt();

        DateTimeOffset cancelledAt =
            CreatedAtUtc.AddMinutes(2);

        Assert.True(payment.CancelAttempt(
            attempt.ExternalReference,
            cancelledAt).IsSuccess);

        // ACT
        Result result = payment.MarkAttemptAsFailed(
            attempt.ExternalReference,
            cancelledAt.AddMinutes(1));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentAttemptErrors.InvalidStatusTransition(
                PaymentAttemptStatus.Cancelled,
                PaymentAttemptStatus.Failed),
            result.Error);

        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Cancelled, attempt.Status);
        Assert.Equal(cancelledAt, payment.CompletedAtUtc);
        Assert.Equal(cancelledAt, payment.UpdatedAtUtc);
        Assert.Equal(cancelledAt, attempt.CompletedAtUtc);
    }

    [Fact]
    public void OldFailedAttempt_WhenRetryIsPending_ShouldNotChangeActiveAttempt()
    {
        // ARRANGE
        (Payment payment, PaymentAttempt oldAttempt) =
            CreatePendingAttempt();

        DateTimeOffset failedAt =
            CreatedAtUtc.AddMinutes(2);

        Assert.True(payment.MarkAttemptAsFailed(
            oldAttempt.ExternalReference,
            failedAt).IsSuccess);

        PaymentAttempt newAttempt = payment.AddAttempt(
            "operation-002",
            "pi_second",
            failedAt.AddMinutes(1)).Value;

        DateTimeOffset retryCreatedAt =
            newAttempt.CreatedAtUtc;

        // ACT
        Result result = payment.MarkAttemptAsFailed(
            oldAttempt.ExternalReference,
            retryCreatedAt.AddMinutes(1));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Failed, oldAttempt.Status);
        Assert.Equal(PaymentAttemptStatus.Pending, newAttempt.Status);

        Assert.Equal(failedAt, oldAttempt.CompletedAtUtc);
        Assert.Null(newAttempt.CompletedAtUtc);
        Assert.Null(payment.CompletedAtUtc);

        Assert.Equal(retryCreatedAt, payment.UpdatedAtUtc);
        Assert.Equal(2, payment.Attempts.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalTransition_BeforeAttemptCreation_ShouldNotMutate(
        bool cancelAttempt)
    {
        // ARRANGE
        (Payment payment, PaymentAttempt attempt) =
            CreatePendingAttempt();

        DateTimeOffset invalidCompletionTime =
            attempt.CreatedAtUtc.AddSeconds(-1);

        // ACT
        Result result = cancelAttempt
            ? payment.CancelAttempt(
                attempt.ExternalReference,
                invalidCompletionTime)
            : payment.MarkAttemptAsFailed(
                attempt.ExternalReference,
                invalidCompletionTime);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentAttemptErrors.CompletionBeforeCreation,
            result.Error);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        Assert.Null(attempt.CompletedAtUtc);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Equal(attempt.CreatedAtUtc, payment.UpdatedAtUtc);
    }

    [Fact]
    public void ReserveInitiation_WithPendingAttempt_ShouldRejectReservation()
    {
        // ARRANGE
        (Payment payment, PaymentAttempt attempt) =
            CreatePendingAttempt();

        // ACT
        Assert.Throws<InvalidOperationException>(
            () => payment.ReserveInitiation(
                "operation-002",
                CreatedAtUtc.AddMinutes(2)));

        // ASSERT
        Assert.Null(payment.PendingInitiationKey);
        Assert.Null(payment.PendingInitiationCreatedAtUtc);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        Assert.Single(payment.Attempts);
    }

    [Fact]
    public void ReserveInitiation_AfterSuccess_ShouldRejectReservation()
    {
        // ARRANGE
        (Payment payment, PaymentAttempt attempt) =
            CreatePendingAttempt();

        DateTimeOffset succeededAt =
            CreatedAtUtc.AddMinutes(2);

        Assert.True(payment.MarkAttemptAsSucceeded(
            attempt.ExternalReference,
            succeededAt).IsSuccess);

        // ACT
        Assert.Throws<InvalidOperationException>(
            () => payment.ReserveInitiation(
                "operation-002",
                succeededAt.AddMinutes(1)));

        // ASSERT
        Assert.Null(payment.PendingInitiationKey);
        Assert.Null(payment.PendingInitiationCreatedAtUtc);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(succeededAt, payment.CompletedAtUtc);
        Assert.Single(payment.Attempts);
    }

    [Fact]
    public void AddAttempt_BeforePaymentCreation_ShouldRejectWithoutMutation()
    {
        // ARRANGE
        Payment payment = CreatePayment();

        // ACT
        Result<PaymentAttempt> result = payment.AddAttempt(
            "operation-001",
            "pi_before_creation",
            CreatedAtUtc.AddSeconds(-1));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentErrors.AttemptBeforePaymentCreation,
            result.Error);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(CreatedAtUtc, payment.UpdatedAtUtc);
        Assert.Null(payment.CompletedAtUtc);
        Assert.Empty(payment.Attempts);
    }


    [Fact]
    public void MarkAttemptAsSucceeded_WhenRepeated_ShouldPreserveOriginalCompletionTime()
    {
        // ARRANGE
        (Payment payment, PaymentAttempt attempt) =
            CreatePendingAttempt();

        DateTimeOffset firstSuccessAt =
            CreatedAtUtc.AddMinutes(2);

        Assert.True(payment.MarkAttemptAsSucceeded(
            attempt.ExternalReference,
            firstSuccessAt).IsSuccess);

        // ACT
        Result repeatedResult = payment.MarkAttemptAsSucceeded(
            attempt.ExternalReference,
            firstSuccessAt.AddMinutes(5));

        // ASSERT
        Assert.True(repeatedResult.IsSuccess);

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);

        Assert.Equal(firstSuccessAt, attempt.CompletedAtUtc);
        Assert.Equal(firstSuccessAt, payment.CompletedAtUtc);
        Assert.Equal(firstSuccessAt, payment.UpdatedAtUtc);

        Assert.Single(payment.Attempts);
    }


    private static (Payment Payment, PaymentAttempt Attempt)
        CreatePendingAttempt()
    {
        Payment payment = CreatePayment();

        Result<PaymentAttempt> result = payment.AddAttempt(
            "operation-001",
            "pi_first",
            CreatedAtUtc.AddMinutes(1));

        Assert.True(result.IsSuccess);

        return (payment, result.Value);
    }

    private static Payment CreatePayment()
    {
        Money amount = Money.Create(150m, "USD").Value;

        Result<Payment> result = Payment.Create(
            Guid.NewGuid(),
            amount,
            CreatedAtUtc);

        Assert.True(result.IsSuccess);

        return result.Value;
    }
}
