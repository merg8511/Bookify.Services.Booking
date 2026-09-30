using Bookify.Services.Booking.Api.Endpoints.Payments.GetStatus;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class GetPaymentStatusEndpointTests
{
    private readonly BookingApiFactory _factory;

    public GetPaymentStatusEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_WhenBookingHasNoPayment_ShouldReturnBookingStatusAndNullPaymentStatuses()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await SeedPendingPaymentBookingAsync(cancellationToken);
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(BuildEndpoint(bookingId), cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        GetPaymentStatusResponse? body = await response.Content
            .ReadFromJsonAsync<GetPaymentStatusResponse>(cancellationToken);

        Assert.NotNull(body);
        Assert.Equal(bookingId, body.BookingId);
        Assert.Equal("PendingPayment", body.BookingStatus);
        Assert.Null(body.PaymentStatus);
        Assert.Null(body.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Get_WhenPaymentHasPendingAttempt_ShouldReturnPendingStatuses()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (Guid bookingId, Payment payment, PaymentAttempt attempt) = await SeedPaymentWithAttemptAsync(cancellationToken);
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(BuildEndpoint(bookingId), cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        GetPaymentStatusResponse? body = await response.Content
            .ReadFromJsonAsync<GetPaymentStatusResponse>(cancellationToken);

        Assert.NotNull(body);
        Assert.Equal(bookingId, body.BookingId);
        Assert.Equal("PendingPayment", body.BookingStatus);
        Assert.Equal(payment.Status.ToString(), body.PaymentStatus);
        Assert.Equal(attempt.Status.ToString(), body.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Get_WhenPaymentWasRetried_ShouldReturnLatestAttemptStatus()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await SeedRetriedPaymentAsync(cancellationToken);
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(BuildEndpoint(bookingId), cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        GetPaymentStatusResponse? body = await response.Content
            .ReadFromJsonAsync<GetPaymentStatusResponse>(cancellationToken);

        Assert.NotNull(body);
        Assert.Equal("PendingPayment", body.BookingStatus);
        Assert.Equal("Pending", body.PaymentStatus);
        Assert.Equal("Pending", body.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Get_WhenPaymentSucceeded_ShouldReturnPaidAndSucceededStatuses()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = await SeedSucceededPaymentAsync(cancellationToken);
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(BuildEndpoint(bookingId), cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        GetPaymentStatusResponse? body = await response.Content
            .ReadFromJsonAsync<GetPaymentStatusResponse>(cancellationToken);

        Assert.NotNull(body);
        Assert.Equal("Paid", body.BookingStatus);
        Assert.Equal("Succeeded", body.PaymentStatus);
        Assert.Equal("Succeeded", body.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Get_WhenBookingDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        Guid bookingId = Guid.NewGuid();
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(
            BuildEndpoint(bookingId),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetailsResponse? problem = await response.Content
            .ReadFromJsonAsync<ProblemDetailsResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal("Booking.NotFound", problem.Code);
    }

    [Fact]
    public async Task Get_WhenBookingIdIsEmpty_ShouldReturnBadRequest()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(
            BuildEndpoint(Guid.Empty),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetailsResponse? problem = await response.Content
            .ReadFromJsonAsync<ProblemDetailsResponse>(TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal("Booking.InvalidId", problem.Code);
    }

    private async Task<Guid> SeedPendingPaymentBookingAsync(CancellationToken cancellationToken)
    {
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingPayment,
            cancellationToken: cancellationToken);

        return booking.Id;
    }

    private async Task<(Guid BookingId, Payment Payment, PaymentAttempt Attempt)> SeedPaymentWithAttemptAsync(
        CancellationToken cancellationToken)
    {
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingPayment,
            cancellationToken: cancellationToken);

        DateTimeOffset paymentCreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        Payment payment = Payment.Create(
            booking.Id,
            Money.Create(200m, "USD").Value,
            paymentCreatedAtUtc).Value;

        Result<PaymentAttempt> attemptResult = payment.AddAttempt(
            $"status-{Guid.NewGuid():N}",
            $"pi_{Guid.NewGuid():N}",
            paymentCreatedAtUtc.AddMinutes(1));

        Assert.True(attemptResult.IsSuccess);

        await PersistPaymentAsync(payment, cancellationToken);

        return (booking.Id, payment, attemptResult.Value);
    }

    private async Task<Guid> SeedRetriedPaymentAsync(CancellationToken cancellationToken)
    {
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingPayment,
            cancellationToken: cancellationToken);

        DateTimeOffset paymentCreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        Payment payment = Payment.Create(
            booking.Id,
            Money.Create(200m, "USD").Value,
            paymentCreatedAtUtc).Value;

        Result<PaymentAttempt> firstAttemptResult = payment.AddAttempt(
            $"status-first-{Guid.NewGuid():N}",
            $"pi_{Guid.NewGuid():N}",
            paymentCreatedAtUtc.AddMinutes(1));

        Assert.True(firstAttemptResult.IsSuccess);

        Result failedResult = payment.MarkAttemptAsFailed(
            firstAttemptResult.Value.ExternalReference,
            paymentCreatedAtUtc.AddMinutes(2));

        Assert.True(failedResult.IsSuccess);

        Result<PaymentAttempt> retryResult = payment.AddAttempt(
            $"status-retry-{Guid.NewGuid():N}",
            $"pi_{Guid.NewGuid():N}",
            paymentCreatedAtUtc.AddMinutes(3));

        Assert.True(retryResult.IsSuccess);

        await PersistPaymentAsync(payment, cancellationToken);

        return booking.Id;
    }

    private async Task<Guid> SeedSucceededPaymentAsync(CancellationToken cancellationToken)
    {
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.Paid,
            cancellationToken: cancellationToken);

        DateTimeOffset paymentCreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3);
        Payment payment = Payment.Create(
            booking.Id,
            Money.Create(200m, "USD").Value,
            paymentCreatedAtUtc).Value;

        Result<PaymentAttempt> attemptResult = payment.AddAttempt(
            $"status-success-{Guid.NewGuid():N}",
            $"pi_{Guid.NewGuid():N}",
            paymentCreatedAtUtc.AddMinutes(1));

        Assert.True(attemptResult.IsSuccess);

        Result paymentSucceededResult = payment.MarkAttemptAsSucceeded(
            attemptResult.Value.ExternalReference,
            paymentCreatedAtUtc.AddMinutes(2));

        Assert.True(paymentSucceededResult.IsSuccess);

        await PersistPaymentAsync(payment, cancellationToken);

        return booking.Id;
    }

    private async Task PersistPaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IPaymentRepository paymentRepository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        paymentRepository.Add(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string BuildEndpoint(Guid bookingId) => $"/api/v1/payments/bookings/{bookingId:D}/status";
}
