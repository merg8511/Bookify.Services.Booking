using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class CancelBookingEndpointTests
{
    private readonly BookingApiFactory _factory;

    public CancelBookingEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WhenBookingIsPendingApproval_ReturnsNoContentAndPersistsCancellation()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                BookingStatus.PendingApproval,
                cancellationToken: cancellationToken);

        DomainBooking booking =
            guest.Booking;

        using HttpClient client =
            _factory.CreateGuestClient(
                guest.GuestAccessToken);

        using HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/cancel",
                content: null,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBookingRepository repository =
            scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        DomainBooking? persistedBooking =
            await repository.GetByIdAsync(
                booking.Id,
                cancellationToken);

        Assert.NotNull(
            persistedBooking);

        Assert.Equal(
            BookingStatus.Cancelled,
            persistedBooking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            persistedBooking.CancellationReason);

        Assert.False(
            persistedBooking.BlocksInventory);

        IBookingReadService readService =
            scope.ServiceProvider
                .GetRequiredService<IBookingReadService>();

        BookingDetailsReadModel? readModel =
            await readService.GetByIdAsync(
                booking.Id,
                cancellationToken);

        Assert.NotNull(
            readModel);

        Assert.Equal(
            BookingStatus.Cancelled.ToString(),
            readModel.Status);
    }

    [Fact]
    public async Task Post_WhenBookingIsPendingPaymentWithoutPayment_ReturnsNoContentAndPersistsCancellation()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        DomainBooking booking =
            guest.Booking;

        using HttpClient client =
            _factory.CreateGuestClient(
                guest.GuestAccessToken);

        using HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/cancel",
                content: null,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBookingRepository repository =
            scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        DomainBooking? persistedBooking =
            await repository.GetByIdAsync(
                booking.Id,
                cancellationToken);

        Assert.NotNull(
            persistedBooking);

        Assert.Equal(
            BookingStatus.Cancelled,
            persistedBooking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            persistedBooking.CancellationReason);

        Assert.False(
            persistedBooking.BlocksInventory);
    }

    [Fact]
    public async Task Post_WhenBookingHasPendingProviderPayment_CancelsProviderPaymentAndPersistsBothCancellations()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        DomainBooking booking =
            guest.Booking;

        string externalReference;

        using (IServiceScope setupScope =
            _factory.Services.CreateScope())
        {
            IPaymentGateway paymentGateway =
                setupScope.ServiceProvider
                    .GetRequiredService<IPaymentGateway>();

            IPaymentRepository paymentRepository =
                setupScope.ServiceProvider
                    .GetRequiredService<IPaymentRepository>();

            IUnitOfWork unitOfWork =
                setupScope.ServiceProvider
                    .GetRequiredService<IUnitOfWork>();

            Money amount =
                Money.Create(
                    200m,
                    "USD").Value;

            string operationKey =
                $"cancel-booking-{Guid.NewGuid():N}";

            Result<CreatePaymentAttemptResponse> providerResult =
                await paymentGateway.CreatePaymentAttemptAsync(
                    new CreatePaymentAttemptRequest(
                        booking.Id,
                        amount,
                        operationKey),
                    cancellationToken);

            Assert.True(
                providerResult.IsSuccess);

            Assert.Equal(
                PaymentGatewayStatus.Pending,
                providerResult.Value.Status);

            externalReference =
                providerResult.Value.ExternalReference;

            DateTimeOffset createdAtUtc =
                DateTimeOffset.UtcNow;

            Result<Payment> paymentResult =
                Payment.Create(
                    booking.Id,
                    amount,
                    createdAtUtc);

            Assert.True(
                paymentResult.IsSuccess);

            Payment payment =
                paymentResult.Value;

            Result<PaymentAttempt> attemptResult =
                payment.AddAttempt(
                    operationKey,
                    externalReference,
                    createdAtUtc);

            Assert.True(
                attemptResult.IsSuccess);

            paymentRepository.Add(
                payment);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        using HttpClient client =
            _factory.CreateGuestClient(
                guest.GuestAccessToken);

        // Act
        using HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/cancel",
                content: null,
                cancellationToken);

        // Assert HTTP
        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        // Assert DB + provider
        using IServiceScope verificationScope =
            _factory.Services.CreateScope();

        IBookingRepository bookingRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        IPaymentRepository verificationPaymentRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<IPaymentRepository>();

        IPaymentGateway verificationPaymentGateway =
            verificationScope.ServiceProvider
                .GetRequiredService<IPaymentGateway>();

        DomainBooking? persistedBooking =
            await bookingRepository.GetByIdAsync(
                booking.Id,
                cancellationToken);

        Assert.NotNull(
            persistedBooking);

        Assert.Equal(
            BookingStatus.Cancelled,
            persistedBooking.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest,
            persistedBooking.CancellationReason);

        Payment? persistedPayment =
            await verificationPaymentRepository
                .GetByBookingIdAsync(
                    booking.Id,
                    cancellationToken);

        Assert.NotNull(
            persistedPayment);

        Assert.Equal(
            PaymentStatus.Cancelled,
            persistedPayment.Status);

        PaymentAttempt persistedAttempt =
            Assert.Single(
                persistedPayment.Attempts);

        Assert.Equal(
            PaymentAttemptStatus.Cancelled,
            persistedAttempt.Status);

        Assert.NotNull(
            persistedAttempt.CompletedAtUtc);

        Result<PaymentGatewayResponse> providerStatusResult =
            await verificationPaymentGateway
                .GetPaymentStatusAsync(
                    externalReference,
                    cancellationToken);

        Assert.True(
            providerStatusResult.IsSuccess);

        Assert.Equal(
            PaymentGatewayStatus.Cancelled,
            providerStatusResult.Value.Status);
    }

    [Fact]
    public async Task Post_WhenBookingIsPaid_ReturnsConflict()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                BookingStatus.Paid,
                customerSubjectId:
                    TestIdentitySubjects.Customer,
                cancellationToken:
                    cancellationToken);

        using HttpClient client =
            _factory.CreateCustomerClient();

        using HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/cancel",
                content: null,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(
                    cancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Booking.InvalidStatusTransition",
            problem.Code);
    }

    [Fact]
    public async Task Post_WhenBookingDoesNotExist_ReturnsNotFound()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        using HttpClient client =
            _factory.CreateCustomerClient();

        using HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{bookingId}/cancel",
                content: null,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(
                    cancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Booking.NotFound",
            problem.Code);
    }

    [Fact]
    public async Task Post_WithEmptyBookingId_ReturnsBadRequest()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpClient client =
            _factory.CreateCustomerClient();

        using HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{Guid.Empty}/cancel",
                content: null,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>(
                    cancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Booking.InvalidId",
            problem.Code);
    }
}
