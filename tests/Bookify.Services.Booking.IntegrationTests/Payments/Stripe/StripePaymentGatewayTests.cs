using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Stripe;

namespace Bookify.Services.Booking.IntegrationTests.Payments.Stripe;

public sealed class StripePaymentGatewayTests
{
    [Fact]
    public async Task CreatePaymentAttemptAsync_WithUsd_ShouldCreatePaymentIntentUsingMinorUnits()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                CreateResult =
                    new PaymentIntent
                    {
                        Id = "pi_test",
                        Status =
                            "requires_payment_method"
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        Guid bookingId =
            Guid.NewGuid();

        CreatePaymentAttemptRequest request =
            CreateRequest(
                bookingId,
                12.34m,
                "USD",
                "payment-operation-001");

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .CreatePaymentAttemptAsync(
                    request, cancellationToken);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            "pi_test",
            result.Value.ExternalReference);

        Assert.Equal(
            PaymentGatewayStatus.Pending,
            result.Value.Status);

        Assert.NotNull(
            service.LastCreateOptions);

        Assert.Equal(
            1234L,
            service.LastCreateOptions!
                .Amount.GetValueOrDefault());

        Assert.Equal(
            "usd",
            service.LastCreateOptions.Currency);

        Assert.Equal(
            bookingId.ToString("D"),
            service.LastCreateOptions
                .Metadata[
                    "bookify_booking_id"]);

        Assert.NotNull(
            service.LastCreateRequestOptions);

        Assert.Equal(
            "payment-operation-001",
            service.LastCreateRequestOptions!
                .IdempotencyKey);
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithZeroDecimalCurrency_ShouldNotMultiplyAmount()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                CreateResult =
                    new PaymentIntent
                    {
                        Id = "pi_jpy",
                        Status =
                            "requires_payment_method"
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        CreatePaymentAttemptRequest request =
            CreateRequest(
                Guid.NewGuid(),
                500m,
                "JPY",
                "payment-operation-jpy");

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .CreatePaymentAttemptAsync(
                    request, cancellationToken);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            500L,
            service.LastCreateOptions!
                .Amount.GetValueOrDefault());
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithInvalidCurrencyPrecision_ShouldFailBeforeCallingStripe()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService();

        var gateway =
            new StripePaymentGateway(
                service);

