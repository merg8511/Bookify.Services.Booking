using Bookify.Services.Booking.Application.Abstractions.Payments.Webhooks;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Payments.Webhooks;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PaymentWebhookEventStoreTests
{
    private readonly BookingApiFactory _factory;

    public PaymentWebhookEventStoreTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }
    [Fact]
    public async Task PrepareAsync_WhenEventIsNew_ShouldPersistProcessedEvent()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string eventId =
            $"evt_{Guid.NewGuid():N}";

        DateTimeOffset receivedAtUtc =
            TruncateToMicroseconds(
                DateTimeOffset.UtcNow);

        DateTimeOffset processedAtUtc =
            receivedAtUtc.AddSeconds(
                1);

        using (
            IServiceScope scope =
                CreateScope())
        {
            IPaymentWebhookEventStore store =
                scope.ServiceProvider
                    .GetRequiredService<
                        IPaymentWebhookEventStore>();

            IUnitOfWork unitOfWork =
                scope.ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            Result<PaymentWebhookEventPreparation>
                preparationResult =
                    await store.PrepareAsync(
                        PaymentWebhookProviders.Stripe,
                        eventId,
                        "payment_intent.succeeded",
                        receivedAtUtc,
                        cancellationToken);

            Assert.True(
                preparationResult.IsSuccess);

            Assert.Equal(
                PaymentWebhookPreparationStatus
                    .ReadyToProcess,
                preparationResult.Value.Status);

            await store.MarkProcessedAsync(
                preparationResult.Value.EventRecordId,
                processedAtUtc,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        using IServiceScope verificationScope =
            CreateScope();

        IPaymentWebhookEventStore verificationStore =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IPaymentWebhookEventStore>();

        StoredPaymentWebhookEvent? storedEvent =
            await verificationStore.GetAsync(
                PaymentWebhookProviders.Stripe,
                eventId,
                cancellationToken);

        Assert.NotNull(
            storedEvent);

        Assert.Equal(
            eventId,
            storedEvent.EventId);

        Assert.Equal(
            "payment_intent.succeeded",
            storedEvent.EventType);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            storedEvent.ProcessingStatus);

        Assert.Equal(
            receivedAtUtc,
            storedEvent.ReceivedAtUtc);

        Assert.Equal(
            processedAtUtc,
            storedEvent.ProcessedAtUtc);

        Assert.Null(
            storedEvent.ErrorCode);

        Assert.Null(
            storedEvent.ErrorMessage);
    }

    [Fact]
    public async Task PrepareAsync_WhenEventWasAlreadyProcessed_ShouldReturnAlreadyProcessed()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string eventId =
            $"evt_{Guid.NewGuid():N}";

        DateTimeOffset receivedAtUtc =
            TruncateToMicroseconds(
                DateTimeOffset.UtcNow);

        Guid originalRecordId;

        using (
            IServiceScope setupScope =
                CreateScope())
        {
            IPaymentWebhookEventStore store =
                setupScope.ServiceProvider
                    .GetRequiredService<
                        IPaymentWebhookEventStore>();

            IUnitOfWork unitOfWork =
                setupScope.ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            Result<PaymentWebhookEventPreparation>
                preparationResult =
                    await store.PrepareAsync(
                        PaymentWebhookProviders.Stripe,
                        eventId,
                        "payment_intent.succeeded",
                        receivedAtUtc,
                        cancellationToken);

            Assert.True(
                preparationResult.IsSuccess);

            originalRecordId =
                preparationResult.Value.EventRecordId;

            await store.MarkProcessedAsync(
                originalRecordId,
                receivedAtUtc.AddSeconds(1),
                cancellationToken);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        using IServiceScope retryScope =
            CreateScope();

        IPaymentWebhookEventStore retryStore =
            retryScope.ServiceProvider
                .GetRequiredService<
                    IPaymentWebhookEventStore>();

        Result<PaymentWebhookEventPreparation>
            retryResult =
                await retryStore.PrepareAsync(
                    PaymentWebhookProviders.Stripe,
                    eventId,
                    "payment_intent.succeeded",
                    receivedAtUtc.AddMinutes(1),
                    cancellationToken);

        Assert.True(
            retryResult.IsSuccess);

        Assert.Equal(
            PaymentWebhookPreparationStatus
                .AlreadyProcessed,
            retryResult.Value.Status);

        Assert.Equal(
            originalRecordId,
            retryResult.Value.EventRecordId);
    }

    [Fact]
    public async Task PrepareAsync_WhenEventPreviouslyFailed_ShouldAllowRetryAndClearErrorOnSuccess()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string eventId =
            $"evt_{Guid.NewGuid():N}";

        DateTimeOffset firstReceivedAtUtc =    TruncateToMicroseconds(            DateTimeOffset.UtcNow);

        Guid originalRecordId;

        Error technicalError =
            Error.Failure(
                "Payments.Webhook.TestFailure",
                "Test webhook failure.");

        using (
            IServiceScope failureScope =
                CreateScope())
        {
            IPaymentWebhookEventStore store =
                failureScope.ServiceProvider
                    .GetRequiredService<
                        IPaymentWebhookEventStore>();

            IUnitOfWork unitOfWork =
                failureScope.ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            Result<PaymentWebhookEventPreparation>
                preparationResult =
                    await store.PrepareAsync(
                        PaymentWebhookProviders.Stripe,
                        eventId,
                        "payment_intent.succeeded",
                        firstReceivedAtUtc,
                        cancellationToken);

            Assert.True(
                preparationResult.IsSuccess);

            originalRecordId =
                preparationResult.Value.EventRecordId;

            await store.MarkFailedAsync(
                originalRecordId,
                technicalError,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        using (
            IServiceScope retryScope =
                CreateScope())
        {
            IPaymentWebhookEventStore store =
                retryScope.ServiceProvider
                    .GetRequiredService<
                        IPaymentWebhookEventStore>();

            IUnitOfWork unitOfWork =
                retryScope.ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            Result<PaymentWebhookEventPreparation>
                retryResult =
                    await store.PrepareAsync(
                        PaymentWebhookProviders.Stripe,
                        eventId,
                        "payment_intent.succeeded",
                        firstReceivedAtUtc
                            .AddMinutes(5),
                        cancellationToken);

            Assert.True(
                retryResult.IsSuccess);

            Assert.Equal(
                PaymentWebhookPreparationStatus
                    .ReadyToProcess,
                retryResult.Value.Status);

            Assert.Equal(
                originalRecordId,
                retryResult.Value.EventRecordId);

            await store.MarkProcessedAsync(
                retryResult.Value.EventRecordId,
                firstReceivedAtUtc
                    .AddMinutes(5),
                cancellationToken);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        using IServiceScope verificationScope =
            CreateScope();

        IPaymentWebhookEventStore verificationStore =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IPaymentWebhookEventStore>();

        StoredPaymentWebhookEvent? storedEvent =
            await verificationStore.GetAsync(
                PaymentWebhookProviders.Stripe,
                eventId,
                cancellationToken);

        Assert.NotNull(
            storedEvent);

        Assert.Equal(
            originalRecordId,
            storedEvent.Id);

        Assert.Equal(
            PaymentWebhookProcessingStatus.Processed,
            storedEvent.ProcessingStatus);

        Assert.Equal(
            firstReceivedAtUtc,
            storedEvent.ReceivedAtUtc);

        Assert.Null(
            storedEvent.ErrorCode);

        Assert.Null(
            storedEvent.ErrorMessage);
    }

    private IServiceScope CreateScope()
    {
        return _factory.Services
            .CreateScope();
    }

    private static DateTimeOffset
    TruncateToMicroseconds(
        DateTimeOffset value)
    {
        long remainder =
            value.Ticks %
            TimeSpan.TicksPerMicrosecond;

        return value.AddTicks(
            -remainder);
    }
}
