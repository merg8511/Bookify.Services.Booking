
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Payment;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminBookingPaymentEndpointTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminBookingPaymentEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    private static string PaymentUrl(
        Guid propertyId,
        Guid bookingId) =>
        $"/api/v1/admin/properties/{propertyId}" +
        $"/bookings/{bookingId}/payment";

    [Fact]
    public async Task Get_WhenBookingHasNoPayment_ShouldReturnNullPayment()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        // ACT
        using HttpResponseMessage response =
            await owner.GetAsync(
                PaymentUrl(booking.PropertyId, booking.Id),
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetAdminBookingPaymentResponse body =
            Assert.IsType<GetAdminBookingPaymentResponse>(
                await response.Content
                    .ReadFromJsonAsync<GetAdminBookingPaymentResponse>(
                        CancellationToken));

        Assert.Equal(booking.PropertyId, body.PropertyId);
        Assert.Equal(booking.Id, body.BookingId);
        Assert.Equal("PendingPayment", body.BookingStatus);
        Assert.Null(body.Payment);
    }

    [Fact]
    public async Task Get_WithRetriedPayment_ShouldReturnAllAttemptsNewestFirst()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: CancellationToken);

        DateTimeOffset createdAtUtc =
            DateTimeOffset.UtcNow.AddMinutes(-10);

        Payment payment = Payment.Create(
            booking.Id,
            Money.Create(250m, "USD").Value,
            createdAtUtc).Value;

        string firstExternalReference =
            $"pi_{Guid.NewGuid():N}";

        string secondExternalReference =
            $"pi_{Guid.NewGuid():N}";

        // First attempt — Failed.
        PaymentAttempt firstAttempt = payment.AddAttempt(
            $"private-first-{Guid.NewGuid():N}",
            firstExternalReference,
            createdAtUtc.AddMinutes(1)).Value;

        var firstFailure = payment.MarkAttemptAsFailed(
            firstExternalReference,
            createdAtUtc.AddMinutes(2));

        Assert.True(firstFailure.IsSuccess);

        // Second attempt — Pending.
        PaymentAttempt secondAttempt = payment.AddAttempt(
            $"private-second-{Guid.NewGuid():N}",
            secondExternalReference,
            createdAtUtc.AddMinutes(3)).Value;

        await PersistPaymentAsync(payment);

        using HttpClient owner = _factory.CreateOwnerClient();

        // ACT
        using HttpResponseMessage response =
            await owner.GetAsync(
                PaymentUrl(booking.PropertyId, booking.Id),
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetAdminBookingPaymentResponse body =
            Assert.IsType<GetAdminBookingPaymentResponse>(
                await response.Content
                    .ReadFromJsonAsync<GetAdminBookingPaymentResponse>(
                        CancellationToken));

        AdminPaymentDetailsResponse result =
            Assert.IsType<AdminPaymentDetailsResponse>(
                body.Payment);

        Assert.Equal(payment.Id, result.Id);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(250m, result.Amount);
        Assert.Equal("USD", result.Currency);

        Assert.False(result.HasPendingInitiation);
        Assert.Null(result.PendingInitiationCreatedAtUtc);

        Assert.Equal(2, result.Attempts.Count);

        AdminPaymentAttemptResponse latest = result.Attempts[0];
        AdminPaymentAttemptResponse previous = result.Attempts[1];

        Assert.Equal(secondAttempt.Id, latest.Id);
        Assert.Equal("Pending", latest.Status);
        Assert.Equal(secondExternalReference, latest.ExternalReference);
        Assert.Equal(250m, latest.Amount);
        Assert.Equal("USD", latest.Currency);
        Assert.Null(latest.CompletedAtUtc);

        Assert.Equal(firstAttempt.Id, previous.Id);
        Assert.Equal("Failed", previous.Status);
        Assert.Equal(firstExternalReference, previous.ExternalReference);
        Assert.NotNull(previous.CompletedAtUtc);

        // Internal idempotency keys must never be exposed.
        string json = await response.Content.ReadAsStringAsync(
            CancellationToken);

        Assert.DoesNotContain(
            "idempotencyKey",
            json,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "pendingInitiationKey",
            json,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_WithDurableInitiation_ShouldExposeStateButNotKey()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: CancellationToken);

        DateTimeOffset createdAtUtc = DateTimeOffset
            .FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .AddMinutes(-5);

        DateTimeOffset reservedAtUtc = createdAtUtc.AddMinutes(1);

        string privateInitiationKey =
            $"private-initiation-{Guid.NewGuid():N}";

        Payment payment = Payment.Create(
            booking.Id,
            Money.Create(200m, "USD").Value,
            createdAtUtc).Value;

        payment.ReserveInitiation(
            privateInitiationKey,
            reservedAtUtc);

        await PersistPaymentAsync(payment);

        using HttpClient admin = _factory.CreateAdminClient();

        // ACT
        using HttpResponseMessage response =
            await admin.GetAsync(
                PaymentUrl(booking.PropertyId, booking.Id),
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetAdminBookingPaymentResponse body =
            Assert.IsType<GetAdminBookingPaymentResponse>(
                await response.Content
                    .ReadFromJsonAsync<GetAdminBookingPaymentResponse>(
                        CancellationToken));

        AdminPaymentDetailsResponse result =
            Assert.IsType<AdminPaymentDetailsResponse>(
                body.Payment);

        Assert.True(result.HasPendingInitiation);

        Assert.Equal(
            reservedAtUtc,
            result.PendingInitiationCreatedAtUtc);

        Assert.Empty(result.Attempts);

        // The operational key itself must remain private.
        string json = await response.Content.ReadAsStringAsync(
            CancellationToken);

        Assert.DoesNotContain(
            privateInitiationKey,
            json,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "pendingInitiationKey",
            json,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "idempotencyKey",
            json,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_ShouldEnforceOwnershipAndPropertyScope()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingPayment,
                cancellationToken: CancellationToken);

        SeedData otherProperty =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        string endpoint = PaymentUrl(
            booking.PropertyId,
            booking.Id);

        using HttpClient owner = _factory.CreateOwnerClient();

        using HttpClient differentOwner =
            _factory.CreateOwnerClient("different-owner");

        using HttpClient customer =
            _factory.CreateCustomerClient();

        using HttpClient admin = _factory.CreateAdminClient();

        // ACT
        using HttpResponseMessage ownerResponse =
            await owner.GetAsync(endpoint, CancellationToken);

        using HttpResponseMessage wrongOwnerResponse =
            await differentOwner.GetAsync(
                endpoint,
                CancellationToken);

        using HttpResponseMessage customerResponse =
            await customer.GetAsync(
                endpoint,
                CancellationToken);

        using HttpResponseMessage anonymousResponse =
            await _factory.Client.GetAsync(
                endpoint,
                CancellationToken);

        using HttpResponseMessage adminResponse =
            await admin.GetAsync(
                endpoint,
                CancellationToken);

        // Admin has property access, but the Booking belongs
        // to a different property from the one in this URL.
        using HttpResponseMessage mismatchedPropertyResponse =
            await admin.GetAsync(
                PaymentUrl(
                    otherProperty.PropertyId,
                    booking.Id),
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            ownerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            wrongOwnerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            customerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonymousResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            adminResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            mismatchedPropertyResponse.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await mismatchedPropertyResponse.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal("Booking.NotFound", problem.Code);
    }

    private async Task PersistPaymentAsync(Payment payment)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPaymentRepository paymentRepository =
            scope.ServiceProvider
                .GetRequiredService<IPaymentRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

        paymentRepository.Add(payment);

        await unitOfWork.SaveChangesAsync(CancellationToken);
    }
}
