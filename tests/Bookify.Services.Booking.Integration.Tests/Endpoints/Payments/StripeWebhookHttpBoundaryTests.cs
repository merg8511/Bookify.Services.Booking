using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Payment;
using Bookify.Services.Booking.Api.Endpoints.Payments.GetStatus;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class StripeWebhookHttpBoundaryTests
{
    private const string WebhookPath =
        "/api/v1/payments/webhooks/stripe";

    private readonly BookingApiFactory _factory;

    public StripeWebhookHttpBoundaryTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SignedSucceededEvent_WithoutBookingMetadata_ShouldBeIgnored()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        PendingPaymentSeed seed =
            await SeedPendingPaymentAsync(cancellationToken);

        // La firma es correcta, pero falta metadata.bookify_booking_id.
        string payload = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = "payment_intent.succeeded",
            data = new
            {
                @object = new
                {
                    id = seed.ExternalReference
                }
            }
        });

        // ACT
        using HttpResponseMessage response =
            await PostSignedWebhookAsync(
                payload,
                cancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await AssertPendingStateAsync(
            seed,
            cancellationToken);
    }

    [Fact]
    public async Task SignedSucceededEvent_WithInvalidBookingId_ShouldReturnValidationProblem()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        PendingPaymentSeed seed =
            await SeedPendingPaymentAsync(cancellationToken);

        string payload = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = "payment_intent.succeeded",
            data = new
            {
                @object = new
                {
                    id = seed.ExternalReference,
                    metadata = new Dictionary<string, string>
                    {
                        ["bookify_booking_id"] = "not-a-guid"
                    }
                }
            }
        });

        // ACT
        using HttpResponseMessage response =
            await PostSignedWebhookAsync(
                payload,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(
                    cancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(
            "Payments.Webhook.InvalidBookingId",
            problem.Code);

        Assert.Equal(400, problem.Status);

        await AssertPendingStateAsync(
            seed,
            cancellationToken);
    }

    [Fact]
    public async Task SignedSucceededEvent_WithoutDataObject_ShouldReturnInvalidPayload()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        PendingPaymentSeed seed =
            await SeedPendingPaymentAsync(cancellationToken);

        // No contiene data.object, requerido para un evento
        // payment_intent.succeeded.
        string payload = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = "payment_intent.succeeded"
        });

        // ACT
        using HttpResponseMessage response =
            await PostSignedWebhookAsync(
                payload,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(
                    cancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(
            "Payments.Webhook.InvalidPayload",
            problem.Code);

        await AssertPendingStateAsync(
            seed,
            cancellationToken);
    }

    [Fact]
    public async Task SucceededWebhook_ShouldSynchronizePublicAndAdminPaymentViews()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        PendingPaymentSeed seed =
            await SeedPendingPaymentAsync(cancellationToken);

        string publicStatusPath =
            $"/api/v1/payments/bookings/{seed.BookingId:D}/status";

        string adminPaymentPath =
            $"/api/v1/admin/properties/{seed.PropertyId:D}" +
            $"/bookings/{seed.BookingId:D}/payment";

        using HttpClient customer =
            _factory.CreateCustomerClient();

        using HttpClient owner =
            _factory.CreateOwnerClient();

        // Verificamos el estado público ANTES del webhook.
        using HttpResponseMessage pendingResponse =
            await customer.GetAsync(
                publicStatusPath,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            pendingResponse.StatusCode);

        GetPaymentStatusResponse? pendingStatus =
            await pendingResponse.Content
                .ReadFromJsonAsync<GetPaymentStatusResponse>(
                    cancellationToken);

        Assert.NotNull(pendingStatus);

        Assert.Equal(
            "PendingPayment",
            pendingStatus.BookingStatus);

        Assert.Equal(
            "Pending",
            pendingStatus.PaymentStatus);

        Assert.Equal(
            "Pending",
            pendingStatus.PaymentAttemptStatus);

        // Evento firmado que confirma el pago.
        string payload = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = "payment_intent.succeeded",
            data = new
            {
                @object = new
                {
                    id = seed.ExternalReference,
                    metadata = new Dictionary<string, string>
                    {
                        ["bookify_booking_id"] =
                            seed.BookingId.ToString("D")
                    }
                }
            }
        });

        // ACT
        using HttpResponseMessage webhookResponse =
            await PostSignedWebhookAsync(
                payload,
                cancellationToken);

        using HttpResponseMessage publicResponse =
            await customer.GetAsync(
                publicStatusPath,
                cancellationToken);

        using HttpResponseMessage adminResponse =
            await owner.GetAsync(
                adminPaymentPath,
                cancellationToken);

        // ASSERT — Webhook.
        Assert.Equal(
            HttpStatusCode.OK,
            webhookResponse.StatusCode);

        // ASSERT — Vista pública del cliente.
        Assert.Equal(
            HttpStatusCode.OK,
            publicResponse.StatusCode);

        GetPaymentStatusResponse? publicStatus =
            await publicResponse.Content
                .ReadFromJsonAsync<GetPaymentStatusResponse>(
                    cancellationToken);

        Assert.NotNull(publicStatus);

        Assert.Equal(seed.BookingId, publicStatus.BookingId);
        Assert.Equal("Paid", publicStatus.BookingStatus);
        Assert.Equal("Succeeded", publicStatus.PaymentStatus);
        Assert.Equal(
            "Succeeded",
            publicStatus.PaymentAttemptStatus);

        // ASSERT — Vista administrativa.
        Assert.Equal(
            HttpStatusCode.OK,
            adminResponse.StatusCode);

        GetAdminBookingPaymentResponse? adminStatus =
            await adminResponse.Content
                .ReadFromJsonAsync<GetAdminBookingPaymentResponse>(
                    cancellationToken);

        Assert.NotNull(adminStatus);

        Assert.Equal(seed.PropertyId, adminStatus.PropertyId);
        Assert.Equal(seed.BookingId, adminStatus.BookingId);
        Assert.Equal("Paid", adminStatus.BookingStatus);

        AdminPaymentDetailsResponse payment =
            Assert.IsType<AdminPaymentDetailsResponse>(
                adminStatus.Payment);

        Assert.Equal(seed.PaymentId, payment.Id);
        Assert.Equal("Succeeded", payment.Status);
        Assert.Equal(200m, payment.Amount);
        Assert.Equal("USD", payment.Currency);
        Assert.NotNull(payment.CompletedAtUtc);

        AdminPaymentAttemptResponse attempt =
            Assert.Single(payment.Attempts);

        Assert.Equal(seed.AttemptId, attempt.Id);
        Assert.Equal("Succeeded", attempt.Status);

        Assert.Equal(
            seed.ExternalReference,
            attempt.ExternalReference);

        Assert.NotNull(attempt.CompletedAtUtc);

        Assert.Equal(
            payment.CompletedAtUtc,
            attempt.CompletedAtUtc);

        // El estado público no debe exponer referencias internas
        // ni las claves privadas de idempotencia.
        string publicJson =
            await publicResponse.Content.ReadAsStringAsync(
                cancellationToken);

        Assert.DoesNotContain(
            "externalReference",
            publicJson,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "clientSecret",
            publicJson,
            StringComparison.OrdinalIgnoreCase);

        // La vista administrativa puede mostrar la referencia
        // del proveedor, pero no la clave de idempotencia.
        string adminJson =
            await adminResponse.Content.ReadAsStringAsync(
                cancellationToken);

        Assert.DoesNotContain(
            "idempotencyKey",
            adminJson,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "pendingInitiationKey",
            adminJson,
            StringComparison.OrdinalIgnoreCase);

        // ASSERT — PostgreSQL: estado autoritativo.
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBookingRepository bookings =
            scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        IPaymentRepository payments =
            scope.ServiceProvider
                .GetRequiredService<IPaymentRepository>();

        DomainBooking? storedBooking =
            await bookings.GetByIdAsync(
                seed.BookingId,
                cancellationToken);

        Payment? storedPayment =
            await payments.GetByBookingIdAsync(
                seed.BookingId,
                cancellationToken);

        Assert.NotNull(storedBooking);
        Assert.NotNull(storedPayment);

        Assert.Equal(
            BookingStatus.Paid,
            storedBooking.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            storedPayment.Status);

        PaymentAttempt storedAttempt =
            Assert.Single(storedPayment.Attempts);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            storedAttempt.Status);
    }

    private async Task<PendingPaymentSeed> SeedPendingPaymentAsync(
        CancellationToken cancellationToken)
    {
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        DateTimeOffset createdAtUtc =
            DateTimeOffset.UtcNow.AddMinutes(-2);

        Payment payment = Payment.Create(
            booking.Id,
            Money.Create(200m, "USD").Value,
            createdAtUtc).Value;

        string externalReference =
            $"pi_{Guid.NewGuid():N}";

        PaymentAttempt attempt = payment.AddAttempt(
            $"http-contract-{Guid.NewGuid():N}",
            externalReference,
            createdAtUtc.AddMinutes(1)).Value;

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPaymentRepository repository =
            scope.ServiceProvider
                .GetRequiredService<IPaymentRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

        repository.Add(payment);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new PendingPaymentSeed(
            booking.Id,
            booking.PropertyId,
            payment.Id,
            attempt.Id,
            externalReference);
    }

    private async Task<HttpResponseMessage> PostSignedWebhookAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        string signatureHeader = CreateStripeSignatureHeader(
            payload,
            BookingApiFactory.StripeWebhookSecret,
            DateTimeOffset.UtcNow);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            WebhookPath);

        request.Content = new StringContent(
            payload,
            Encoding.UTF8,
            "application/json");

        request.Headers.TryAddWithoutValidation(
            "Stripe-Signature",
            signatureHeader);

        return await _factory.Client.SendAsync(
            request,
            cancellationToken);
    }

    private static string CreateStripeSignatureHeader(
        string payload,
        string secret,
        DateTimeOffset timestamp)
    {
        long unixTimestamp =
            timestamp.ToUnixTimeSeconds();

        string signedPayload =
            $"{unixTimestamp}.{payload}";

        byte[] secretBytes =
            Encoding.UTF8.GetBytes(secret);

        byte[] payloadBytes =
            Encoding.UTF8.GetBytes(signedPayload);

        using var hmac = new HMACSHA256(secretBytes);

        byte[] hash =
            hmac.ComputeHash(payloadBytes);

        string signature =
            Convert.ToHexString(hash).ToLowerInvariant();

        return $"t={unixTimestamp},v1={signature}";
    }

    private async Task AssertPendingStateAsync(
        PendingPaymentSeed seed,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        IPaymentRepository paymentRepository =
            scope.ServiceProvider
                .GetRequiredService<IPaymentRepository>();

        DomainBooking? booking =
            await bookingRepository.GetByIdAsync(
                seed.BookingId,
                cancellationToken);

        Payment? payment =
            await paymentRepository.GetByBookingIdAsync(
                seed.BookingId,
                cancellationToken);

        Assert.NotNull(booking);
        Assert.NotNull(payment);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(seed.PaymentId, payment.Id);

        Assert.Equal(
            PaymentStatus.Pending,
            payment.Status);

        Assert.Null(payment.CompletedAtUtc);

        PaymentAttempt attempt =
            Assert.Single(payment.Attempts);

        Assert.Equal(seed.AttemptId, attempt.Id);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);

        Assert.Null(attempt.CompletedAtUtc);
    }

    private sealed record PendingPaymentSeed(
        Guid BookingId,
        Guid PropertyId,
        Guid PaymentId,
        Guid AttemptId,
        string ExternalReference);
}