        CreatePaymentAttemptRequest request =
            CreateRequest(
                Guid.NewGuid(),
                500.50m,
                "JPY",
                "payment-operation-invalid");

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .CreatePaymentAttemptAsync(
                    request, cancellationToken);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors
                .InvalidAmountPrecision(
                    "JPY"),
            result.Error);

        Assert.Equal(
            0,
            service.CreateInvocationCount);
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithoutIdempotencyKey_ShouldFailBeforeCallingStripe()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService();

        var gateway =
            new StripePaymentGateway(
                service);

        CreatePaymentAttemptRequest request =
            CreateRequest(
                Guid.NewGuid(),
                100m,
                "USD",
                string.Empty);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .CreatePaymentAttemptAsync(
                    request, cancellationToken);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors
                .IdempotencyKeyRequired,
            result.Error);

        Assert.Equal(
            0,
            service.CreateInvocationCount);
    }

    [Theory]
    [InlineData(
        "requires_payment_method",
        PaymentGatewayStatus.Pending)]
    [InlineData(
        "requires_confirmation",
        PaymentGatewayStatus.Pending)]
    [InlineData(
        "requires_action",
        PaymentGatewayStatus.Pending)]
    [InlineData(
        "processing",
        PaymentGatewayStatus.Pending)]
    [InlineData(
        "requires_capture",
        PaymentGatewayStatus.Pending)]
    [InlineData(
        "succeeded",
        PaymentGatewayStatus.Succeeded)]
    [InlineData(
        "canceled",
        PaymentGatewayStatus.Cancelled)]
    public async Task GetPaymentStatusAsync_ShouldMapStripeStatus(
        string stripeStatus,
        PaymentGatewayStatus expectedStatus)
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                GetResult =
                    new PaymentIntent
                    {
                        Id = "pi_status",
                        Status =
                            stripeStatus
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .GetPaymentStatusAsync(
                    "pi_status", cancellationToken);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            expectedStatus,
            result.Value.Status);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_WithUnsupportedStatus_ShouldFail()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                GetResult =
                    new PaymentIntent
                    {
                        Id = "pi_unknown",
                        Status =
                            "future_status"
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .GetPaymentStatusAsync(
                    "pi_unknown", cancellationToken);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors
                .UnsupportedProviderStatus(
                    "future_status"),
            result.Error);
    }

    [Fact]
    public async Task CancelPaymentAsync_WhenAlreadyCancelled_ShouldBeIdempotent()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                GetResult =
                    new PaymentIntent
                    {
                        Id = "pi_cancelled",
                        Status = "canceled"
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .CancelPaymentAsync(
                    "pi_cancelled", cancellationToken);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentGatewayStatus.Cancelled,
            result.Value.Status);

        Assert.Equal(
            0,
            service.CancelInvocationCount);
    }

    [Fact]
    public async Task CancelPaymentAsync_WhenPending_ShouldCancelStripePaymentIntent()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                GetResult =
                    new PaymentIntent
                    {
                        Id = "pi_pending",
                        Status =
                            "requires_payment_method"
                    },

                CancelResult =
                    new PaymentIntent
                    {
                        Id = "pi_pending",
                        Status = "canceled"
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .CancelPaymentAsync(
                    "pi_pending", cancellationToken);

        // ASSERT
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentGatewayStatus.Cancelled,
            result.Value.Status);

        Assert.Equal(
            1,
            service.CancelInvocationCount);
    }

    [Fact]
    public async Task CancelPaymentAsync_WhenSucceeded_ShouldFailWithoutCallingCancel()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                GetResult =
                    new PaymentIntent
                    {
                        Id = "pi_succeeded",
                        Status = "succeeded"
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        // ACT
        Result<PaymentGatewayResponse> result =
            await gateway
                .CancelPaymentAsync(
                    "pi_succeeded", cancellationToken);

        // ASSERT
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.CannotCancel(
                "pi_succeeded",
                PaymentGatewayStatus.Succeeded),
            result.Error);

        Assert.Equal(
            0,
            service.CancelInvocationCount);
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithThreeDecimalCurrency_ShouldUseThousandths()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                CreateResult =
                    new PaymentIntent
                    {
                        Id = "pi_kwd",
                        Status =
                            "requires_payment_method"
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        CreatePaymentAttemptRequest request =
            CreateRequest(
                Guid.NewGuid(),
                12.345m,
                "KWD",
                "payment-operation-kwd");

        // Act
        Result<PaymentGatewayResponse> result =
            await gateway
                .CreatePaymentAttemptAsync(
                    request,
                    cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            12345L,
            service.LastCreateOptions!
                .Amount.GetValueOrDefault());
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithUgx_ShouldUseTwoDecimalMinorUnitRepresentation()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService
            {
                CreateResult =
                    new PaymentIntent
                    {
                        Id = "pi_ugx",
                        Status =
                            "requires_payment_method"
                    }
            };

        var gateway =
            new StripePaymentGateway(
                service);

        CreatePaymentAttemptRequest request =
            CreateRequest(
                Guid.NewGuid(),
                5m,
                "UGX",
                "payment-operation-ugx");

        // Act
        Result<PaymentGatewayResponse> result =
            await gateway
                .CreatePaymentAttemptAsync(
                    request,
                    cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            500L,
            service.LastCreateOptions!
                .Amount.GetValueOrDefault());
    }

    [Fact]
    public async Task CreatePaymentAttemptAsync_WithFractionalUgx_ShouldFailBeforeCallingStripe()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var service =
            new StubPaymentIntentService();

        var gateway =
            new StripePaymentGateway(
                service);

        CreatePaymentAttemptRequest request =
            CreateRequest(
                Guid.NewGuid(),
                5.5m,
                "UGX",
                "payment-operation-ugx-invalid");

        // Act
        Result<PaymentGatewayResponse> result =
            await gateway
                .CreatePaymentAttemptAsync(
                    request,
                    cancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors
                .InvalidAmountPrecision(
                    "UGX"),
            result.Error);

        Assert.Equal(
            0,
            service.CreateInvocationCount);
    }

    private static CreatePaymentAttemptRequest
        CreateRequest(
            Guid bookingId,
            decimal amount,
            string currency,
            string idempotencyKey)
    {
        Result<Money> moneyResult =
            Money.Create(
                amount,
                currency);

        Assert.True(
            moneyResult.IsSuccess);

        return new CreatePaymentAttemptRequest(
            bookingId,
            moneyResult.Value,
            idempotencyKey);
    }

    private sealed class StubPaymentIntentService
        : PaymentIntentService
    {
        public PaymentIntent CreateResult { get; set; } =
            new()
            {
                Id = "pi_create",
                Status = "requires_payment_method"
            };

        public PaymentIntent GetResult { get; set; } =
            new()
            {
                Id = "pi_get",
                Status = "requires_payment_method"
            };

        public PaymentIntent CancelResult { get; set; } =
            new()
            {
                Id = "pi_cancel",
                Status = "canceled"
            };

        public PaymentIntentCreateOptions?
            LastCreateOptions
        { get; private set; }

        public RequestOptions?
            LastCreateRequestOptions
        { get; private set; }

        public int CreateInvocationCount { get; private set; }

        public int CancelInvocationCount { get; private set; }

        public override Task<PaymentIntent>
            CreateAsync(
                PaymentIntentCreateOptions options,
                RequestOptions requestOptions = null!,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CreateInvocationCount++;

            LastCreateOptions =
                options;

            LastCreateRequestOptions =
                requestOptions;

            return Task.FromResult(
                CreateResult);
        }

        public override Task<PaymentIntent>
            GetAsync(
                string id,
                PaymentIntentGetOptions options = null!,
                RequestOptions requestOptions = null!,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                GetResult);
        }

        public override Task<PaymentIntent>
            CancelAsync(
                string id,
                PaymentIntentCancelOptions options = null!,
                RequestOptions requestOptions = null!,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CancelInvocationCount++;

            return Task.FromResult(
                CancelResult);
        }
    }
}
