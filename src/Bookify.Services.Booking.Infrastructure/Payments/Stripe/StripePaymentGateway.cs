using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;

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

    public StripePaymentGateway(
        StripePaymentIntentService paymentIntentService)
    {
        _paymentIntentService =
            paymentIntentService ?? throw new ArgumentNullException(nameof(paymentIntentService));
    }

    public async Task<Result<PaymentGatewayResponse>> CreatePaymentAttemptAsync(
        CreatePaymentAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Amount);
        cancellationToken.ThrowIfCancellationRequested();

        string idempotencyKey = request.IdempotencyKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result<PaymentGatewayResponse>
                .Failure(
                    PaymentGatewayErrors
                        .IdempotencyKeyRequired);
        }

        Result<long> amountResult = StripeAmountConverter
            .ToMinorUnits(request.Amount);

        if (amountResult.IsFailure)
        {
            return Result<PaymentGatewayResponse>
                .Failure(amountResult.Error);
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
            StripePaymentIntent paymentIntent =
                await _paymentIntentService
                    .CreateAsync(
                        options,
                        requestOptions,
                        cancellationToken);

            return MapPaymentIntent(paymentIntent);
        }
        catch (OperationCanceledException) when (!cancellationToken
                .IsCancellationRequested)
        {
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors.ProviderTimeout);
        }
        catch (StripeException exception)
        {
            return Result<PaymentGatewayResponse>
                .Failure(MapStripeException(exception));
        }
    }

    public async Task<Result<PaymentGatewayResponse>> GetPaymentStatusAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedExternalReference = NormalizeExternalReference(externalReference);

        if (string.IsNullOrWhiteSpace(normalizedExternalReference))
        {
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors.ExternalReferenceRequired);
        }

        try
        {
            StripePaymentIntent paymentIntent = await _paymentIntentService
                .GetAsync(
                    normalizedExternalReference,
                    cancellationToken: cancellationToken);

            return MapPaymentIntent(paymentIntent);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors.ProviderTimeout);
        }
        catch (StripeException exception)
        {
            return Result<PaymentGatewayResponse>
                .Failure(MapStripeException(exception, normalizedExternalReference));
        }
    }

    public async Task<Result<PaymentGatewayResponse>> CancelPaymentAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedExternalReference = NormalizeExternalReference(externalReference);

        if (string.IsNullOrWhiteSpace(normalizedExternalReference))
        {
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors.ExternalReferenceRequired);
        }

        try
        {
            StripePaymentIntent currentPaymentIntent =
                await _paymentIntentService
                    .GetAsync(
                        normalizedExternalReference,
                        cancellationToken: cancellationToken);

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

            StripePaymentIntent cancelledPaymentIntent =
                await _paymentIntentService
                    .CancelAsync(
                        normalizedExternalReference,
                        cancellationToken: cancellationToken);

            return MapPaymentIntent(cancelledPaymentIntent);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors.ProviderTimeout);
        }
        catch (StripeException exception)
        {
            return Result<PaymentGatewayResponse>
                .Failure(
                    MapStripeException(exception, normalizedExternalReference));
        }
    }

    private static Result<PaymentGatewayResponse> MapPaymentIntent
        (StripePaymentIntent paymentIntent)
    {
        ArgumentNullException.ThrowIfNull(paymentIntent);

        if (string.IsNullOrWhiteSpace(paymentIntent.Id))
        {
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors.ProviderUnavailable);
        }

        PaymentGatewayStatus? status =
            paymentIntent.Status switch
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
            return Result<PaymentGatewayResponse>
                .Failure(PaymentGatewayErrors
                    .UnsupportedProviderStatus(paymentIntent.Status ?? string.Empty));
        }

        return Result<PaymentGatewayResponse>
            .Success(
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
