using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;
using System.Collections.Concurrent;

namespace Bookify.Services.Booking.Infrastructure.Payments.Fake;

public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, PaymentGatewayStatus> _payments = new(StringComparer.Ordinal);
    private readonly FakePaymentGatewayScenario _scenario;

    public FakePaymentGateway() : this(FakePaymentGatewayScenario.Success)
    {
    }

    public FakePaymentGateway(FakePaymentGatewayScenario scenario)
    {
        _scenario = scenario;
    }

    public Task<Result<PaymentGatewayResponse>> CreatePaymentAttemptAsync(
        CreatePaymentAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Amount);

        cancellationToken.ThrowIfCancellationRequested();

        return _scenario switch
        {
            FakePaymentGatewayScenario.Success => CreateSuccessfulAttemptAsync(),

            FakePaymentGatewayScenario.Failure =>
                Task.FromResult(
                    Result<PaymentGatewayResponse>.Failure(
                        PaymentGatewayErrors.ProviderRejected)),

            FakePaymentGatewayScenario.Timeout =>
                Task.FromResult(
                    Result<PaymentGatewayResponse>.Failure(
                            PaymentGatewayErrors.ProviderTimeout)),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported fake payment gateway scenario '{_scenario}'")
        };
    }

    public Task<Result<PaymentGatewayResponse>> GetPaymentStatusAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedExternalReference = NormalizeExternalReference(externalReference);

        if (string.IsNullOrWhiteSpace(normalizedExternalReference))
        {
            return Task.FromResult(
                Result<PaymentGatewayResponse>.Failure(
                    PaymentGatewayErrors.ExternalReferenceRequired));
        }

        if (!_payments.TryGetValue(
            normalizedExternalReference,
            out PaymentGatewayStatus status))
        {
            return Task.FromResult(
                Result<PaymentGatewayResponse>.Failure(
                    PaymentGatewayErrors
                        .ExternalReferenceNotFound(
                            normalizedExternalReference)));
        }

        return Task.FromResult(
            Result<PaymentGatewayResponse>.Success(
                new PaymentGatewayResponse(
                    normalizedExternalReference,
                    status)));
    }

    public Task<Result<PaymentGatewayResponse>> CancelPaymentAsync(
        string externalReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedExternalReference = NormalizeExternalReference(externalReference);

        if (string.IsNullOrWhiteSpace(normalizedExternalReference))
        {
            return Task.FromResult(
                Result<PaymentGatewayResponse>.Failure(
                    PaymentGatewayErrors
                        .ExternalReferenceRequired));
        }

        while (true)
        {
            if (!_payments.TryGetValue(
                normalizedExternalReference,
                out PaymentGatewayStatus currentStatus))
            {
                return Task.FromResult(
                    Result<PaymentGatewayResponse>.Failure(
                        PaymentGatewayErrors
                            .ExternalReferenceNotFound(
                                normalizedExternalReference)));
            }

            if (currentStatus == PaymentGatewayStatus.Cancelled)
            {
                return Task.FromResult(
                    Result<PaymentGatewayResponse>.Success(
                        new PaymentGatewayResponse(
                            normalizedExternalReference,
                            PaymentGatewayStatus.Cancelled)));
            }

            if (currentStatus != PaymentGatewayStatus.Pending)
            {
                return Task.FromResult(
                    Result<PaymentGatewayResponse>.Failure(
                        PaymentGatewayErrors.CannotCancel(
                            normalizedExternalReference,
                            currentStatus)));
            }

            bool updated = _payments.TryUpdate(
                normalizedExternalReference,
                PaymentGatewayStatus.Cancelled,
                PaymentGatewayStatus.Pending);

            if (updated)
            {
                return Task.FromResult(
                    Result<PaymentGatewayResponse>.Success(
                        new PaymentGatewayResponse(
                            normalizedExternalReference,
                            PaymentGatewayStatus.Cancelled)));
            }
        }
    }

    private Task<Result<PaymentGatewayResponse>> CreateSuccessfulAttemptAsync()
    {
        string externalReference = $"fake_{Guid.NewGuid():N}";

        bool added = _payments.TryAdd(
                        externalReference,
                        PaymentGatewayStatus.Pending);

        if (!added)
        {
            throw new InvalidOperationException(
                "Could not generate a unique fake payment reference.");
        }

        return Task.FromResult(
            Result<PaymentGatewayResponse>.Success(
                new PaymentGatewayResponse(
                    externalReference,
                    PaymentGatewayStatus.Pending)));
    }

    private static string NormalizeExternalReference(string externalReference)
    {
        return externalReference?.Trim() ?? string.Empty;
    }
}
