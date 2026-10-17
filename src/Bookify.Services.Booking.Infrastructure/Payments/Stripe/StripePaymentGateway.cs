using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Infrastructure.Observability;
using Stripe;
using System.Diagnostics;
using StripeException = global::Stripe.StripeException;
using StripePaymentIntent = global::Stripe.PaymentIntent;
using StripePaymentIntentAutomaticPaymentMethodsOptions = global::Stripe.PaymentIntentAutomaticPaymentMethodsOptions;
using StripePaymentIntentCreateOptions = global::Stripe.PaymentIntentCreateOptions;
using StripePaymentIntentService = global::Stripe.PaymentIntentService;
using StripeRequestOptions = global::Stripe.RequestOptions;

namespace Bookify.Services.Booking.Infrastructure.Payments.Stripe;

public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly StripePaymentIntentService _paymentIntentService;
    private readonly int _maxTransientRetries;
    private readonly int _retryBaseDelayMilliseconds;

    public StripePaymentGateway(
        StripePaymentIntentService paymentIntentService,
        int maxTransientRetries = 1,
        int retryBaseDelayMilliseconds = 250)
    {
        _paymentIntentService = paymentIntentService ??
            throw new ArgumentNullException(nameof(paymentIntentService));

        if (maxTransientRetries is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTransientRetries));
        }

        if (retryBaseDelayMilliseconds is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(retryBaseDelayMilliseconds));
        }

        _maxTransientRetries = maxTransientRetries;
        _retryBaseDelayMilliseconds = retryBaseDelayMilliseconds;
    }

    public async Task<Result<CreatePaymentAttemptResponse>> CreatePaymentAttemptAsync(
        CreatePaymentAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Amount);
        cancellationToken.ThrowIfCancellationRequested();

        using Activity? activity = BookingActivitySource.Instance.StartActivity(
            "stripe.payment_intent.create",
            ActivityKind.Client);

        activity?.SetTag("peer.service", "stripe");
        activity?.SetTag("bookify.payment.operation", "create");

        string idempotencyKey = request.IdempotencyKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result<CreatePaymentAttemptResponse>
                .Failure(PaymentGatewayErrors.IdempotencyKeyRequired);
        }

        Result<long> amountResult = StripeAmountConverter.ToMinorUnits(request.Amount);

        if (amountResult.IsFailure)
        {
            return Result<CreatePaymentAttemptResponse>.Failure(amountResult.Error);
        }

        var options = new StripePaymentIntentCreateOptions
        {
            Amount = amountResult.Value,
            Currency = request.Amount.Currency.ToLowerInvariant(),
            AutomaticPaymentMethods = new StripePaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            },
            Metadata = new Dictionary<string, string>
            {
                ["bookify_booking_id"] = request.BookingId.ToString("D")
            }
        };

        var requestOptions = new StripeRequestOptions
        {
            IdempotencyKey = idempotencyKey
        };

        try
        {
            StripePaymentIntent paymentIntent = await ExecuteSafeRetriesAsync(token => _paymentIntentService.CreateAsync(
                options, requestOptions, token), activity, cancellationToken);

            return MapCreatedPaymentIntent(paymentIntent);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Provider timeout");

            return Result<CreatePaymentAttemptResponse>.Failure(PaymentGatewayErrors.ProviderTimeout);
        }
        catch (HttpRequestException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Provider unavailable");

            return Result<CreatePaymentAttemptResponse>.Failure(PaymentGatewayErrors.ProviderUnavailable);
        }
        catch (StripeException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Stripe request failed");

            return Result<CreatePaymentAttemptResponse>.Failure(MapStripeException(exception));
        }
    }

    public async Task<Result<PaymentGatewayResponse>> GetPaymentStatusAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using Activity? activity = BookingActivitySource.Instance.StartActivity(
            "stripe.payment_intent.get",
            ActivityKind.Client);

        activity?.SetTag("peer.service", "stripe");
        activity?.SetTag("bookify.payment.operation", "get");

        string normalizedExternalReference = NormalizeExternalReference(externalReference);

        if (string.IsNullOrWhiteSpace(normalizedExternalReference))
        {
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors.ExternalReferenceRequired);
        }

        try
        {
            StripePaymentIntent paymentIntent = await ExecuteSafeRetriesAsync(
                token => _paymentIntentService.GetAsync(
                    normalizedExternalReference,
                    cancellationToken: token),
                activity,
                cancellationToken);

            return MapPaymentIntent(paymentIntent);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Provider timeout");

            return Result<PaymentGatewayResponse>.Failure(PaymentGatewayErrors.ProviderTimeout);
        }
        catch (HttpRequestException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Provider unavailable");

            return Result<PaymentGatewayResponse>.Failure(PaymentGatewayErrors.ProviderUnavailable);
        }
        catch (StripeException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Stripe request failed");

            return Result<PaymentGatewayResponse>.Failure(MapStripeException(exception, normalizedExternalReference));
        }
    }

    public async Task<Result<PaymentGatewayResponse>> CancelPaymentAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using Activity? activity = BookingActivitySource.Instance.StartActivity(
            "stripe.payment_intent.cancel",
            ActivityKind.Client);

        activity?.SetTag("peer.service", "stripe");
        activity?.SetTag("bookify.payment.operation", "cancel");

        string normalizedExternalReference = NormalizeExternalReference(externalReference);

        if (string.IsNullOrWhiteSpace(normalizedExternalReference))
        {
            return Result<PaymentGatewayResponse>.Failure(PaymentGatewayErrors.ExternalReferenceRequired);
        }

        try
        {
            StripePaymentIntent currentPaymentIntent = await ExecuteSafeRetriesAsync(
                token => _paymentIntentService.GetAsync(normalizedExternalReference, cancellationToken: token),
                activity,
                cancellationToken);

            Result<PaymentGatewayResponse> currentResult = MapPaymentIntent(currentPaymentIntent);

            if (currentResult.IsFailure)
            {
                return currentResult;
            }

            PaymentGatewayStatus currentStatus = currentResult.Value.Status;

            if (currentStatus == PaymentGatewayStatus.Cancelled)
            {
                return currentResult;
            }

            if (currentStatus != PaymentGatewayStatus.Pending)
            {
                return Result<PaymentGatewayResponse>
                    .Failure(PaymentGatewayErrors.CannotCancel(normalizedExternalReference, currentStatus));
            }

            StripePaymentIntent cancelledPaymentIntent = await _paymentIntentService
                .CancelAsync(normalizedExternalReference, cancellationToken: cancellationToken);

            return MapPaymentIntent(cancelledPaymentIntent);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Provider timeout");

            return Result<PaymentGatewayResponse>.Failure(PaymentGatewayErrors.ProviderTimeout);
        }
        catch (HttpRequestException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Provider unavailable");

            return Result<PaymentGatewayResponse>.Failure(PaymentGatewayErrors.ProviderUnavailable);
        }
        catch (StripeException exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Stripe request failed");

            return Result<PaymentGatewayResponse>.Failure(MapStripeException(exception, normalizedExternalReference));
        }
    }

    private async Task<T> ExecuteSafeRetriesAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        int retries = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                T response = await operation(cancellationToken);

                activity?.SetTag("bookify.stripe.retry_count", retries);

                return response;
            }
            catch (StripeException exception)
                when (CanRetry(retries, cancellationToken) && IsTransientStripeException(exception))
            {
                // Explicitly classified transient provider error.
            }
            catch (HttpRequestException)
                when (CanRetry(retries, cancellationToken))
            {
                // Transient transport failure.
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested && CanRetry(retries, cancellationToken))
            {
                // Transport timeout, not caller cancellation.
            }

            retries++;

            activity?.SetTag("bookify.stripe.retry_count", retries);

            await Task.Delay(CalculateBackoff(retries), cancellationToken);
        }
    }

    private bool CanRetry(int completedRetries, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested && completedRetries < _maxTransientRetries;
    }


    private static bool IsTransientStripeException(StripeException exception)
    {
        int statusCode = (int)exception.HttpStatusCode;

        bool potentiallyTransient = statusCode is 0 or 408 or 409 or >= 500 and <= 599;

        if (statusCode == 429)
        {
            potentiallyTransient = string.Equals(exception.StripeError?.Code, "lock_timeout", StringComparison.Ordinal);
        }

        var headers = exception.StripeResponse?.Headers;

        if (headers is not null && headers.Contains("Stripe-Should-Retry"))
        {
            string? decision = headers.GetValues("Stripe-Should-Retry").FirstOrDefault();

            if (string.Equals(decision, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(decision, "true", StringComparison.OrdinalIgnoreCase))
            {
                return statusCode is 0 or 408 or 409 or 429 or >= 500 and <= 599;
            }
        }

        return potentiallyTransient;
    }


    private TimeSpan CalculateBackoff(int retryNumber)
    {
        double exponentialDelay = _retryBaseDelayMilliseconds * Math.Pow(2, retryNumber - 1);
        double boundedDelay = Math.Min(exponentialDelay, 2000);
        double jitter = 0.75 + Random.Shared.NextDouble() * 0.25;

        return TimeSpan.FromMilliseconds(Math.Max(1, boundedDelay * jitter));
    }

    private static Result<CreatePaymentAttemptResponse> MapCreatedPaymentIntent(StripePaymentIntent paymentIntent)
    {
        Result<PaymentGatewayResponse> paymentResult = MapPaymentIntent(paymentIntent);

        if (paymentResult.IsFailure)
        {
            return Result<CreatePaymentAttemptResponse>
                .Failure(paymentResult.Error);
        }

        if (string.IsNullOrWhiteSpace(paymentIntent.ClientSecret))
        {
            return Result<CreatePaymentAttemptResponse>
                .Failure(PaymentGatewayErrors.ClientSecretMissing);
        }

        PaymentGatewayResponse payment = paymentResult.Value;

        return Result<CreatePaymentAttemptResponse>
            .Success(
                new CreatePaymentAttemptResponse(
                    payment.ExternalReference,
                    payment.Status,
                    paymentIntent.ClientSecret));
    }

    private static Result<PaymentGatewayResponse> MapPaymentIntent(StripePaymentIntent paymentIntent)
    {
        ArgumentNullException.ThrowIfNull(paymentIntent);

        if (string.IsNullOrWhiteSpace(paymentIntent.Id))
        {
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors.ProviderUnavailable);
        }

        PaymentGatewayStatus? status = paymentIntent.Status switch
        {
            "requires_payment_method" => PaymentGatewayStatus.Pending,
            "requires_confirmation" => PaymentGatewayStatus.Pending,
            "requires_action" => PaymentGatewayStatus.Pending,
            "processing" => PaymentGatewayStatus.Pending,
            "requires_capture" => PaymentGatewayStatus.Pending,
            "succeeded" => PaymentGatewayStatus.Succeeded,
            "canceled" => PaymentGatewayStatus.Cancelled,
            _ => null
        };

        if (status is null)
        {
            return Result<PaymentGatewayResponse>.Failure(
                PaymentGatewayErrors.UnsupportedProviderStatus(paymentIntent.Status ?? string.Empty));
        }

        return Result<PaymentGatewayResponse>.Success(
                new PaymentGatewayResponse(
                    paymentIntent.Id,
                    status.Value));
    }

    private static Error MapStripeException(
        StripeException exception,
        string? externalReference = null)
    {
        int httpStatusCode = (int)exception.HttpStatusCode;

        if (httpStatusCode is 408 or 504)
        {
            return PaymentGatewayErrors.ProviderTimeout;
        }

        if (httpStatusCode == 404 && !string.IsNullOrWhiteSpace(externalReference))
        {
            return PaymentGatewayErrors.ExternalReferenceNotFound(externalReference);
        }

        if (httpStatusCode == 0 ||
            httpStatusCode == 429 ||
            httpStatusCode >= 500)
        {
            return PaymentGatewayErrors.ProviderUnavailable;
        }

        return PaymentGatewayErrors.ProviderRejected;
    }

    private static string NormalizeExternalReference(string externalReference)
    {
        return externalReference?.Trim() ?? string.Empty;
    }
}
