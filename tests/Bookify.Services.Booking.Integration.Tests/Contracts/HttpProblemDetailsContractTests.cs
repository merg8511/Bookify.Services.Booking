using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Contracts;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class HttpProblemDetailsContractTests
{
    private const string ValidationType =
        "urn:bookify:problem-type:validation";

    private const string NotFoundType =
        "urn:bookify:problem-type:not-found";

    private const string ConflictType =
        "urn:bookify:problem-type:conflict";

    private readonly BookingApiFactory _factory;

    public HttpProblemDetailsContractTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminBookings_InvalidStatus_ShouldReturnCompleteValidationProblem()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        SeedData property =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: cancellationToken);

        string path =
            $"/api/v1/admin/properties/{property.PropertyId:D}/bookings";

        using HttpClient admin = _factory.CreateAdminClient();

        // ACT
        using HttpResponseMessage response = await admin.GetAsync(
            $"{path}?status=Unknown",
            cancellationToken);

        // ASSERT
        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ValidationType,
            "Validation error",
            "AdminBookings.InvalidStatus",
            path,
            cancellationToken);
    }

    [Fact]
    public async Task AdminBooking_FromAnotherProperty_ShouldReturnCompleteNotFoundProblem()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                cancellationToken: cancellationToken);

        SeedData otherProperty =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: cancellationToken);

        string path =
            $"/api/v1/admin/properties/{otherProperty.PropertyId:D}" +
            $"/bookings/{booking.Id:D}";

        using HttpClient admin = _factory.CreateAdminClient();

        // ACT
        using HttpResponseMessage response = await admin.GetAsync(
            path,
            cancellationToken);

        // ASSERT
        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            NotFoundType,
            "Resource not found",
            "Booking.NotFound",
            path,
            cancellationToken);
    }

    [Fact]
    public async Task InitiatePayment_BeforeApproval_ShouldReturnCompleteConflictProblem()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                cancellationToken: cancellationToken);

        const string path = "/api/v1/payments";

        using HttpClient customer = _factory.CreateCustomerClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            path);

        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            $"problem-contract-{Guid.NewGuid():N}");

        request.Content = JsonContent.Create(
            new InitiatePaymentRequest(booking.Id));

        // ACT
        using HttpResponseMessage response =
            await customer.SendAsync(
                request,
                cancellationToken);

        // ASSERT
        await AssertProblemAsync(
            response,
            HttpStatusCode.Conflict,
            ConflictType,
            "Conflict",
            "Payments.Initiate.BookingNotPendingPayment",
            path,
            cancellationToken);
    }

    [Fact]
    public async Task StripeWebhook_WithoutSignature_ShouldReturnCompleteValidationProblem()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        const string path =
            "/api/v1/payments/webhooks/stripe";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            path);

        request.Content = new StringContent(
            """{"id":"evt_missing_signature","type":"payment_intent.succeeded"}""",
            Encoding.UTF8,
            "application/json");

        // ACT
        using HttpResponseMessage response =
            await _factory.Client.SendAsync(
                request,
                cancellationToken);

        // ASSERT
        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            ValidationType,
            "Validation error",
            "Payments.Webhook.SignatureRequired",
            path,
            cancellationToken);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedType,
        string expectedTitle,
        string expectedCode,
        string expectedInstance,
        CancellationToken cancellationToken)
    {
        // Verificamos tanto el contrato HTTP como su cuerpo.
        Assert.Equal(expectedStatus, response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(
                    cancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(expectedType, problem.Type);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedCode, problem.Code);
        Assert.Equal(expectedInstance, problem.Instance);

        Assert.False(
            string.IsNullOrWhiteSpace(problem.Detail));

        Assert.False(
            string.IsNullOrWhiteSpace(problem.TraceId));
    }
}
