
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Stripe;
using System.Net;
using System.Net.Http;

namespace Bookify.Services.Booking.Integration.Tests.Payments.Stripe;

public sealed class StripeResilienceTests
{
    [Fact]
    public async Task Create_WhenTransientFailure_ShouldRetryWithSameKeyAndPayload()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.CreateFailures.Enqueue(
            CreateStripeException(
                HttpStatusCode.ServiceUnavailable,
                "api_error"));

        var gateway = CreateGateway(service);

        CreatePaymentAttemptRequest request =
            CreateRequest("booking-payment-operation");

        Result<CreatePaymentAttemptResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                request,
                token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, service.CreateInvocationCount);

        Assert.Equal(
            "pi_resilient",
            result.Value.ExternalReference);

        Assert.Equal(2, service.CreateKeys.Count);

        Assert.Equal(
            "booking-payment-operation",
            service.CreateKeys[0]);

        Assert.Equal(
            service.CreateKeys[0],
            service.CreateKeys[1]);

        // Both invocations used the same request payload.
        Assert.Same(
            service.CreateOptions[0],
            service.CreateOptions[1]);
    }

    [Fact]
    public async Task Create_WhenFunctionalError_ShouldNotRetry()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.CreateFailures.Enqueue(
            CreateStripeException(
                HttpStatusCode.PaymentRequired,
                "card_declined"));

        var gateway = CreateGateway(service);

        Result<CreatePaymentAttemptResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest("operation-functional"),
                token);

        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderRejected,
            result.Error);

        Assert.Equal(
            1,
            service.CreateInvocationCount);
    }

    [Fact]
    public async Task GetStatus_WhenTransientFailure_ShouldRetry()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.GetFailures.Enqueue(
            CreateStripeException(
                HttpStatusCode.Conflict,
                "lock_timeout"));

        var gateway = CreateGateway(service);

        Result<PaymentGatewayResponse> result =
            await gateway.GetPaymentStatusAsync(
                "pi_resilient",
                token);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            2,
            service.GetInvocationCount);

        Assert.Equal(
            PaymentGatewayStatus.Pending,
            result.Value.Status);
    }

    [Fact]
    public async Task GetStatus_WhenGeneralRateLimit_ShouldNotRetry()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.GetFailures.Enqueue(
            CreateStripeException(
                HttpStatusCode.TooManyRequests,
                "rate_limit"));

        var gateway = CreateGateway(service);

        Result<PaymentGatewayResponse> result =
            await gateway.GetPaymentStatusAsync(
                "pi_resilient",
                token);

        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderUnavailable,
            result.Error);

        Assert.Equal(
            1,
            service.GetInvocationCount);
    }

    [Fact]
    public async Task Cancel_WhenTransientMutationFailure_ShouldNotRetryCancel()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.CancelFailures.Enqueue(
            CreateStripeException(
                HttpStatusCode.ServiceUnavailable,
                "api_error"));

        var gateway = CreateGateway(service);

        Result<PaymentGatewayResponse> result =
            await gateway.CancelPaymentAsync(
                "pi_resilient",
                token);

        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderUnavailable,
            result.Error);

        Assert.Equal(
            1,
            service.GetInvocationCount);

        Assert.Equal(
            1,
            service.CancelInvocationCount);
    }

    [Fact]
    public async Task Create_WhenTransportFails_ShouldRetrySameOperation()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.CreateFailures.Enqueue(
            new HttpRequestException(
                "Simulated network failure"));

        var gateway = CreateGateway(service);

        Result<CreatePaymentAttemptResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest("operation-network"),
                token);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            2,
            service.CreateInvocationCount);

        Assert.Equal(
            service.CreateKeys[0],
            service.CreateKeys[1]);
    }


    [Fact]
    public async Task Create_WhenAllTransientAttemptsFail_ShouldReturnProviderUnavailable()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.CreateFailures.Enqueue(
            CreateStripeException(
                HttpStatusCode.ServiceUnavailable,
                "api_error"));

        service.CreateFailures.Enqueue(
            CreateStripeException(
                HttpStatusCode.ServiceUnavailable,
                "api_error"));

        StripePaymentGateway gateway =
            CreateGateway(service);

        // Act
        Result<CreatePaymentAttemptResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest("operation-exhausted"),
                cancellationToken);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderUnavailable,
            result.Error);

        // Initial call + one retry, never an unlimited loop.
        Assert.Equal(
            2,
            service.CreateInvocationCount);

        Assert.Equal(
            2,
            service.CreateKeys.Count);

        Assert.Equal(
            service.CreateKeys[0],
            service.CreateKeys[1]);

        Assert.Same(
            service.CreateOptions[0],
            service.CreateOptions[1]);
    }


    [Fact]
    public async Task Create_WhenProviderTimesOutTwice_ShouldReturnProviderTimeout()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.CreateFailures.Enqueue(
            new TaskCanceledException(
                "Simulated provider timeout."));

        service.CreateFailures.Enqueue(
            new TaskCanceledException(
                "Simulated provider timeout."));

        StripePaymentGateway gateway =
            CreateGateway(service);

        // Act
        Result<CreatePaymentAttemptResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest("operation-timeout"),
                cancellationToken);

        // Assert
        Assert.True(result.IsFailure);

        Assert.Equal(
            PaymentGatewayErrors.ProviderTimeout,
            result.Error);

        Assert.Equal(
            2,
            service.CreateInvocationCount);

        Assert.Equal(
            service.CreateKeys[0],
            service.CreateKeys[1]);

        Assert.Same(
            service.CreateOptions[0],
            service.CreateOptions[1]);
    }


    [Fact]
    public async Task Create_WhenCallerCancels_ShouldNotCallProviderOrRetry()
    {
        // Arrange
        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        var service = new StubPaymentIntentService();

        StripePaymentGateway gateway =
            CreateGateway(service);

        // Act
        Func<Task> action = async () =>
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest("operation-cancelled"),
                cancellationSource.Token);

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(action);

        Assert.Equal(
            0,
            service.CreateInvocationCount);
    }


    [Fact]
    public async Task Create_WhenTransientError_ShouldApplyMillisecondsBackoff()
    {
        CancellationToken token =
            TestContext.Current.CancellationToken;

        var service = new StubPaymentIntentService();

        service.CreateFailures.Enqueue(
            CreateStripeException(
                HttpStatusCode.ServiceUnavailable,
                "api_error"));

        var gateway = new StripePaymentGateway(
            service,
            maxTransientRetries: 1,
            retryBaseDelayMilliseconds: 250);

        var stopwatch =
            System.Diagnostics.Stopwatch.StartNew();

        Result<CreatePaymentAttemptResponse> result =
            await gateway.CreatePaymentAttemptAsync(
                CreateRequest("backoff-unit-test"),
                token);

        stopwatch.Stop();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, service.CreateInvocationCount);

        // Jitter is between 75% and 100%.
        // Use a lower threshold to tolerate scheduling variance.
        Assert.True(
            stopwatch.Elapsed >= TimeSpan.FromMilliseconds(150),
            $"Retry occurred too quickly: {stopwatch.Elapsed}.");
    }

    private static StripePaymentGateway CreateGateway(
        StubPaymentIntentService service)
    {
        // Small delay keeps tests fast without changing
        // the production configuration.
        return new StripePaymentGateway(
            service,
            maxTransientRetries: 1,
            retryBaseDelayMilliseconds: 1);
    }

    private static CreatePaymentAttemptRequest CreateRequest(
        string idempotencyKey)
    {
        Result<Money> moneyResult =
            Money.Create(12.34m, "USD");

        Assert.True(moneyResult.IsSuccess);

        return new CreatePaymentAttemptRequest(
            Guid.NewGuid(),
            moneyResult.Value,
            idempotencyKey);
    }

    private static StripeException CreateStripeException(
        HttpStatusCode statusCode,
        string errorCode)
    {
        return new StripeException(
            statusCode,
            new StripeError
            {
                Code = errorCode
            },
            "Simulated Stripe failure");
    }

    private sealed class StubPaymentIntentService :
        PaymentIntentService
    {
        public Queue<Exception> CreateFailures { get; } =
            new();

        public Queue<Exception> GetFailures { get; } =
            new();

        public Queue<Exception> CancelFailures { get; } =
            new();

        public List<string?> CreateKeys { get; } =
            new();

        public List<PaymentIntentCreateOptions> CreateOptions { get; } =
            new();

        public int CreateInvocationCount { get; private set; }

        public int GetInvocationCount { get; private set; }

        public int CancelInvocationCount { get; private set; }

        public override Task<PaymentIntent> CreateAsync(
            PaymentIntentCreateOptions options,
            RequestOptions requestOptions = null!,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CreateInvocationCount++;

            CreateKeys.Add(requestOptions?.IdempotencyKey);
            CreateOptions.Add(options);

            if (CreateFailures.TryDequeue(
                    out Exception? failure))
            {
                return Task.FromException<PaymentIntent>(
                    failure);
            }

            return Task.FromResult(
                new PaymentIntent
                {
                    Id = "pi_resilient",
                    Status = "requires_payment_method",
                    ClientSecret = "pi_resilient_secret"
                });
        }

        public override Task<PaymentIntent> GetAsync(
            string id,
            PaymentIntentGetOptions options = null!,
            RequestOptions requestOptions = null!,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GetInvocationCount++;

            if (GetFailures.TryDequeue(
                    out Exception? failure))
            {
                return Task.FromException<PaymentIntent>(
                    failure);
            }

            return Task.FromResult(
                new PaymentIntent
                {
                    Id = id,
                    Status = "requires_payment_method"
                });
        }

        public override Task<PaymentIntent> CancelAsync(
            string id,
            PaymentIntentCancelOptions options = null!,
            RequestOptions requestOptions = null!,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CancelInvocationCount++;

            if (CancelFailures.TryDequeue(
                    out Exception? failure))
            {
                return Task.FromException<PaymentIntent>(
                    failure);
            }

            return Task.FromResult(
                new PaymentIntent
                {
                    Id = id,
                    Status = "canceled"
                });
        }
    }
}
