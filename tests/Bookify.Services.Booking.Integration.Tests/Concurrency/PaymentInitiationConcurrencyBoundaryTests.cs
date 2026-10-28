using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Concurrency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Concurrency")]
public sealed class PaymentInitiationConcurrencyBoundaryTests
{
    private const string Endpoint = "/api/v1/payments";
    private const int RequestCount = 6;

    private readonly BookingApiFactory _factory;

    public PaymentInitiationConcurrencyBoundaryTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentInitiations_ShouldPersistOnlyOnePendingAttempt(
        bool useSameIdempotencyKey)
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        using HttpClient customer =
            _factory.CreateCustomerClient();

        var startGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        string sharedKey =
            $"concurrent-payment-{Guid.NewGuid():N}";

        Task<HttpResponseMessage>[] tasks =
            Enumerable.Range(0, RequestCount)
                .Select(index =>
                {
                    string key = useSameIdempotencyKey
                        ? sharedKey
                        : $"concurrent-payment-{Guid.NewGuid():N}";

                    return SendAfterGateAsync(
                        customer,
                        booking.Id,
                        key,
                        startGate.Task,
                        cancellationToken);
                })
                .ToArray();

        // ACT
        startGate.SetResult(true);

        HttpResponseMessage[] responses =
            await Task.WhenAll(tasks);

        try
        {
            // ASSERT — HTTP
            Assert.Contains(
                responses,
                response =>
                    response.StatusCode == HttpStatusCode.OK);

            Assert.All(
                responses,
                response => Assert.True(
                    response.StatusCode is
                        HttpStatusCode.OK or
                        HttpStatusCode.Conflict,
                    $"Unexpected HTTP response: {response.StatusCode}"));

            // Claves diferentes representan operaciones diferentes:
            // solo una puede iniciar un intento Pending.
            if (!useSameIdempotencyKey)
            {
                Assert.Single(
                    responses,
                    response =>
                        response.StatusCode == HttpStatusCode.OK);

                Assert.Equal(
                    RequestCount - 1,
                    responses.Count(response =>
                        response.StatusCode ==
                        HttpStatusCode.Conflict));
            }

            // ASSERT — PostgreSQL
            using IServiceScope scope =
                _factory.Services.CreateScope();

            BookingDbContext dbContext =
                scope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            Payment payment =
                await dbContext.Payments
                    .Include(current => current.Attempts)
                    .AsNoTracking()
                    .SingleAsync(
                        current =>
                            current.BookingId == booking.Id,
                        cancellationToken);

            PaymentAttempt attempt =
                Assert.Single(payment.Attempts);

            Assert.Equal(
                PaymentStatus.Pending,
                payment.Status);

            Assert.Equal(
                PaymentAttemptStatus.Pending,
                attempt.Status);

            Assert.Null(payment.PendingInitiationKey);
            Assert.Null(payment.PendingInitiationCreatedAtUtc);

            Assert.Null(attempt.CompletedAtUtc);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    private static async Task<HttpResponseMessage> SendAfterGateAsync(
        HttpClient client,
        Guid bookingId,
        string idempotencyKey,
        Task startGate,
        CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Endpoint);

        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            idempotencyKey);

        request.Content = JsonContent.Create(
            new InitiatePaymentRequest(bookingId));

        return await client.SendAsync(
            request,
            cancellationToken);
    }
}
