
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Security")]
public sealed class ForgedWebhookPersistenceSecurityTests
{
    private const string Endpoint =
        "/api/v1/payments/webhooks/stripe";

    private readonly BookingApiFactory _factory;

    public ForgedWebhookPersistenceSecurityTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("missing-signature")]
    [InlineData("wrong-secret")]
    [InlineData("altered-payload")]
    public async Task ForgedEvent_ShouldNotPoisonWebhookDeduplicationStore(
        string attack)
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string eventId =
            $"evt_security_{Guid.NewGuid():N}";

        // El tipo processing es conocido por Stripe,
        // pero no desencadena una conciliación terminal.
        // Es suficiente para comprobar la autenticidad
        // antes de acceder al almacenamiento de eventos.
        string originalPayload = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = "payment_intent.processing",
            data = new
            {
                @object = new
                {
                    id = $"pi_{Guid.NewGuid():N}"
                }
            }
        });

        string? signature;
        string requestPayload;

        switch (attack)
        {
            case "missing-signature":
                signature = null;
                requestPayload = originalPayload;
                break;

            case "wrong-secret":
                signature = CreateSignatureHeader(
                    originalPayload,
                    "whsec_untrusted_test_secret",
                    DateTimeOffset.UtcNow);

                requestPayload = originalPayload;
                break;

            case "altered-payload":
                // Firmamos el contenido original.
                signature = CreateSignatureHeader(
                    originalPayload,
                    BookingApiFactory.StripeWebhookSecret,
                    DateTimeOffset.UtcNow);

                // CAMBIO 22.7:
                // Modificamos el contenido DESPUÉS de firmarlo.
                requestPayload = originalPayload.Replace(
                    "payment_intent.processing",
                    "payment_intent.succeeded",
                    StringComparison.Ordinal);

                Assert.NotEqual(
                    originalPayload,
                    requestPayload);

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(attack));
        }

        // ACT 1 — Intento falsificado.
        using HttpResponseMessage forgedResponse =
            await PostWebhookAsync(
                requestPayload,
                signature,
                cancellationToken);

        // ASSERT — Rechazo de la falsificación.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            forgedResponse.StatusCode);

        ProblemDetailsResponse? problem =
            await forgedResponse.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(
                    cancellationToken);

        Assert.NotNull(problem);

        string expectedCode =
            attack == "missing-signature"
                ? "Payments.Webhook.SignatureRequired"
                : "Payments.Webhook.InvalidSignature";

        Assert.Equal(expectedCode, problem.Code);

        // ASSERT — No se almacenó el eventId falsificado.
        StoredPaymentWebhookEvent? beforeValidDelivery =
            await GetStoredEventAsync(
                eventId,
                cancellationToken);

        Assert.Null(beforeValidDelivery);

        // ACT 2 — El proveedor legítimo envía
        // el evento original con una firma válida.
        string legitimateSignature = CreateSignatureHeader(
            originalPayload,
            BookingApiFactory.StripeWebhookSecret,
            DateTimeOffset.UtcNow);

        using HttpResponseMessage validResponse =
            await PostWebhookAsync(
                originalPayload,
                legitimateSignature,
                cancellationToken);

        // ASSERT — El eventId sigue disponible.
        Assert.Equal(
            HttpStatusCode.OK,
            validResponse.StatusCode);

        StoredPaymentWebhookEvent? storedEvent =
            await GetStoredEventAsync(
                eventId,
                cancellationToken);

        Assert.NotNull(storedEvent);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            storedEvent.ProcessingStatus);

        Assert.NotNull(storedEvent.ProcessedAtUtc);
    }

    private async Task<StoredPaymentWebhookEvent?>
        GetStoredEventAsync(
            string eventId,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPaymentWebhookEventStore store =
            scope.ServiceProvider
                .GetRequiredService<IPaymentWebhookEventStore>();

        return await store.GetAsync(
            PaymentWebhookProviders.Stripe,
            eventId,
            cancellationToken);
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(
        string payload,
        string? signature,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Endpoint);

        request.Content = new StringContent(
            payload,
            Encoding.UTF8,
            "application/json");

        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation(
                "Stripe-Signature",
                signature);
        }

        return await _factory.Client.SendAsync(
            request,
            cancellationToken);
    }

    private static string CreateSignatureHeader(
        string payload,
        string secret,
        DateTimeOffset timestamp)
    {
        long seconds = timestamp.ToUnixTimeSeconds();

        string signedPayload =
            $"{seconds}.{payload}";

        byte[] secretBytes =
            Encoding.UTF8.GetBytes(secret);

        byte[] payloadBytes =
            Encoding.UTF8.GetBytes(signedPayload);

        using var hmac = new HMACSHA256(secretBytes);

        byte[] signature =
            hmac.ComputeHash(payloadBytes);

        return $"t={seconds},v1=" +
            Convert.ToHexString(signature).ToLowerInvariant();
    }
}
