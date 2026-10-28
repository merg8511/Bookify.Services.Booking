using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Concurrency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Concurrency")]
public sealed class WebhookVsPaymentExpirationConcurrencyTests
{
    private const string WebhookEndpoint =
        "/api/v1/payments/webhooks/stripe";

    private readonly BookingApiFactory _factory;

    public WebhookVsPaymentExpirationConcurrencyTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentWebhookAndExpiration_ShouldPreserveConsistentTerminalState()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Este seeder crea una Booking PendingPayment con
        // PaymentDueAtUtc vencido respecto al reloj de prueba
        // actual utilizado en este escenario.
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        // Crear el Payment mediante el caso de uso real,
        // para que FakePaymentGateway conozca su referencia.
        InitiatePaymentResponse initiation =
            await InitiatePaymentAsync(
                booking.Id,
                cancellationToken);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            initiation.Status);

        string payload = CreateSucceededPayload(
            booking.Id,
            initiation.ExternalReference);

        var startGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Task<Result> expirationTask =
            ExpireAfterGateAsync(
                booking.Id,
                startGate.Task,
                cancellationToken);

        Task<HttpResponseMessage> webhookTask =
            SendWebhookAfterGateAsync(
                payload,
                startGate.Task,
                cancellationToken);

        // ACT — ambas operaciones compiten por la Booking.
        startGate.SetResult(true);

        Result expirationResult =
            await expirationTask;

        using HttpResponseMessage webhookResponse =
            await webhookTask;

        // ASSERT — la expiración debe manejar la carrera.
        Assert.True(
            expirationResult.IsSuccess,
            expirationResult.IsFailure
                ? expirationResult.Error.Code
                : string.Empty);

        Assert.True(
            webhookResponse.StatusCode is
                HttpStatusCode.OK or
                HttpStatusCode.Conflict,
            $"Unexpected webhook status: {webhookResponse.StatusCode}");

        // ASSERT — estado autoritativo PostgreSQL.
        using IServiceScope verificationScope =
            _factory.Services.CreateScope();

        IBookingRepository bookingRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        IPaymentRepository paymentRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<IPaymentRepository>();

        DomainBooking? storedBooking =
            await bookingRepository.GetByIdAsync(
                booking.Id,
                cancellationToken);

        Payment? storedPayment =
            await paymentRepository.GetByBookingIdAsync(
                booking.Id,
                cancellationToken);

        Assert.NotNull(storedBooking);
        Assert.NotNull(storedPayment);

        PaymentAttempt storedAttempt =
            Assert.Single(storedPayment.Attempts);

        Assert.Equal(
            initiation.PaymentId,
            storedPayment.Id);

        Assert.Equal(
            initiation.PaymentAttemptId,
            storedAttempt.Id);

        // Dos finales permitidos por la coordinación actual.
        switch (storedBooking.Status)
        {
            case BookingStatus.Paid:
                {
                    // El webhook ganó la carrera.
                    Assert.Equal(
                        HttpStatusCode.OK,
                        webhookResponse.StatusCode);

                    Assert.Equal(
                        PaymentStatus.Succeeded,
                        storedPayment.Status);

                    Assert.Equal(
                        PaymentAttemptStatus.Succeeded,
                        storedAttempt.Status);

                    Assert.NotNull(storedBooking.PaidAtUtc);
                    Assert.NotNull(storedPayment.CompletedAtUtc);

                    Assert.Null(storedBooking.CancellationReason);

                    break;
                }

            case BookingStatus.Cancelled:
                {
                    // La expiración ganó y confirmó la cancelación.
                    // El webhook tardío no puede pagar una reserva
                    // que ya fue cancelada.
                    Assert.Equal(
                        HttpStatusCode.Conflict,
                        webhookResponse.StatusCode);

                    Assert.Equal(
                        BookingCancellationReason.PaymentExpired,
                        storedBooking.CancellationReason);

                    Assert.Equal(
                        PaymentStatus.Cancelled,
                        storedPayment.Status);

                    Assert.Equal(
                        PaymentAttemptStatus.Cancelled,
                        storedAttempt.Status);

                    Assert.Null(storedBooking.PaidAtUtc);

                    break;
                }

            default:
                Assert.Fail(
                    "The race left the booking in an invalid state: " +
                    storedBooking.Status);

                break;
        }
    }

    private async Task<InitiatePaymentResponse> InitiatePaymentAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var executor = scope.ServiceProvider
            .GetRequiredService<
                ICommandExecutor<
                    InitiatePaymentCommand,
                    InitiatePaymentResponse>>();

        Result<InitiatePaymentResponse> result =
            await executor.ExecuteAsync(
                new InitiatePaymentCommand(
                    bookingId,
                    $"race-initiation-{Guid.NewGuid():N}"),
                cancellationToken);

        Assert.True(
            result.IsSuccess,
            result.IsFailure
                ? result.Error.Code
                : string.Empty);

        return result.Value;
    }

    private async Task<Result> ExpireAfterGateAsync(
        Guid bookingId,
        Task startGate,
        CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(cancellationToken);

        // Scope independiente: diferente DbContext/transacción.
        using IServiceScope scope =
            _factory.Services.CreateScope();

        var executor = scope.ServiceProvider
            .GetRequiredService<
                ICommandExecutor<ExpireBookingPaymentCommand>>();

        return await executor.ExecuteAsync(
            new ExpireBookingPaymentCommand(bookingId),
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWebhookAfterGateAsync(
        string payload,
        Task startGate,
        CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(cancellationToken);

        string signature = CreateSignatureHeader(
            payload,
            BookingApiFactory.StripeWebhookSecret,
            DateTimeOffset.UtcNow);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            WebhookEndpoint);

        request.Content = new StringContent(
            payload,
            Encoding.UTF8,
            "application/json");

        request.Headers.TryAddWithoutValidation(
            "Stripe-Signature",
            signature);

        return await _factory.Client.SendAsync(
            request,
            cancellationToken);
    }

    private static string CreateSucceededPayload(
        Guid bookingId,
        string externalReference)
    {
        return JsonSerializer.Serialize(new
        {
            id = $"evt_expiration_race_{Guid.NewGuid():N}",
            type = "payment_intent.succeeded",
            data = new
            {
                @object = new
                {
                    id = externalReference,
                    metadata = new Dictionary<string, string>
                    {
                        ["bookify_booking_id"] =
                            bookingId.ToString("D")
                    }
                }
            }
        });
    }

    private static string CreateSignatureHeader(
        string payload,
        string secret,
        DateTimeOffset timestamp)
    {
        long seconds = timestamp.ToUnixTimeSeconds();

        byte[] secretBytes =
            Encoding.UTF8.GetBytes(secret);

        byte[] signedBytes =
            Encoding.UTF8.GetBytes(
                $"{seconds}.{payload}");

        using var hmac = new HMACSHA256(secretBytes);

        byte[] signature = hmac.ComputeHash(signedBytes);

        return $"t={seconds},v1=" +
               Convert.ToHexString(signature).ToLowerInvariant();
    }
}
