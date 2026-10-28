
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Security")]
public sealed class GuestCredentialSecurityBoundaryTests
{
    private const string GuestHeader = "Booking-Guest-Token";

    private readonly BookingApiFactory _factory;

    public GuestCredentialSecurityBoundaryTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("booking")]
    [InlineData("payment-status")]
    public async Task GuestToken_FromAnotherBooking_ShouldNotGrantReadAccess(
        string resource)
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData target =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        GuestBookingSeedData other =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        Assert.NotEqual(target.Booking.Id, other.Booking.Id);

        using HttpClient client =
            _factory.CreateGuestClient(other.GuestAccessToken);

        string path = resource switch
        {
            "booking" =>
                $"/api/v1/bookings/{target.Booking.Id:D}",

            "payment-status" =>
                $"/api/v1/payments/bookings/{target.Booking.Id:D}/status",

            _ => throw new ArgumentOutOfRangeException(
                nameof(resource))
        };

        // ACT
        using HttpResponseMessage response =
            await client.GetAsync(path, cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ForeignGuestToken_ShouldNotCancelOrInitiatePayment()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData target =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        GuestBookingSeedData other =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        using HttpClient client =
            _factory.CreateGuestClient(other.GuestAccessToken);

        // ACT 1 — Intentar cancelar otra Booking.
        using HttpResponseMessage cancellationResponse =
            await client.PostAsync(
                $"/api/v1/bookings/{target.Booking.Id:D}/cancel",
                content: null,
                cancellationToken);

        // ACT 2 — Intentar crear un pago para otra Booking.
        using var paymentRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/payments");

        paymentRequest.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            $"foreign-guest-{Guid.NewGuid():N}");

        paymentRequest.Content = JsonContent.Create(
            new InitiatePaymentRequest(target.Booking.Id));

        using HttpResponseMessage paymentResponse =
            await client.SendAsync(
                paymentRequest,
                cancellationToken);

        // ASSERT — Ambas operaciones deben ser rechazadas.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            cancellationResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            paymentResponse.StatusCode);

        // ASSERT — Ninguna transición de estado.
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        IPaymentRepository paymentRepository =
            scope.ServiceProvider
                .GetRequiredService<IPaymentRepository>();

        DomainBooking? storedBooking =
            await bookingRepository.GetByIdAsync(
                target.Booking.Id,
                cancellationToken);

        var storedPayment =
            await paymentRepository.GetByBookingIdAsync(
                target.Booking.Id,
                cancellationToken);

        Assert.NotNull(storedBooking);

        Assert.Equal(
            BookingStatus.PendingPayment,
            storedBooking.Status);

        Assert.Null(storedBooking.CancellationReason);
        Assert.Null(storedBooking.CancelledAtUtc);

        Assert.Null(storedPayment);
    }

    [Fact]
    public async Task DuplicateGuestTokenHeader_ShouldBeRejected()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/bookings/{guest.Booking.Id:D}");

        // CAMBIO 22.7:
        // Dos valores en la misma cabecera, aunque ambos
        // sean válidos, no constituyen una credencial válida.
        bool added = request.Headers.TryAddWithoutValidation(
            GuestHeader,
            new[]
            {
                guest.GuestAccessToken,
                guest.GuestAccessToken
            });

        Assert.True(added);

        // ACT
        using HttpResponseMessage response =
            await client.SendAsync(
                request,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task GuestToken_ShouldNotAccessCustomerOwnedBooking()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking customerBooking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                customerSubjectId: TestIdentitySubjects.Customer,
                cancellationToken: cancellationToken);

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                cancellationToken: cancellationToken);

        using HttpClient client =
            _factory.CreateGuestClient(guest.GuestAccessToken);

        // ACT
        using HttpResponseMessage response =
            await client.GetAsync(
                $"/api/v1/bookings/{customerBooking.Id:D}",
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}
