using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Payments.Errors;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Tests.Payments;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            2026,
            8,
            22,
            12,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_ShouldCreatePendingPayment()
    {
        // ARRANGE
        Guid bookingId = Guid.NewGuid();

        Money amount =
            CreateMoney(
                150m,
                "USD");

        // ACT
        Result<Payment> result =
            Payment.Create(
                bookingId,
                amount,
                CreatedAtUtc);

        // ASSERT
        Assert.True(result.IsSuccess);

        Payment payment =
            result.Value;

        Assert.NotEqual(
            Guid.Empty,
            payment.Id);

        Assert.Equal(
            bookingId,
            payment.BookingId);

        Assert.Same(
            amount,
            payment.Amount);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            CreatedAtUtc,
            payment.CreatedAtUtc);

        Assert.Equal(
            CreatedAtUtc,
            payment.UpdatedAtUtc);

        Assert.Null(
            payment.CompletedAtUtc);

        Assert.Empty(
            payment.Attempts);
    }

    [Fact]
    public void Create_WithEmptyBookingId_ShouldFail()
    {
        // ARRANGE
        Money amount =
            CreateMoney(
                150m,
                "USD");

        // ACT
        Result<Payment> result =
            Payment.Create(
                Guid.Empty,
                amount,
                CreatedAtUtc);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentErrors.BookingIdRequired,
            result.Error);
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldFail()
    {
        // ARRANGE
        Money amount =
            CreateMoney(
                0m,
                "USD");

        // ACT
        Result<Payment> result =
            Payment.Create(
                Guid.NewGuid(),
                amount,
                CreatedAtUtc);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentErrors.AmountMustBePositive,
            result.Error);
    }

    [Fact]
    public void AddAttempt_ShouldAddPendingAttempt()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        DateTimeOffset attemptCreatedAtUtc =
            CreatedAtUtc.AddMinutes(1);

        // ACT
        Result<PaymentAttempt> result =
            payment.AddAttempt(
                " external-001 ",
                attemptCreatedAtUtc);

        // ASSERT
        Assert.True(result.IsSuccess);

        PaymentAttempt attempt =
            result.Value;

        Assert.NotEqual(
            Guid.Empty,
            attempt.Id);

        Assert.Equal(
            payment.Id,
            attempt.PaymentId);

        Assert.Equal(
            "external-001",
            attempt.ExternalReference);

        Assert.Same(
            payment.Amount,
            attempt.Amount);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Equal(
            attemptCreatedAtUtc,
            attempt.CreatedAtUtc);

        Assert.Null(
            attempt.CompletedAtUtc);

        Assert.Single(
            payment.Attempts);
    }

    [Fact]
    public void AddAttempt_WhenPendingAttemptExists_ShouldFail()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> firstAttemptResult =
            payment.AddAttempt(
                "external-001",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            firstAttemptResult.IsSuccess);

        // ACT
        Result<PaymentAttempt> secondAttemptResult =
            payment.AddAttempt(
                "external-002",
                CreatedAtUtc.AddMinutes(2));

        // ASSERT
        Assert.True(
            secondAttemptResult.IsFailure);

        Assert.Equal(
            PaymentErrors.ActiveAttemptAlreadyExists,
            secondAttemptResult.Error);

        Assert.Single(
            payment.Attempts);
    }

    [Fact]
    public void AddAttempt_WithDuplicateExternalReference_ShouldFail()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> firstAttemptResult =
            payment.AddAttempt(
                "external-001",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            firstAttemptResult.IsSuccess);

        Result markFailedResult =
            payment.MarkAttemptAsFailed(
                "external-001",
                CreatedAtUtc.AddMinutes(2));

        Assert.True(
            markFailedResult.IsSuccess);

        // ACT
        Result<PaymentAttempt> duplicateResult =
            payment.AddAttempt(
                "external-001",
                CreatedAtUtc.AddMinutes(3));

        // ASSERT
        Assert.True(
            duplicateResult.IsFailure);

        Assert.Equal(
            PaymentErrors.DuplicateExternalReference(
                "external-001"),
            duplicateResult.Error);
    }

    [Fact]
    public void MarkAttemptAsFailed_ShouldFailPaymentAndAllowRetry()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> firstAttemptResult =
            payment.AddAttempt(
                "external-001",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            firstAttemptResult.IsSuccess);

        // ACT
        Result failedResult =
            payment.MarkAttemptAsFailed(
                "external-001",
                CreatedAtUtc.AddMinutes(2));

        Result<PaymentAttempt> retryResult =
            payment.AddAttempt(
                "external-002",
                CreatedAtUtc.AddMinutes(3));

        // ASSERT
        Assert.True(
            failedResult.IsSuccess);

        Assert.True(
            retryResult.IsSuccess);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            2,
            payment.Attempts.Count);

        PaymentAttempt firstAttempt =
            payment.Attempts.First();

        Assert.Equal(
            PaymentAttemptStatus.Failed,
            firstAttempt.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            retryResult.Value.Status);
    }

    [Fact]
    public void MarkAttemptAsSucceeded_ShouldCompletePayment()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                "external-001",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            attemptResult.IsSuccess);

        DateTimeOffset completedAtUtc =
            CreatedAtUtc.AddMinutes(2);

        // ACT
        Result result =
            payment.MarkAttemptAsSucceeded(
                "external-001",
                completedAtUtc);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            completedAtUtc,
            payment.CompletedAtUtc);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attemptResult.Value.Status);

        Assert.Equal(
            completedAtUtc,
            attemptResult.Value.CompletedAtUtc);
    }

    [Fact]
    public void AddAttempt_AfterSuccessfulPayment_ShouldFail()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                "external-001",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            attemptResult.IsSuccess);

        Result succeededResult =
            payment.MarkAttemptAsSucceeded(
                "external-001",
                CreatedAtUtc.AddMinutes(2));

        Assert.True(
            succeededResult.IsSuccess);

        // ACT
        Result<PaymentAttempt> result =
            payment.AddAttempt(
                "external-002",
                CreatedAtUtc.AddMinutes(3));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentErrors.CannotAddAttempt(
                PaymentStatus.Succeeded),
            result.Error);
    }

    [Fact]
    public void CancelAttempt_ShouldCancelPayment()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                "external-001",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            attemptResult.IsSuccess);

        DateTimeOffset cancelledAtUtc =
            CreatedAtUtc.AddMinutes(2);

        // ACT
        Result result =
            payment.CancelAttempt(
                "external-001",
                cancelledAtUtc);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            PaymentStatus.Cancelled,
            payment.Status);

        Assert.Equal(
            cancelledAtUtc,
            payment.CompletedAtUtc);

        Assert.Equal(
            PaymentAttemptStatus.Cancelled,
            attemptResult.Value.Status);
    }

    [Fact]
    public void MarkAttemptAsSucceeded_WhenAttemptDoesNotExist_ShouldFail()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        // ACT
        Result result =
            payment.MarkAttemptAsSucceeded(
                "missing-reference",
                CreatedAtUtc.AddMinutes(1));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentErrors.AttemptNotFound(
                "missing-reference"),
            result.Error);
    }

    [Fact]
    public void CompleteAttempt_BeforeAttemptCreation_ShouldFail()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        DateTimeOffset attemptCreatedAtUtc =
            CreatedAtUtc.AddMinutes(5);

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                "external-001",
                attemptCreatedAtUtc);

        Assert.True(
            attemptResult.IsSuccess);

        // ACT
        Result result =
            payment.MarkAttemptAsSucceeded(
                "external-001",
                CreatedAtUtc.AddMinutes(4));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentAttemptErrors.CompletionBeforeCreation,
            result.Error);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attemptResult.Value.Status);
    }

    private static Payment CreatePayment()
    {
        Result<Payment> result =
            Payment.Create(
                Guid.NewGuid(),
                CreateMoney(
                    150m,
                    "USD"),
                CreatedAtUtc);

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }

    private static Money CreateMoney(
        decimal amount,
        string currency)
    {
        Result<Money> result =
            Money.Create(
                amount,
                currency);

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }
}
