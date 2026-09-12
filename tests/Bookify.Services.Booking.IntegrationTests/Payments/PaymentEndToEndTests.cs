using Bookify.Services.Booking.Api.Endpoints.Payments.GetStatus;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PaymentEndToEndTests
{
    private const string StripeWebhookEndpoint =
        "/api/v1/payments/webhooks/stripe";

    private readonly BookingApiFactory _factory;

    public PaymentEndToEndTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task SucceededFlow_ShouldRemainIdempotentFromInitiationThroughDuplicateWebhook()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync(
                cancellationToken);

        string idempotencyKey =
            $"e2e-success-{Guid.NewGuid():N}";

        // Act - first initiation
        InitiatePaymentResponse firstInitiation =
            await InitiateSuccessfullyAsync(
                bookingId,
                idempotencyKey,
                cancellationToken);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            firstInitiation.Status);

        Assert.False(
            string.IsNullOrWhiteSpace(
                firstInitiation.ClientSecret));

        // Act - same operation retried with the same key
        InitiatePaymentResponse replayedInitiation =
            await InitiateSuccessfullyAsync(
                bookingId,
                idempotencyKey,
                cancellationToken);

        // Assert initiation idempotency
        Assert.Equal(
            firstInitiation.PaymentId,
            replayedInitiation.PaymentId);

        Assert.Equal(
            firstInitiation.PaymentAttemptId,
            replayedInitiation.PaymentAttemptId);

        Assert.Equal(
            firstInitiation.ExternalReference,
            replayedInitiation.ExternalReference);

        Assert.Equal(
            firstInitiation.ClientSecret,
            replayedInitiation.ClientSecret);

        GetPaymentStatusResponse pendingStatus =
            await GetPaymentStatusAsync(
                bookingId,
                cancellationToken);

        Assert.Equal(
            "PendingPayment",
            pendingStatus.BookingStatus);

        Assert.Equal(
            "Pending",
            pendingStatus.PaymentStatus);

        Assert.Equal(
            "Pending",
            pendingStatus.PaymentAttemptStatus);

        string eventId =
            $"evt_e2e_success_{Guid.NewGuid():N}";

        // Act - provider says succeeded
        using HttpResponseMessage firstWebhookResponse =
            await PostSignedWebhookAsync(
                eventId,
                "payment_intent.succeeded",
                bookingId,
                firstInitiation.ExternalReference,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            firstWebhookResponse.StatusCode);

        // Act - Stripe retries the exact same event
        using HttpResponseMessage duplicateWebhookResponse =
            await PostSignedWebhookAsync(
                eventId,
                "payment_intent.succeeded",
                bookingId,
                firstInitiation.ExternalReference,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            duplicateWebhookResponse.StatusCode);

        // Assert public authoritative status
        GetPaymentStatusResponse finalStatus =
            await GetPaymentStatusAsync(
                bookingId,
                cancellationToken);

        Assert.Equal(
            "Paid",
            finalStatus.BookingStatus);

        Assert.Equal(
            "Succeeded",
            finalStatus.PaymentStatus);

        Assert.Equal(
            "Succeeded",
            finalStatus.PaymentAttemptStatus);

        // Assert persisted aggregate state
        (
            DomainBooking persistedBooking,
            Payment persistedPayment) =
                await LoadPaymentStateAsync(
                    bookingId,
                    cancellationToken);

        Assert.Equal(
            BookingStatus.Paid,
            persistedBooking.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            persistedPayment.Status);

        Assert.Equal(
            firstInitiation.PaymentId,
            persistedPayment.Id);

        PaymentAttempt persistedAttempt =
            Assert.Single(
                persistedPayment.Attempts);

        Assert.Equal(
            firstInitiation.PaymentAttemptId,
            persistedAttempt.Id);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            persistedAttempt.Status);
    }

    [Fact]
    public async Task FailedFlow_ShouldKeepBookingPendingPaymentAndExposeRetryableState()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync(
                cancellationToken);

        InitiatePaymentResponse initiation =
            await InitiateSuccessfullyAsync(
                bookingId,
                $"e2e-failed-{Guid.NewGuid():N}",
                cancellationToken);

        string eventId =
            $"evt_e2e_failed_{Guid.NewGuid():N}";

        // Act
        using HttpResponseMessage webhookResponse =
            await PostSignedWebhookAsync(
                eventId,
                "payment_intent.payment_failed",
                bookingId,
                initiation.ExternalReference,
                cancellationToken);

        // Assert webhook
        Assert.Equal(
            HttpStatusCode.OK,
            webhookResponse.StatusCode);

        // Assert public status
        GetPaymentStatusResponse status =
            await GetPaymentStatusAsync(
                bookingId,
                cancellationToken);

        Assert.Equal(
            "PendingPayment",
            status.BookingStatus);

        Assert.Equal(
            "Failed",
            status.PaymentStatus);

        Assert.Equal(
            "Failed",
            status.PaymentAttemptStatus);

        // Assert persisted state
        (
            DomainBooking booking,
            Payment payment) =
                await LoadPaymentStateAsync(
                    bookingId,
                    cancellationToken);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Failed,
            payment.Status);

        Assert.Null(
            payment.CompletedAtUtc);

        PaymentAttempt attempt =
            Assert.Single(
                payment.Attempts);

        Assert.Equal(
            PaymentAttemptStatus.Failed,
            attempt.Status);

        Assert.NotNull(
            attempt.CompletedAtUtc);
    }

    [Fact]
    public async Task CancelledFlow_ShouldAllowNewAttemptAndLaterSucceed()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync(
                cancellationToken);

        InitiatePaymentResponse firstAttempt =
            await InitiateSuccessfullyAsync(
                bookingId,
                $"e2e-cancelled-first-{Guid.NewGuid():N}",
                cancellationToken);

        // Act - first provider attempt is cancelled
        using HttpResponseMessage cancelledWebhookResponse =
            await PostSignedWebhookAsync(
                $"evt_e2e_cancelled_{Guid.NewGuid():N}",
                "payment_intent.canceled",
                bookingId,
                firstAttempt.ExternalReference,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            cancelledWebhookResponse.StatusCode);

        GetPaymentStatusResponse cancelledStatus =
            await GetPaymentStatusAsync(
                bookingId,
                cancellationToken);

        Assert.Equal(
            "PendingPayment",
            cancelledStatus.BookingStatus);

        Assert.Equal(
            "Cancelled",
            cancelledStatus.PaymentStatus);

        Assert.Equal(
            "Cancelled",
            cancelledStatus.PaymentAttemptStatus);

        // Act - new payment operation, therefore new key
        InitiatePaymentResponse retryAttempt =
            await InitiateSuccessfullyAsync(
                bookingId,
                $"e2e-cancelled-retry-{Guid.NewGuid():N}",
                cancellationToken);

        Assert.Equal(
            firstAttempt.PaymentId,
            retryAttempt.PaymentId);

        Assert.NotEqual(
            firstAttempt.PaymentAttemptId,
            retryAttempt.PaymentAttemptId);

        Assert.NotEqual(
            firstAttempt.ExternalReference,
            retryAttempt.ExternalReference);

        Assert.Equal(
            PaymentAttemptStatus.Pending,
            retryAttempt.Status);

        GetPaymentStatusResponse retryStatus =
            await GetPaymentStatusAsync(
                bookingId,
                cancellationToken);

        Assert.Equal(
            "PendingPayment",
            retryStatus.BookingStatus);

        Assert.Equal(
            "Pending",
            retryStatus.PaymentStatus);

        Assert.Equal(
            "Pending",
            retryStatus.PaymentAttemptStatus);

        // Act - retry succeeds
        using HttpResponseMessage succeededWebhookResponse =
            await PostSignedWebhookAsync(
                $"evt_e2e_retry_success_{Guid.NewGuid():N}",
                "payment_intent.succeeded",
                bookingId,
                retryAttempt.ExternalReference,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            succeededWebhookResponse.StatusCode);

        // Assert final public state
        GetPaymentStatusResponse finalStatus =
            await GetPaymentStatusAsync(
                bookingId,
                cancellationToken);

        Assert.Equal(
            "Paid",
            finalStatus.BookingStatus);

        Assert.Equal(
            "Succeeded",
            finalStatus.PaymentStatus);

        Assert.Equal(
            "Succeeded",
            finalStatus.PaymentAttemptStatus);

        // Assert complete history
        (
            DomainBooking booking,
            Payment payment) =
                await LoadPaymentStateAsync(
                    bookingId,
                    cancellationToken);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        Assert.Equal(
            2,
            payment.Attempts.Count);

        PaymentAttempt persistedFirstAttempt =
            Assert.Single(
                payment.Attempts,
                attempt =>
                    attempt.Id ==
                    firstAttempt.PaymentAttemptId);

        PaymentAttempt persistedRetryAttempt =
            Assert.Single(
                payment.Attempts,
                attempt =>
                    attempt.Id ==
                    retryAttempt.PaymentAttemptId);

        Assert.Equal(
            PaymentAttemptStatus.Cancelled,
            persistedFirstAttempt.Status);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            persistedRetryAttempt.Status);

        Assert.DoesNotContain(
            payment.Attempts,
            attempt =>
                attempt.Status ==
                PaymentAttemptStatus.Pending);
    }

    [Fact]
    public async Task OutOfOrderWebhooks_ShouldNeverRegressSucceededPayment()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync(
                cancellationToken);

        InitiatePaymentResponse initiation =
            await InitiateSuccessfullyAsync(
                bookingId,
                $"e2e-ordering-{Guid.NewGuid():N}",
                cancellationToken);

        // Act 1 - provider failure arrives first
        using HttpResponseMessage failedResponse =
            await PostSignedWebhookAsync(
                $"evt_e2e_ordering_failed_{Guid.NewGuid():N}",
                "payment_intent.payment_failed",
                bookingId,
                initiation.ExternalReference,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            failedResponse.StatusCode);

        // Act 2 - later provider truth says succeeded
        using HttpResponseMessage succeededResponse =
            await PostSignedWebhookAsync(
                $"evt_e2e_ordering_succeeded_{Guid.NewGuid():N}",
                "payment_intent.succeeded",
                bookingId,
                initiation.ExternalReference,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            succeededResponse.StatusCode);

        // Act 3 - stale cancelled event arrives even later
        using HttpResponseMessage lateCancelledResponse =
            await PostSignedWebhookAsync(
                $"evt_e2e_ordering_cancelled_{Guid.NewGuid():N}",
                "payment_intent.canceled",
                bookingId,
                initiation.ExternalReference,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            lateCancelledResponse.StatusCode);

        // Assert
        GetPaymentStatusResponse status =
            await GetPaymentStatusAsync(
                bookingId,
                cancellationToken);

        Assert.Equal(
            "Paid",
            status.BookingStatus);

        Assert.Equal(
            "Succeeded",
            status.PaymentStatus);

        Assert.Equal(
            "Succeeded",
            status.PaymentAttemptStatus);

        (
            DomainBooking booking,
            Payment payment) =
                await LoadPaymentStateAsync(
                    bookingId,
                    cancellationToken);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

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
    public async Task WebhookAndNewPaymentAttemptRace_ShouldEndWithSingleSucceededAttemptAndPaidBooking()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await CreatePendingPaymentBookingAsync(
                cancellationToken);

        InitiatePaymentResponse currentAttempt =
            await InitiateSuccessfullyAsync(
                bookingId,
                $"e2e-race-first-{Guid.NewGuid():N}",
                cancellationToken);

        string eventId =
            $"evt_e2e_race_success_{Guid.NewGuid():N}";

        using IServiceScope initiationScope =
            _factory.Services
                .CreateScope();

        ICommandExecutor<
            InitiatePaymentCommand,
            InitiatePaymentResponse> executor =
                initiationScope.ServiceProvider
                    .GetRequiredService<
                        ICommandExecutor<
                            InitiatePaymentCommand,
                            InitiatePaymentResponse>>();

        var competingCommand =
            new InitiatePaymentCommand(
                bookingId,
                $"e2e-race-second-{Guid.NewGuid():N}");

        var startGate =
            new TaskCompletionSource(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        async Task<Result<InitiatePaymentResponse>>
            InitiateCompetingAttemptAsync()
        {
            await startGate.Task;

            return await executor.ExecuteAsync(
                competingCommand,
                cancellationToken);
        }

        async Task<HttpResponseMessage>
            SendSucceededWebhookAsync()
        {
            await startGate.Task;

            return await PostSignedWebhookAsync(
                eventId,
                "payment_intent.succeeded",
                bookingId,
                currentAttempt.ExternalReference,
                cancellationToken);
        }

        Task<Result<InitiatePaymentResponse>>
            competingInitiationTask =
                InitiateCompetingAttemptAsync();

        Task<HttpResponseMessage>
            webhookTask =
                SendSucceededWebhookAsync();

        // Act
        startGate.SetResult();

        Result<InitiatePaymentResponse>
            competingInitiation =
                await competingInitiationTask;

        using HttpResponseMessage webhookResponse =
            await webhookTask;

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            webhookResponse.StatusCode);

        //
        // Regardless of which operation acquired the Booking
        // row lock first, creating another payment attempt
        // must lose:
        //
        // - before webhook: an active Pending attempt exists;
        // - after webhook: Payment/Booking are already succeeded.
        //
        Assert.True(
            competingInitiation.IsFailure);

        GetPaymentStatusResponse status =
            await GetPaymentStatusAsync(
                bookingId,
                cancellationToken);

        Assert.Equal(
            "Paid",
            status.BookingStatus);

        Assert.Equal(
            "Succeeded",
            status.PaymentStatus);

        Assert.Equal(
            "Succeeded",
            status.PaymentAttemptStatus);

        (
            DomainBooking booking,
            Payment payment) =
                await LoadPaymentStateAsync(
                    bookingId,
                    cancellationToken);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Equal(
            PaymentStatus.Succeeded,
            payment.Status);

        PaymentAttempt attempt =
            Assert.Single(
                payment.Attempts);

        Assert.Equal(
            currentAttempt.PaymentAttemptId,
            attempt.Id);

        Assert.Equal(
            PaymentAttemptStatus.Succeeded,
            attempt.Status);

        Assert.DoesNotContain(
            payment.Attempts,
            candidate =>
                candidate.Status ==
                PaymentAttemptStatus.Pending);
    }

    private async Task<InitiatePaymentResponse>
        InitiateSuccessfullyAsync(
            Guid bookingId,
            string idempotencyKey,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        ICommandExecutor<
            InitiatePaymentCommand,
            InitiatePaymentResponse> executor =
                scope.ServiceProvider
                    .GetRequiredService<
                        ICommandExecutor<
                            InitiatePaymentCommand,
                            InitiatePaymentResponse>>();

        var command =
            new InitiatePaymentCommand(
                bookingId,
                idempotencyKey);

        Result<InitiatePaymentResponse> result =
            await executor.ExecuteAsync(
                command,
                cancellationToken);

        Assert.True(
            result.IsSuccess,
            result.Error.Message);

        return result.Value;
    }

    private async Task<GetPaymentStatusResponse>
        GetPaymentStatusAsync(
            Guid bookingId,
            CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _factory.Client
                .GetAsync(
                    $"/api/v1/payments/bookings/" +
                    $"{bookingId:D}/status",
                    cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetPaymentStatusResponse? body =
            await response.Content
                .ReadFromJsonAsync<
                    GetPaymentStatusResponse>(
                        cancellationToken);

        Assert.NotNull(
            body);

        return body;
    }

    private async Task<HttpResponseMessage>
        PostSignedWebhookAsync(
            string eventId,
            string eventType,
            Guid bookingId,
            string externalReference,
            CancellationToken cancellationToken)
    {
        string payload =
            JsonSerializer.Serialize(
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
                                        new Dictionary<
                                            string,
                                            string>
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

        string signatureHeader =
            CreateStripeSignatureHeader(
                payload,
                BookingApiFactory
                    .StripeWebhookSecret,
                DateTimeOffset.UtcNow);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                StripeWebhookEndpoint);

        request.Content =
            new StringContent(
                payload,
                Encoding.UTF8,
                "application/json");

        request.Headers
            .TryAddWithoutValidation(
                "Stripe-Signature",
                signatureHeader);

        return await _factory.Client
            .SendAsync(
                request,
                cancellationToken);
    }

    private static string
        CreateStripeSignatureHeader(
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

    private async Task<(
        DomainBooking Booking,
        Payment Payment)>
        LoadPaymentStateAsync(
            Guid bookingId,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        DomainBooking? booking =
            await bookingRepository
                .GetByIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            booking);

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        Payment payment =
            await dbContext.Payments
                .Include(
                    currentPayment =>
                        currentPayment.Attempts)
                .AsNoTracking()
                .SingleAsync(
                    currentPayment =>
                        currentPayment.BookingId ==
                        bookingId,
                    cancellationToken);

        return (
            booking,
            payment);
    }

    private async Task<Guid>
        CreatePendingPaymentBookingAsync(
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        Property property =
            CreateProperty();

        dbContext.Properties.Add(
            property);

        await dbContext
            .SaveChangesAsync(
                cancellationToken);

        RentableUnit rentableUnit =
            CreateRentableUnit(
                property.Id);

        dbContext.RentableUnits.Add(
            rentableUnit);

        await dbContext
            .SaveChangesAsync(
                cancellationToken);

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

        GuestCount guestCount =
            GuestCount.Create(
                2)
            .Value;

        PriceSnapshot priceSnapshot =
            CreatePriceSnapshot();

        Result<DomainBooking> bookingResult =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount,
                GuestDetails.Create(
    "John Doe",
    "john@example.com",
    "+50377778888").Value,
                priceSnapshot);

        Assert.True(
            bookingResult.IsSuccess);

        DomainBooking booking =
            bookingResult.Value;

        Result approvalResult =
            booking.Approve();

        Assert.True(
            approvalResult.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        bookingRepository.Add(
            booking);

        await unitOfWork
            .SaveChangesAsync(
                cancellationToken);

        return booking.Id;
    }

    private static Property CreateProperty()
    {
        Result<Property> result =
            Property.Create(
                $"Payment E2E Property {Guid.NewGuid():N}",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0));

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }

    private static RentableUnit
        CreateRentableUnit(
            Guid propertyId)
    {
        Result<RentableUnit> result =
            RentableUnit.Create(
                propertyId,
                $"Payment E2E Room {Guid.NewGuid():N}",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2);

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }

    private static PriceSnapshot
        CreatePriceSnapshot()
    {
        Money accommodationPrice =
            Money.Create(
                200m,
                "USD")
            .Value;

        Money extraGuestPrice =
            Money.Create(
                0m,
                "USD")
            .Value;

        Money totalPrice =
            Money.Create(
                200m,
                "USD")
            .Value;

        return PriceSnapshot.Create(
            new PriceBreakdown(
                accommodationPrice,
                extraGuestPrice,
                totalPrice));
    }
}
