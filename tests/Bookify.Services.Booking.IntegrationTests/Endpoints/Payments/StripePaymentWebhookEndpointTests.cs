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
using System.Text;
using System.Text.Json;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class StripePaymentWebhookEndpointTests
{
    private const string Endpoint =
        "/api/v1/payments/webhooks/stripe";

    private readonly BookingApiFactory _factory;

    public StripePaymentWebhookEndpointTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task Post_WhenPaymentIntentSucceeded_ShouldReturnOkAndPersistReconciliation()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (
            Guid bookingId,
            Guid paymentId,
            Guid attemptId,
            string externalReference) =
                await SeedPendingPaymentAsync(
                    cancellationToken);

        string payload =
            CreatePayload(
                "payment_intent.succeeded",
                bookingId,
                externalReference);

        HttpClient client =
            _factory.CreateClient();

        using var content =
            new StringContent(
                payload,
                Encoding.UTF8,
                "application/json");

        // Act
        HttpResponseMessage response =
            await client.PostAsync(
                Endpoint,
                content,
                cancellationToken);

        // Assert HTTP
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        // Assert persisted state
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
            attemptId,
            attempt.Id);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);
    }

    [Fact]
    public async Task Post_WhenEventIsIrrelevant_ShouldReturnOkWithoutChangingPayment()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (
            Guid bookingId,
            Guid paymentId,
            Guid attemptId,
            string externalReference) =
                await SeedPendingPaymentAsync(
                    cancellationToken);

        string payload =
            CreatePayload(
                "payment_intent.processing",
                bookingId,
                externalReference);

        HttpClient client =
            _factory.CreateClient();

        using var content =
            new StringContent(
                payload,
                Encoding.UTF8,
                "application/json");

        // Act
        HttpResponseMessage response =
            await client.PostAsync(
                Endpoint,
                content,
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

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
            BookingStatus.PendingPayment,
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
            PaymentStatus.Pending,
            payment.Status);

        PaymentAttempt attempt =
            Assert.Single(
                payment.Attempts);

        Assert.Equal(
            attemptId,
            attempt.Id);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            attempt.Status);
    }

    [Fact]
    public async Task Post_WhenPayloadIsMalformed_ShouldReturnBadRequest()
    {
        // Arrange
        HttpClient client =
            _factory.CreateClient();

        using var content =
            new StringContent(
                "{ invalid-json",
                Encoding.UTF8,
                "application/json");

        // Act
        HttpResponseMessage response =
            await client.PostAsync(
                Endpoint,
                content,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        TestContext.Current.CancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Payments.Webhook.InvalidPayload",
            problem.Code);
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
                $"Webhook Property {Guid.NewGuid():N}",
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

        DateTimeOffset attemptCreatedAtUtc =
            paymentCreatedAtUtc
                .AddMinutes(1);

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
                $"webhook-operation-{Guid.NewGuid():N}",
                externalReference,
                attemptCreatedAtUtc);

        Assert.True(
            attemptResult.IsSuccess);

        PaymentAttempt attempt =
            attemptResult.Value;

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
            attempt.Id,
            externalReference);
    }

    private static string CreatePayload(
        string eventType,
        Guid bookingId,
        string externalReference)
    {
        return JsonSerializer.Serialize(
            new
            {
                id = $"evt_{Guid.NewGuid():N}",
                type = eventType,
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
}
