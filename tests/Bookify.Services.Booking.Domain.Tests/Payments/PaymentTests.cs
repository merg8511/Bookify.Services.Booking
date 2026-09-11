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
                "payment-operation-001",
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

        Assert.NotSame(
            payment.Amount,
            attempt.Amount);

        Assert.Equal(
            payment.Amount.Amount,
            attempt.Amount.Amount);

        Assert.Equal(
            payment.Amount.Currency,
            attempt.Amount.Currency);

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
                "payment-operation-001",
                "external-001",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            firstAttemptResult.IsSuccess);

        // ACT
        Result<PaymentAttempt> secondAttemptResult =
            payment.AddAttempt(
                "payment-operation-002",
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
                "payment-operation-001",
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
                "payment-operation-002",
                "external-001",
                CreatedAtUtc.AddMinutes(3));

        // ASSERT
        Assert.True(
            duplicateResult.IsFailure);

        Assert.Null(payment.CompletedAtUtc);

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
                "payment-operation-001",
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
                "payment-operation-002",
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

        Assert.Null(
            payment.CompletedAtUtc);
    }

    [Fact]
    public void MarkAttemptAsSucceeded_ShouldCompletePayment()
    {
        // ARRANGE
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                "payment-operation-001",
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
                "payment-operation-001",
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
                "payment-operation-002",
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
                "payment-operation-001",
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
                "payment-operation-001",
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

    [Fact]
    public void AddAttempt_WithDuplicateIdempotencyKey_ShouldFail()
    {
        // ARRANGE
        Payment payment = CreatePayment();

        Result<PaymentAttempt> firstAttempt =
            payment.AddAttempt(
                "operation-001",
                "external-001",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(firstAttempt.IsSuccess);

        Result failedResult =
            payment.MarkAttemptAsFailed(
                "external-001",
                CreatedAtUtc.AddMinutes(2));

        Assert.True(failedResult.IsSuccess);

        // ACT
        Result<PaymentAttempt> result =
            payment.AddAttempt(
                "operation-001",
                "external-002",
                CreatedAtUtc.AddMinutes(3));

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentErrors.DuplicateIdempotencyKey(
                "operation-001"),
            result.Error);
    }

    [Fact]
    public void AddAttempt_WhenPaymentFailed_ShouldCreateNewPendingAttempt()
    {
        // Arrange
        Money amount = CreateMoney(100m, "USD");

        DateTimeOffset createdAtUtc =
            new(
                2026,
                9,
                3,
                12,
                0,
                0,
                TimeSpan.Zero);

        Payment payment = CreatePayment();

        PaymentAttempt firstAttempt =
            payment.AddAttempt(
                "operation-1",
                "external-1",
                createdAtUtc).Value;

        DateTimeOffset failedAtUtc =
            createdAtUtc.AddMinutes(
                1);

        Result failedResult =
            payment.MarkAttemptAsFailed(
                firstAttempt.ExternalReference,
                failedAtUtc);

        Assert.True(
            failedResult.IsSuccess);

        Assert.Equal(
            PaymentStatus.Failed,
            payment.Status);

        // Act
        DateTimeOffset retryAtUtc =
            failedAtUtc.AddMinutes(
                1);

        Result<PaymentAttempt> result =
            payment.AddAttempt(
                "operation-2",
                "external-2",
                retryAtUtc);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            2,
            payment.Attempts.Count);

        Assert.Equal(
            PaymentAttemptStatus.Failed,
            firstAttempt.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            result.Value.Status);

        Assert.Null(
            payment.CompletedAtUtc);
    }

    [Fact]
    public void AddAttempt_WhenPaymentCancelled_ShouldCreateNewPendingAttempt()
    {
        // Arrange
        Money amount = CreateMoney(100m, "USD");

        DateTimeOffset createdAtUtc =
            new(
                2026,
                9,
                3,
                12,
                0,
                0,
                TimeSpan.Zero);

        Payment payment = CreatePayment();

        PaymentAttempt firstAttempt =
            payment.AddAttempt(
                "operation-1",
                "external-1",
                createdAtUtc).Value;

        DateTimeOffset cancelledAtUtc =
            createdAtUtc.AddMinutes(
                1);

        Result cancelledResult =
            payment.CancelAttempt(
                firstAttempt.ExternalReference,
                cancelledAtUtc);

        Assert.True(
            cancelledResult.IsSuccess);

        Assert.Equal(
            PaymentStatus.Cancelled,
            payment.Status);

        // Act
        DateTimeOffset retryAtUtc =
            cancelledAtUtc.AddMinutes(
                1);

        Result<PaymentAttempt> result =
            payment.AddAttempt(
                "operation-2",
                "external-2",
                retryAtUtc);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Equal(
            2,
            payment.Attempts.Count);

        Assert.Equal(
            PaymentAttemptStatus.Cancelled,
            firstAttempt.Status);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            result.Value.Status);

        Assert.Null(
            payment.CompletedAtUtc);
    }

    [Fact]
    public void MarkAttemptAsSucceeded_WhenAttemptPreviouslyFailed_ShouldPromotePaymentToSucceeded()
    {
        // Arrange
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                "payment-operation-late-success-failed",
                "external-late-success-failed",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            attemptResult.IsSuccess);

        Result failedResult =
            payment.MarkAttemptAsFailed(
                attemptResult.Value.ExternalReference,
                CreatedAtUtc.AddMinutes(2));

        Assert.True(
            failedResult.IsSuccess);

        Assert.Equal(
            PaymentStatus.Failed,
            payment.Status);

        // Act
        DateTimeOffset succeededAtUtc =
            CreatedAtUtc.AddMinutes(3);

        Result result =
            payment.MarkAttemptAsSucceeded(
                attemptResult.Value.ExternalReference,
                succeededAtUtc);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attemptResult.Value.Status);

        Assert.Equal(
            succeededAtUtc,
            payment.CompletedAtUtc);

        Assert.Equal(
            succeededAtUtc,
            attemptResult.Value.CompletedAtUtc);
    }

    [Fact]
    public void MarkAttemptAsSucceeded_WhenAttemptPreviouslyCancelled_ShouldPromotePaymentToSucceeded()
    {
        // Arrange
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                "payment-operation-late-success-cancelled",
                "external-late-success-cancelled",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            attemptResult.IsSuccess);

        Result cancelledResult =
            payment.CancelAttempt(
                attemptResult.Value.ExternalReference,
                CreatedAtUtc.AddMinutes(2));

        Assert.True(
            cancelledResult.IsSuccess);

        Assert.Equal(
            PaymentStatus.Cancelled,
            payment.Status);

        // Act
        DateTimeOffset succeededAtUtc =
            CreatedAtUtc.AddMinutes(3);

        Result result =
            payment.MarkAttemptAsSucceeded(
                attemptResult.Value.ExternalReference,
                succeededAtUtc);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attemptResult.Value.Status);

        Assert.Equal(
            succeededAtUtc,
            payment.CompletedAtUtc);

        Assert.Equal(
            succeededAtUtc,
            attemptResult.Value.CompletedAtUtc);
    }

    [Fact]
    public void AddAttempt_AfterFailedAttempt_ShouldCreateIndependentAmountSnapshots()
    {
        // Arrange
        Payment payment =
            CreatePayment();

        Result<PaymentAttempt> firstAttemptResult =
            payment.AddAttempt(
                "payment-operation-first",
                "external-first",
                CreatedAtUtc.AddMinutes(1));

        Assert.True(
            firstAttemptResult.IsSuccess);

        Result failedResult =
            payment.MarkAttemptAsFailed(
                firstAttemptResult.Value.ExternalReference,
                CreatedAtUtc.AddMinutes(2));

        Assert.True(
            failedResult.IsSuccess);

        // Act
        Result<PaymentAttempt> secondAttemptResult =
            payment.AddAttempt(
                "payment-operation-second",
                "external-second",
                CreatedAtUtc.AddMinutes(3));

        // Assert
        Assert.True(
            secondAttemptResult.IsSuccess);

        PaymentAttempt firstAttempt =
            firstAttemptResult.Value;

        PaymentAttempt secondAttempt =
            secondAttemptResult.Value;

        Assert.NotSame(
            payment.Amount,
            firstAttempt.Amount);

        Assert.NotSame(
            payment.Amount,
            secondAttempt.Amount);

        Assert.NotSame(
            firstAttempt.Amount,
            secondAttempt.Amount);

        Assert.Equal(
            payment.Amount.Amount,
            firstAttempt.Amount.Amount);

        Assert.Equal(
            payment.Amount.Amount,
            secondAttempt.Amount.Amount);

        Assert.Equal(
            payment.Amount.Currency,
            firstAttempt.Amount.Currency);

        Assert.Equal(
            payment.Amount.Currency,
            secondAttempt.Amount.Currency);
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
