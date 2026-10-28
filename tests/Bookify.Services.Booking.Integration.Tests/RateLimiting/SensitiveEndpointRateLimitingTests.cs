
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bookify.Services.Booking.Integration.Tests.RateLimiting;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Security")]
public sealed class SensitiveEndpointRateLimitingTests
{
    private const string PaymentEndpoint =
        "/api/v1/payments";

    private const string WebhookEndpoint =
        "/api/v1/payments/webhooks/stripe";

    private readonly BookingApiFactory _factory;

    public SensitiveEndpointRateLimitingTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Webhook_WithoutValidSignature_ShouldStillBeRateLimited()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using WebApplicationFactory<Program> limitedFactory =
            CreateLimitedFactory("Webhook");

        using HttpClient client = limitedFactory.CreateClient();

        // ACT
        // Las solicitudes no tienen Stripe-Signature.
        // La primera llega al handler y es rechazada allí.
        using HttpResponseMessage first =
            await SendUnsignedWebhookAsync(
                client,
                cancellationToken);

        // La segunda debe ser rechazada por rate limiting
        // antes del procesamiento del webhook.
        using HttpResponseMessage second =
            await SendUnsignedWebhookAsync(
                client,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            second.StatusCode);

        await AssertRateLimitProblemAsync(
            second,
            cancellationToken);
    }

    [Fact]
    public async Task PaymentInitiation_DifferentCustomers_ShouldHaveIndependentQuotas()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string firstSubject =
            $"payment-customer-a-{Guid.NewGuid():N}";

        string secondSubject =
            $"payment-customer-b-{Guid.NewGuid():N}";

        var firstBooking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                customerSubjectId: firstSubject,
                cancellationToken: cancellationToken);

        var secondBooking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                customerSubjectId: secondSubject,
                cancellationToken: cancellationToken);

        using WebApplicationFactory<Program> limitedFactory =
            CreateLimitedFactory("PaymentInitiation");

        using HttpClient firstCustomer =
            CreateAuthenticatedCustomer(
                limitedFactory,
                firstSubject);

        using HttpClient secondCustomer =
            CreateAuthenticatedCustomer(
                limitedFactory,
                secondSubject);

        // ACT
        using HttpResponseMessage firstAllowed =
            await InitiatePaymentAsync(
                firstCustomer,
                firstBooking.Id,
                cancellationToken);

        using HttpResponseMessage firstRejected =
            await InitiatePaymentAsync(
                firstCustomer,
                firstBooking.Id,
                cancellationToken);

        using HttpResponseMessage secondAllowed =
            await InitiatePaymentAsync(
                secondCustomer,
                secondBooking.Id,
                cancellationToken);

        using HttpResponseMessage secondRejected =
            await InitiatePaymentAsync(
                secondCustomer,
                secondBooking.Id,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            firstAllowed.StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            firstRejected.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            secondAllowed.StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            secondRejected.StatusCode);

        await AssertRateLimitProblemAsync(
            firstRejected,
            cancellationToken);

        await AssertRateLimitProblemAsync(
            secondRejected,
            cancellationToken);
    }

    [Fact]
    public async Task PaymentInitiation_DifferentGuestTokens_ShouldHaveIndependentQuotas()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData firstGuest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        GuestBookingSeedData secondGuest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        Assert.NotEqual(
            firstGuest.GuestAccessToken,
            secondGuest.GuestAccessToken);

        using WebApplicationFactory<Program> limitedFactory =
            CreateLimitedFactory("PaymentInitiation");

        using HttpClient firstClient =
            CreateGuestClient(
                limitedFactory,
                firstGuest.GuestAccessToken);

        using HttpClient secondClient =
            CreateGuestClient(
                limitedFactory,
                secondGuest.GuestAccessToken);

        // Ambas credenciales provienen de la misma IP de
        // TestServer, pero deben tener cuotas independientes.

        // ACT
        using HttpResponseMessage firstAllowed =
            await InitiatePaymentAsync(
                firstClient,
                firstGuest.Booking.Id,
                cancellationToken);

        using HttpResponseMessage firstRejected =
            await InitiatePaymentAsync(
                firstClient,
                firstGuest.Booking.Id,
                cancellationToken);

        using HttpResponseMessage secondAllowed =
            await InitiatePaymentAsync(
                secondClient,
                secondGuest.Booking.Id,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            firstAllowed.StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            firstRejected.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            secondAllowed.StatusCode);

        await AssertRateLimitProblemAsync(
            firstRejected,
            cancellationToken);
    }

    private WebApplicationFactory<Program> CreateLimitedFactory(
        string policy)
    {
        // CAMBIO 22.7:
        // La factoría original sigue usando cuotas altas.
        // Esta factoría derivada limita únicamente la
        // política que estamos probando.
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:Database",
                _factory.DatabaseConnectionString);

            builder.UseSetting(
                $"RateLimiting:{policy}:PermitLimit",
                "1");

            builder.UseSetting(
                $"RateLimiting:{policy}:Window",
                "01:00:00");
        });
    }

    private static HttpClient CreateAuthenticatedCustomer(
        WebApplicationFactory<Program> factory,
        string subject)
    {
        HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                JwtBearerDefaults.AuthenticationScheme,
                TestIdentityTokens.Create(
                    subject,
                    BookifyRoles.Customer));

        return client;
    }

    private static HttpClient CreateGuestClient(
        WebApplicationFactory<Program> factory,
        string guestToken)
    {
        HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });

        bool added =
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Booking-Guest-Token",
                guestToken);

        Assert.True(added);

        return client;
    }

    private static async Task<HttpResponseMessage>
        InitiatePaymentAsync(
            HttpClient client,
            Guid bookingId,
            CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            PaymentEndpoint);

        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            $"security-limit-{Guid.NewGuid():N}");

        request.Content = JsonContent.Create(
            new InitiatePaymentRequest(bookingId));

        return await client.SendAsync(
            request,
            cancellationToken);
    }

    private static async Task<HttpResponseMessage>
        SendUnsignedWebhookAsync(
            HttpClient client,
            CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            WebhookEndpoint);

        request.Content = new StringContent(
            """{"id":"evt_rate_limit_test","type":"payment_intent.processing"}""",
            Encoding.UTF8,
            "application/json");

        return await client.SendAsync(
            request,
            cancellationToken);
    }

    private static async Task AssertRateLimitProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(
                    cancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(429, problem.Status);

        Assert.Equal(
            "RateLimit.Exceeded",
            problem.Code);

        Assert.Equal(
            "urn:bookify:problem-type:rate-limit",
            problem.Type);

        Assert.False(
            string.IsNullOrWhiteSpace(problem.TraceId));
    }
}
