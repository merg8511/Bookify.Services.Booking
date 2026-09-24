using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class StripePaymentWebhookConcurrencyTests
{
    private const string Endpoint =
        "/api/v1/payments/webhooks/stripe";

    private readonly BookingApiFactory _factory;

    public StripePaymentWebhookConcurrencyTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task Post_WhenSameEventIsDeliveredConcurrently_ShouldReturnOkForBothAndProcessOnce()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (
            Guid bookingId,
            Guid paymentId,
            Guid attemptId,
            string externalReference) =
                await SeedPendingPaymentAsync(
                    cancellationToken);

        string eventId =
            $"evt_concurrent_duplicate_{Guid.NewGuid():N}";

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

        HttpClient firstClient =
            _factory.CreateClient();

        HttpClient secondClient =
            _factory.CreateClient();

        Task<HttpResponseMessage> firstTask =
            PostWebhookAsync(
                firstClient,
                payload,
                signature,
                cancellationToken);

        Task<HttpResponseMessage> secondTask =
            PostWebhookAsync(
                secondClient,
                payload,
                signature,
                cancellationToken);

        HttpResponseMessage[] responses =
            await Task.WhenAll(
                firstTask,
                secondTask);

        try
        {
            Assert.All(
                responses,
                response =>
                    Assert.Equal(
                        HttpStatusCode.OK,
                        response.StatusCode));
        }
        finally
        {
            foreach (
                HttpResponseMessage response
                in responses)
            {
                response.Dispose();
            }

            firstClient.Dispose();
            secondClient.Dispose();
        }

        StoredPaymentWebhookEvent storedEvent =
            await GetRequiredStoredEventAsync(
                eventId,
                cancellationToken);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            storedEvent.ProcessingStatus);

        Assert.NotNull(
            storedEvent.ProcessedAtUtc);

        await AssertSucceededStateAsync(
            bookingId,
            paymentId,
            attemptId,
            cancellationToken);
    }

    [Theory]
    [InlineData("payment_intent.payment_failed")]
    [InlineData("payment_intent.canceled")]
    public async Task Post_WhenSucceededAndLateTerminalEventRunConcurrently_ShouldAlwaysEndSucceeded(
        string competingEventType)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (
            Guid bookingId,
            Guid paymentId,
            Guid attemptId,
            string externalReference) =
                await SeedPendingPaymentAsync(
                    cancellationToken);

        string succeededEventId =
            $"evt_success_{Guid.NewGuid():N}";

        string competingEventId =
            $"evt_competing_{Guid.NewGuid():N}";

        string succeededPayload =
            CreatePayload(
                succeededEventId,
                "payment_intent.succeeded",
                bookingId,
                externalReference);

        string competingPayload =
            CreatePayload(
                competingEventId,
                competingEventType,
                bookingId,
                externalReference);

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        string succeededSignature =
            CreateSignatureHeader(
                succeededPayload,
                BookingApiFactory
                    .StripeWebhookSecret,
                now);

        string competingSignature =
            CreateSignatureHeader(
                competingPayload,
                BookingApiFactory
                    .StripeWebhookSecret,
                now);

        HttpClient firstClient =
            _factory.CreateClient();

        HttpClient secondClient =
            _factory.CreateClient();

        Task<HttpResponseMessage> succeededTask =
            PostWebhookAsync(
                firstClient,
                succeededPayload,
                succeededSignature,
                cancellationToken);

        Task<HttpResponseMessage> competingTask =
            PostWebhookAsync(
                secondClient,
                competingPayload,
                competingSignature,
                cancellationToken);

        HttpResponseMessage[] responses =
            await Task.WhenAll(
                succeededTask,
                competingTask);

        try
        {
            Assert.All(
                responses,
                response =>
                    Assert.Equal(
                        HttpStatusCode.OK,
                        response.StatusCode));
        }
        finally
        {
            foreach (
                HttpResponseMessage response
                in responses)
            {
                response.Dispose();
            }

            firstClient.Dispose();
            secondClient.Dispose();
        }

        StoredPaymentWebhookEvent succeededEvent =
            await GetRequiredStoredEventAsync(
                succeededEventId,
                cancellationToken);

        StoredPaymentWebhookEvent competingEvent =
            await GetRequiredStoredEventAsync(
                competingEventId,
                cancellationToken);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            succeededEvent.ProcessingStatus);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            competingEvent.ProcessingStatus);

        await AssertSucceededStateAsync(
            bookingId,
            paymentId,
            attemptId,
            cancellationToken);
    }

    private async Task AssertSucceededStateAsync(
        Guid bookingId,
        Guid paymentId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IPaymentRepository paymentRepository =
            scope.ServiceProvider
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
            attemptId,
            attempt.Id);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);
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
        Guid AttemptId,
        string ExternalReference)>
        SeedPendingPaymentAsync(
            CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                $"Webhook Concurrency {Guid.NewGuid():N}",
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
                    2)                .Value,
                GuestDetails.Create("John Doe", "john@example.com", "+50377778888").Value,
                BookingTestTime.CreatedAtUtc)
            .Value;

        Assert.True(
            booking.Approve(BookingTestTime.ApprovedAtUtc).IsSuccess);

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

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                $"webhook-concurrency-{Guid.NewGuid():N}",
                externalReference,
                paymentCreatedAtUtc
                    .AddMinutes(1));

        Assert.True(
            attemptResult.IsSuccess);

        PaymentAttempt attempt =
            attemptResult.Value;

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IPaymentRepository paymentRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPaymentRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
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

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return (
            booking.Id,
            payment.Id,
            attempt.Id,
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
                                            bookingId
                                                .ToString("D")
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
