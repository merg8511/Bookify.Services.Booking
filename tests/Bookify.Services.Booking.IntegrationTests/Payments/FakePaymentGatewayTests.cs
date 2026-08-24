using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Payments.Fake;

namespace Bookify.Services.Booking.IntegrationTests.Payments;

public sealed class FakePaymentGatewayTests
{
    [Fact]
    public async Task CreatePaymentAttemptAsync_WithSuccessScenario_ShouldCreatePendingPayment()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Success);

        CreatePaymentAttemptRequest request =
            CreateRequest();

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                request,
                cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.StartsWith(
            "fake_",
            result.Value.ExternalReference,
            StringComparison.Ordinal);

        Assert.Equal(
            PaymentGatewayStatus.Pending,
            result.Value.Status);
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithFailureScenario_ShouldReturnProviderRejected()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Failure);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest(), cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderRejected,
            result.Error);
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithTimeoutScenario_ShouldReturnProviderTimeout()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Timeout);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest(), cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderTimeout,
            result.Error);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_AfterSuccessfulCreation_ShouldReturnPending()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Success);

        Result<PaymentGatewayResponse> createResult =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest(), cancellationToken);

        Assert.True(
            createResult.IsSuccess);

        // ACT
        Result<PaymentGatewayResponse> statusResult =
            await gateway.GetPaymentStatusAsync(
                createResult.Value.ExternalReference, cancellationToken);

        // ASSERT
        Assert.True(
            statusResult.IsSuccess);

        Assert.Equal(
            createResult.Value.ExternalReference,
            statusResult.Value.ExternalReference);

        Assert.Equal(
            PaymentGatewayStatus.Pending,
            statusResult.Value.Status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WhenReferenceDoesNotExist_ShouldFail()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Success);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway.GetPaymentStatusAsync(
                "missing-reference", cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors
                .ExternalReferenceNotFound(
                    "missing-reference"),
            result.Error);
    }

    [Fact]
    public async Task CancelPaymentAsync_WhenPaymentIsPending_ShouldCancelPayment()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Success);

        Result<PaymentGatewayResponse> createResult =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest(), cancellationToken);

        Assert.True(
            createResult.IsSuccess);

        string externalReference =
            createResult.Value.ExternalReference;

        // ACT
        Result<PaymentGatewayResponse> cancelResult =
            await gateway.CancelPaymentAsync(
                externalReference, cancellationToken);

        // ASSERT
        Assert.True(
            cancelResult.IsSuccess);

        Assert.Equal(
            PaymentGatewayStatus.Cancelled,
            cancelResult.Value.Status);

        Result<PaymentGatewayResponse> statusResult =
            await gateway.GetPaymentStatusAsync(
                externalReference, cancellationToken);

        Assert.True(
            statusResult.IsSuccess);

        Assert.Equal(
            PaymentGatewayStatus.Cancelled,
            statusResult.Value.Status);
    }

    [Fact]
    public async Task CancelPaymentAsync_WhenCalledTwice_ShouldBeIdempotent()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Success);

        Result<PaymentGatewayResponse> createResult =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest(), cancellationToken);

        Assert.True(
            createResult.IsSuccess);

        string externalReference =
            createResult.Value.ExternalReference;

        Result<PaymentGatewayResponse> firstCancelResult =
            await gateway.CancelPaymentAsync(
                externalReference, cancellationToken);

        Assert.True(
            firstCancelResult.IsSuccess);

        // ACT
        Result<PaymentGatewayResponse> secondCancelResult =
            await gateway.CancelPaymentAsync(
                externalReference, cancellationToken);

        // ASSERT
        Assert.True(
            secondCancelResult.IsSuccess);

        Assert.Equal(
            PaymentGatewayStatus.Cancelled,
            secondCancelResult.Value.Status);
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_ShouldGenerateUniqueExternalReferences()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Success);

        // ACT
        Result<PaymentGatewayResponse> firstResult =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest(), cancellationToken);

        Result<PaymentGatewayResponse> secondResult =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest(), cancellationToken);

        // ASSERT
        Assert.True(
            firstResult.IsSuccess);

        Assert.True(
            secondResult.IsSuccess);

        Assert.NotEqual(
            firstResult.Value.ExternalReference,
            secondResult.Value.ExternalReference);
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithSameIdempotencyKey_ShouldReturnSameExternalReference()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var gateway =
            new FakePaymentGateway(
                FakePaymentGatewayScenario.Success);

        Result<Money> moneyResult =
            Money.Create(
                150m,
                "USD");

        Assert.True(
            moneyResult.IsSuccess);

        string idempotencyKey =
            Guid.NewGuid()
                .ToString("N");

        var request =
            new CreatePaymentAttemptRequest(
                Guid.NewGuid(),
                moneyResult.Value,
                idempotencyKey);

        // ACT
        Result<PaymentGatewayResponse>
            firstResult =
                await gateway
                    .CreatePaymentAttemptAsync(
                        request, cancellationToken);

        Result<PaymentGatewayResponse>
            secondResult =
                await gateway
                    .CreatePaymentAttemptAsync(
                        request, cancellationToken);

        // ASSERT
        Assert.True(
            firstResult.IsSuccess);

        Assert.True(
            secondResult.IsSuccess);

        Assert.Equal(
            firstResult.Value.ExternalReference,
            secondResult.Value.ExternalReference);
    }

    private static CreatePaymentAttemptRequest
        CreateRequest()
    {
        Result<Money> moneyResult =
            Money.Create(
                150m,
                "USD");

        Assert.True(
            moneyResult.IsSuccess);

        return new CreatePaymentAttemptRequest(
            Guid.NewGuid(),
            moneyResult.Value,
            Guid.NewGuid().ToString("N"));
    }
}
