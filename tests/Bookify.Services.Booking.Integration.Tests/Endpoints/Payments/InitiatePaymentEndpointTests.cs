using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class InitiatePaymentEndpointTests
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly BookingApiFactory _factory;

    public InitiatePaymentEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WithPendingPaymentBooking_ReturnsPayment()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreateBookingAsync(approve: true, cancellationToken);
        var request = new InitiatePaymentRequest(bookingId);
        string idempotencyKey = $"payment-http-{Guid.NewGuid():N}";

        // Act
        HttpResponseMessage response = await PostPaymentAsync(request, idempotencyKey, cancellationToken);

        // Assert - HTTP
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain("externalReference", json, StringComparison.OrdinalIgnoreCase);

        InitiatePaymentResponse? body = JsonSerializer.Deserialize<InitiatePaymentResponse>(json, _jsonSerializerOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.ClientSecret));
        Assert.StartsWith("fake_", body.ClientSecret, StringComparison.Ordinal);
        Assert.NotEqual(Guid.Empty, body.PaymentId);
        Assert.NotEqual(Guid.Empty, body.PaymentAttemptId);
        Assert.Equal(PaymentAttemptStatus.Pending.ToString(), body.Status);
        Assert.Equal(200m, body.Amount);
        Assert.Equal("USD", body.Currency);

        // Assert - PostgreSQL
        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        Payment payment = await dbContext.Payments
            .AsNoTracking()
            .SingleAsync(p => p.BookingId == bookingId, cancellationToken);

        PaymentAttempt attempt = await dbContext.PaymentAttempts
            .AsNoTracking()
            .SingleAsync(a => a.PaymentId == payment.Id, cancellationToken);

        Assert.Equal(body.PaymentId, payment.Id);
        Assert.Equal(body.PaymentAttemptId, attempt.Id);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(PaymentAttemptStatus.Pending, attempt.Status);
        Assert.Equal(body.Amount, payment.Amount.Amount);
        Assert.Equal(body.Currency, payment.Amount.Currency);
    }

    [Fact]
    public async Task Post_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreateBookingAsync(approve: true, cancellationToken);
        var request = new InitiatePaymentRequest(bookingId);

        // Act
        HttpResponseMessage response = await PostPaymentAsync(request, idempotencyKey: null, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        bool paymentExists = await dbContext.Payments
            .AsNoTracking()
            .AnyAsync(payment => payment.BookingId == bookingId, cancellationToken);

        Assert.False(paymentExists);
    }

    [Fact]
    public async Task Post_WithEmptyBookingId_ReturnsBadRequest()
    {
        // Arrange
        var request = new InitiatePaymentRequest(Guid.Empty);

        // Act
        HttpResponseMessage response = await PostPaymentAsync(
            request,
            $"payment-http-{Guid.NewGuid():N}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_WithMissingBooking_ReturnsNotFound()
    {
        // Arrange
        var request = new InitiatePaymentRequest(Guid.NewGuid());

        // Act
        HttpResponseMessage response = await PostPaymentAsync(
            request,
            $"payment-http-{Guid.NewGuid():N}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_WhenBookingIsNotPendingPayment_ReturnsConflict()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreateBookingAsync(approve: false, cancellationToken);
        var request = new InitiatePaymentRequest(bookingId);

        // Act
        HttpResponseMessage response = await PostPaymentAsync(
            request,
            $"payment-http-{Guid.NewGuid():N}",
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_WithSameIdempotencyKey_ShouldReturnSamePaymentSession()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreateBookingAsync(approve: true, cancellationToken);
        var request = new InitiatePaymentRequest(bookingId);
        string idempotencyKey = $"payment-http-{Guid.NewGuid():N}";

        // Act
        HttpResponseMessage firstResponse = await PostPaymentAsync(request, idempotencyKey, cancellationToken);
        HttpResponseMessage secondResponse = await PostPaymentAsync(request, idempotencyKey, cancellationToken);

        // Assert - HTTP
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        InitiatePaymentResponse? firstBody = await firstResponse.Content.ReadFromJsonAsync<InitiatePaymentResponse>(cancellationToken);
        InitiatePaymentResponse? secondBody = await secondResponse.Content.ReadFromJsonAsync<InitiatePaymentResponse>(cancellationToken);

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody, secondBody);
        Assert.Equal(firstBody.ClientSecret, secondBody.ClientSecret);

        // Assert - only one physical operation
        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        Payment payment = await dbContext.Payments
            .AsNoTracking()
            .SingleAsync(p => p.BookingId == bookingId, cancellationToken);

        int attemptCount = await dbContext.PaymentAttempts
            .AsNoTracking()
            .CountAsync(attempt => attempt.PaymentId == payment.Id, cancellationToken);

        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task Post_WithSameIdempotencyKeyForDifferentBooking_ShouldReturnConflict()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid firstBookingId = await CreateBookingAsync(approve: true, cancellationToken);
        Guid secondBookingId = await CreateBookingAsync(approve: true, cancellationToken);
        string idempotencyKey = $"payment-http-{Guid.NewGuid():N}";

        // Act
        HttpResponseMessage firstResponse = await PostPaymentAsync(
            new InitiatePaymentRequest(firstBookingId),
            idempotencyKey,
            cancellationToken);

        HttpResponseMessage secondResponse = await PostPaymentAsync(
            new InitiatePaymentRequest(secondBookingId),
            idempotencyKey,
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);

        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        bool secondPaymentExists = await dbContext.Payments
            .AsNoTracking()
            .AnyAsync(payment => payment.BookingId == secondBookingId, cancellationToken);

        Assert.False(secondPaymentExists);
    }

    [Fact]
    public async Task Post_WithClientSuppliedAmount_ShouldUseBookingPriceSnapshot()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreateBookingAsync(approve: true, cancellationToken);
        var request = new
        {
            BookingId = bookingId,
            Amount = 0.01m,
            Currency = "EUR"
        };

        // Act
        HttpResponseMessage response = await PostPaymentAsync(
            request,
            $"payment-http-{Guid.NewGuid():N}",
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        InitiatePaymentResponse? body = await response.Content.ReadFromJsonAsync<InitiatePaymentResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(200m, body.Amount);
        Assert.Equal("USD", body.Currency);
    }

    [Fact]
    public async Task Post_ShouldNotPersistClientSecretInIdempotencyResponse()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreateBookingAsync(approve: true, cancellationToken);
        string idempotencyKey = $"payment-sensitive-{Guid.NewGuid():N}";
        var request = new InitiatePaymentRequest(bookingId);

        // Act
        HttpResponseMessage response = await PostPaymentAsync(request, idempotencyKey, cancellationToken);

        // Assert HTTP
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        InitiatePaymentResponse? body = await response.Content.ReadFromJsonAsync<InitiatePaymentResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.ClientSecret));

        // Assert idempotency persistence
        using IServiceScope scope = _factory.Services.CreateScope();
        IDbConnectionFactory connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        IdempotencyStoredResponse stored = await connection.QuerySingleAsync<IdempotencyStoredResponse>(
            new CommandDefinition(
                """
                SELECT
                    status_code AS "StatusCode",
                    response_body AS "ResponseBody"
                FROM idempotency_requests
                WHERE key = @Key;
                """,
                new { Key = idempotencyKey },
                cancellationToken: cancellationToken));

        Assert.Equal(StatusCodes.Status200OK, stored.StatusCode);
        Assert.Null(stored.ResponseBody);
    }

    [Fact]
    public async Task Post_WhenPreviousAttemptWasCancelledAndNewKeyIsUsed_ShouldCreateNewAttempt()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await CreateBookingAsync(approve: true, cancellationToken);

        // DB Setup: Payment with Cancelled Attempt
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var payment = Payment.Create(bookingId, BookingTestData.CreatePriceSnapshot().TotalPrice, DateTimeOffset.UtcNow).Value;

            string previousIdempotencyKey = $"cancelled-payment-{Guid.NewGuid():N}";
            string previousExternalReference = $"cancelled-external-{Guid.NewGuid():N}";
            var attempt = payment.AddAttempt(previousIdempotencyKey, previousExternalReference, DateTimeOffset.UtcNow).Value;
            payment.CancelAttempt(previousExternalReference, DateTimeOffset.UtcNow);

            dbContext.Payments.Add(payment);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var request = new InitiatePaymentRequest(bookingId);
        string idempotencyKey = $"retry-new-key-{Guid.NewGuid():N}";

        // Act
        HttpResponseMessage response = await PostPaymentAsync(request, idempotencyKey, cancellationToken);

        // Assert HTTP
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        InitiatePaymentResponse? body = await response.Content.ReadFromJsonAsync<InitiatePaymentResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(PaymentAttemptStatus.Pending.ToString(), body.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.ClientSecret));

        // DB Assert
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var payment = await dbContext.Payments
                .Include(p => p.Attempts)
                .AsNoTracking()
                .SingleAsync(p => p.BookingId == bookingId, cancellationToken);

            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Equal(2, payment.Attempts.Count);
        }
    }

    private async Task<HttpResponseMessage> PostPaymentAsync<TRequest>(
        TRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        HttpClient client = _factory.CreateClient();
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments");

        if (idempotencyKey is not null)
        {
            message.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        message.Content = JsonContent.Create(request);

        return await client.SendAsync(message, cancellationToken);
    }

    private async Task<Guid> CreateBookingAsync(bool approve, CancellationToken cancellationToken)
    {
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: approve ? BookingStatus.PendingPayment : BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        return booking.Id;
    }

    private sealed class IdempotencyStoredResponse
    {
        public int StatusCode { get; init; }

        public string? ResponseBody { get; init; }
    }
}

