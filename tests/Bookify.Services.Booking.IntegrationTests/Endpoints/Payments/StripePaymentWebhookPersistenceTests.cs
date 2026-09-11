using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class StripePaymentWebhookPersistenceTests
{
    private const string Endpoint =
        "/api/v1/payments/webhooks/stripe";

    private readonly BookingApiFactory _factory;

    public StripePaymentWebhookPersistenceTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task Post_WhenProcessedEventIsDeliveredAgain_ShouldReturnOkWithoutUpdatingWebhookRecord()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (
            Guid bookingId,
            Guid paymentId,
            string externalReference) =
                await SeedPendingPaymentAsync(
                    includeAttempt: true,
                    cancellationToken);

        string eventId =
            $"evt_{Guid.NewGuid():N}";

        string payload =
            CreatePayload(
                eventId,
                "payment_intent.succeeded",
                bookingId,
                externalReference);

        string signature =
            CreateSignatureHeader(
                payload,
                BookingApiFactory
                    .StripeWebhookSecret,
                DateTimeOffset.UtcNow);

        HttpClient client =
            _factory.CreateClient();

        HttpResponseMessage firstResponse =
            await PostWebhookAsync(
                client,
                payload,
                signature,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            firstResponse.StatusCode);

        StoredPaymentWebhookEvent firstStoredEvent =
            await GetRequiredStoredEventAsync(
                eventId,
                cancellationToken);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            firstStoredEvent.ProcessingStatus);

        Assert.NotNull(
            firstStoredEvent.ProcessedAtUtc);

        DateTimeOffset? originalProcessedAtUtc =
            firstStoredEvent.ProcessedAtUtc;

        Guid originalRecordId =
            firstStoredEvent.Id;

        HttpResponseMessage secondResponse =
            await PostWebhookAsync(
                client,
                payload,
                signature,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode);

        StoredPaymentWebhookEvent secondStoredEvent =
            await GetRequiredStoredEventAsync(
                eventId,
                cancellationToken);

        Assert.Equal(
            originalRecordId,
            secondStoredEvent.Id);

        Assert.Equal(
            originalProcessedAtUtc,
            secondStoredEvent.ProcessedAtUtc);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            secondStoredEvent.ProcessingStatus);

        using IServiceScope verificationScope =
            _factory.Services
                .CreateScope();

        IBookingRepository bookingRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IPaymentRepository paymentRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IPaymentRepository>();

        DomainBooking? booking =
            await bookingRepository
                .GetByIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            booking);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Payment? payment =
            await paymentRepository
                .GetByBookingIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            payment);

        Assert.Equal(
            paymentId,
            payment.Id);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        PaymentAttempt attempt =
            Assert.Single(
                payment.Attempts);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);
    }

    [Fact]
    public async Task Post_WhenFailedEventIsRetriedAfterStateIsFixed_ShouldProcessSameStoredEvent()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (
            Guid bookingId,
            Guid paymentId,
            string externalReference) =
                await SeedPendingPaymentAsync(
                    includeAttempt: false,
                    cancellationToken);

        string eventId =
            $"evt_{Guid.NewGuid():N}";

        string payload =
            CreatePayload(
                eventId,
                "payment_intent.succeeded",
                bookingId,
                externalReference);

        string signature =
            CreateSignatureHeader(
                payload,
                BookingApiFactory
                    .StripeWebhookSecret,
                DateTimeOffset.UtcNow);

        HttpClient client =
            _factory.CreateClient();

        // First delivery fails because the local
        // PaymentAttempt does not exist yet.
        HttpResponseMessage firstResponse =
            await PostWebhookAsync(
                client,
                payload,
                signature,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            firstResponse.StatusCode);

        ProblemDetailsResponse? firstProblem =
            await firstResponse.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        cancellationToken);

        Assert.NotNull(
            firstProblem);

        Assert.Equal(
            "Payments.Webhook.PaymentAttemptNotFound",
            firstProblem.Code);

        StoredPaymentWebhookEvent failedEvent =
            await GetRequiredStoredEventAsync(
                eventId,
                cancellationToken);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Failed,
            failedEvent.ProcessingStatus);

        Assert.Equal(
            "Payments.Webhook.PaymentAttemptNotFound",
            failedEvent.ErrorCode);

        Assert.Null(
            failedEvent.ProcessedAtUtc);

        Guid eventRecordId =
            failedEvent.Id;

        // Repair the local state.
        using (
            IServiceScope repairScope =
                _factory.Services
                    .CreateScope())
        {
            IPaymentRepository paymentRepository =
                repairScope.ServiceProvider
                    .GetRequiredService<
                        IPaymentRepository>();

            IUnitOfWork unitOfWork =
                repairScope.ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            Payment? payment =
                await paymentRepository
                    .GetByBookingIdAsync(
                        bookingId,
                        cancellationToken);

            Assert.NotNull(
                payment);

            Assert.Equal(
                paymentId,
                payment.Id);

            Result<PaymentAttempt> attemptResult =
                payment.AddAttempt(
                    $"webhook-retry-{Guid.NewGuid():N}",
                    externalReference,
                    DateTimeOffset.UtcNow);

            Assert.True(
                attemptResult.IsSuccess);

            await unitOfWork
                .SaveChangesAsync(
                    cancellationToken);
        }

        // Same Stripe EventId is delivered again.
        HttpResponseMessage secondResponse =
            await PostWebhookAsync(
                client,
                payload,
                signature,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode);

        StoredPaymentWebhookEvent processedEvent =
            await GetRequiredStoredEventAsync(
                eventId,
                cancellationToken);

        Assert.Equal(
            eventRecordId,
            processedEvent.Id);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            processedEvent.ProcessingStatus);

        Assert.NotNull(
            processedEvent.ProcessedAtUtc);

        Assert.Null(
            processedEvent.ErrorCode);

        Assert.Null(
            processedEvent.ErrorMessage);

        using IServiceScope verificationScope =
            _factory.Services
                .CreateScope();

        IBookingRepository bookingRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IPaymentRepository verificationPaymentRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IPaymentRepository>();

        DomainBooking? booking =
            await bookingRepository
                .GetByIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            booking);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Payment? persistedPayment =
            await verificationPaymentRepository
                .GetByBookingIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            persistedPayment);

        Assert.Equal(
            PaymentStatus.Succeeded,
            persistedPayment.Status);

        PaymentAttempt persistedAttempt =
            Assert.Single(
                persistedPayment.Attempts);

        Assert.Equal(
            externalReference,
            persistedAttempt.ExternalReference);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            persistedAttempt.Status);
    }

    private async Task<
        StoredPaymentWebhookEvent>
        GetRequiredStoredEventAsync(
            string eventId,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPaymentWebhookEventStore store =
            scope.ServiceProvider
                .GetRequiredService<
                    IPaymentWebhookEventStore>();

        StoredPaymentWebhookEvent? storedEvent =
            await store.GetAsync(
                PaymentWebhookProviders.Stripe,
                eventId,
                cancellationToken);

        Assert.NotNull(
            storedEvent);

        return storedEvent;
    }

    private async Task<(
        Guid BookingId,
        Guid PaymentId,
        string ExternalReference)>
        SeedPendingPaymentAsync(
            bool includeAttempt,
            CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                $"Webhook Persistence {Guid.NewGuid():N}",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0))
            .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                property.Id,
                $"Webhook Room {Guid.NewGuid():N}",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(
                    2026,
                    10,
                    10),
                new DateOnly(
                    2026,
                    10,
                    12))
            .Value;

        DomainBooking booking =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(
                    2)
                .Value)
            .Value;

        Assert.True(
            booking.Approve().IsSuccess);

        DateTimeOffset paymentCreatedAtUtc =
            DateTimeOffset.UtcNow
                .AddMinutes(-2);

        Payment payment =
            Payment.Create(
                booking.Id,
                Money.Create(
                    200m,
                    "USD")
                .Value,
                paymentCreatedAtUtc)
            .Value;

        string externalReference =
            $"pi_{Guid.NewGuid():N}";

        if (includeAttempt)
        {
            Result<PaymentAttempt> attemptResult =
                payment.AddAttempt(
                    $"webhook-operation-{Guid.NewGuid():N}",
                    externalReference,
                    paymentCreatedAtUtc
                        .AddMinutes(1));

            Assert.True(
                attemptResult.IsSuccess);
        }

        using IServiceScope setupScope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            setupScope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            setupScope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IBookingRepository bookingRepository =
            setupScope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IPaymentRepository paymentRepository =
            setupScope.ServiceProvider
                .GetRequiredService<
                    IPaymentRepository>();

        IUnitOfWork unitOfWork =
            setupScope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        bookingRepository.Add(
            booking);

        paymentRepository.Add(
            payment);

        await unitOfWork
            .SaveChangesAsync(
                cancellationToken);

        return (
            booking.Id,
            payment.Id,
            externalReference);
    }

    private static async Task<HttpResponseMessage>
        PostWebhookAsync(
            HttpClient client,
            string payload,
            string signatureHeader,
            CancellationToken cancellationToken)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                Endpoint);

        request.Content =
            new StringContent(
                payload,
                Encoding.UTF8,
                "application/json");

        request.Headers
            .TryAddWithoutValidation(
                "Stripe-Signature",
                signatureHeader);

        return await client.SendAsync(
            request,
            cancellationToken);
    }

    private static string CreatePayload(
        string eventId,
        string eventType,
        Guid bookingId,
        string externalReference)
    {
        return JsonSerializer.Serialize(
            new
            {
                id =
                    eventId,

                type =
                    eventType,

                data =
                    new
                    {
                        @object =
                            new
                            {
                                id =
                                    externalReference,

                                metadata =
                                    new Dictionary<string, string>
                                    {
                                        [
                                            "bookify_booking_id"
                                        ] =
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
        long unixTimestamp =
            timestamp.ToUnixTimeSeconds();

        string signedPayload =
            $"{unixTimestamp}.{payload}";

        byte[] secretBytes =
            Encoding.UTF8.GetBytes(
                secret);

        byte[] payloadBytes =
            Encoding.UTF8.GetBytes(
                signedPayload);

        using var hmac =
            new HMACSHA256(
                secretBytes);

        byte[] hash =
            hmac.ComputeHash(
                payloadBytes);

        string signature =
            Convert.ToHexString(
                    hash)
                .ToLowerInvariant();

        return
            $"t={unixTimestamp},v1={signature}";
    }
}
