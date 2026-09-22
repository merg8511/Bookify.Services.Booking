using Bookify.Services.Booking.Api.Endpoints.Payments.GetStatus;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Payments;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class GetPaymentStatusEndpointTests
{
    private readonly BookingApiFactory
        _factory;

    public GetPaymentStatusEndpointTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task Get_WhenBookingHasNoPayment_ShouldReturnBookingStatusAndNullPaymentStatuses()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await SeedPendingPaymentBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // Act
        HttpResponseMessage response =
            await client.GetAsync(
                BuildEndpoint(
                    bookingId),
                cancellationToken);

        // Assert
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

        Assert.Equal(
            bookingId,
            body.BookingId);

        Assert.Equal(
            "PendingPayment",
            body.BookingStatus);

        Assert.Null(
            body.PaymentStatus);

        Assert.Null(
            body.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Get_WhenPaymentHasPendingAttempt_ShouldReturnPendingStatuses()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (
            Guid bookingId,
            Payment payment,
            PaymentAttempt attempt) =
                await SeedPaymentWithAttemptAsync(
                    cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // Act
        HttpResponseMessage response =
            await client.GetAsync(
                BuildEndpoint(
                    bookingId),
                cancellationToken);

        // Assert
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

        Assert.Equal(
            bookingId,
            body.BookingId);

        Assert.Equal(
            "PendingPayment",
            body.BookingStatus);

        Assert.Equal(
            payment.Status.ToString(),
            body.PaymentStatus);

        Assert.Equal(
            attempt.Status.ToString(),
            body.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Get_WhenPaymentWasRetried_ShouldReturnLatestAttemptStatus()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await SeedRetriedPaymentAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // Act
        HttpResponseMessage response =
            await client.GetAsync(
                BuildEndpoint(
                    bookingId),
                cancellationToken);

        // Assert
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

        Assert.Equal(
            "PendingPayment",
            body.BookingStatus);

        Assert.Equal(
            "Pending",
            body.PaymentStatus);

        Assert.Equal(
            "Pending",
            body.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Get_WhenPaymentSucceeded_ShouldReturnPaidAndSucceededStatuses()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await SeedSucceededPaymentAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // Act
        HttpResponseMessage response =
            await client.GetAsync(
                BuildEndpoint(
                    bookingId),
                cancellationToken);

        // Assert
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

        Assert.Equal(
            "Paid",
            body.BookingStatus);

        Assert.Equal(
            "Succeeded",
            body.PaymentStatus);

        Assert.Equal(
            "Succeeded",
            body.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Get_WhenBookingDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        Guid bookingId =
            Guid.NewGuid();

        HttpClient client =
            _factory.CreateClient();

        // Act
        HttpResponseMessage response =
            await client.GetAsync(
                BuildEndpoint(
                    bookingId),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        TestContext.Current.CancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Booking.NotFound",
            problem.Code);
    }

    [Fact]
    public async Task Get_WhenBookingIdIsEmpty_ShouldReturnBadRequest()
    {
        // Arrange
        HttpClient client =
            _factory.CreateClient();

        // Act
        HttpResponseMessage response =
            await client.GetAsync(
                BuildEndpoint(
                    Guid.Empty),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        TestContext.Current.CancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Booking.InvalidId",
            problem.Code);
    }

    private async Task<Guid>
        SeedPendingPaymentBookingAsync(
            CancellationToken cancellationToken)
    {
        (
            Property property,
            RentableUnit rentableUnit,
            DomainBooking booking) =
                CreatePendingPaymentBooking();

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        bookingRepository.Add(
            booking);

        await unitOfWork
            .SaveChangesAsync(
                cancellationToken);

        return booking.Id;
    }

    private async Task<(
        Guid BookingId,
        Payment Payment,
        PaymentAttempt Attempt)>
        SeedPaymentWithAttemptAsync(
            CancellationToken cancellationToken)
    {
        (
            Property property,
            RentableUnit rentableUnit,
            DomainBooking booking) =
                CreatePendingPaymentBooking();

        DateTimeOffset paymentCreatedAtUtc =
            DateTimeOffset.UtcNow
                .AddMinutes(-2);

        Payment payment =
            Payment.Create(
                booking.Id,
                Money.Create(
                    200m,
                    "USD")
                .Value,
                paymentCreatedAtUtc)
            .Value;

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                $"status-{Guid.NewGuid():N}",
                $"pi_{Guid.NewGuid():N}",
                paymentCreatedAtUtc
                    .AddMinutes(1));

        Assert.True(
            attemptResult.IsSuccess);

        await PersistAsync(
            property,
            rentableUnit,
            booking,
            payment,
            cancellationToken);

        return (
            booking.Id,
            payment,
            attemptResult.Value);
    }

    private async Task<Guid>
        SeedRetriedPaymentAsync(
            CancellationToken cancellationToken)
    {
        (
            Property property,
            RentableUnit rentableUnit,
            DomainBooking booking) =
                CreatePendingPaymentBooking();

        DateTimeOffset paymentCreatedAtUtc =
            DateTimeOffset.UtcNow
                .AddMinutes(-5);

        Payment payment =
            Payment.Create(
                booking.Id,
                Money.Create(
                    200m,
                    "USD")
                .Value,
                paymentCreatedAtUtc)
            .Value;

        Result<PaymentAttempt> firstAttemptResult =
            payment.AddAttempt(
                $"status-first-{Guid.NewGuid():N}",
                $"pi_{Guid.NewGuid():N}",
                paymentCreatedAtUtc
                    .AddMinutes(1));

        Assert.True(
            firstAttemptResult.IsSuccess);

        Result failedResult =
            payment.MarkAttemptAsFailed(
                firstAttemptResult.Value
                    .ExternalReference,
                paymentCreatedAtUtc
                    .AddMinutes(2));

        Assert.True(
            failedResult.IsSuccess);

        Result<PaymentAttempt> retryResult =
            payment.AddAttempt(
                $"status-retry-{Guid.NewGuid():N}",
                $"pi_{Guid.NewGuid():N}",
                paymentCreatedAtUtc
                    .AddMinutes(3));

        Assert.True(
            retryResult.IsSuccess);

        await PersistAsync(
            property,
            rentableUnit,
            booking,
            payment,
            cancellationToken);

        return booking.Id;
    }

    private async Task<Guid>
        SeedSucceededPaymentAsync(
            CancellationToken cancellationToken)
    {
        (
            Property property,
            RentableUnit rentableUnit,
            DomainBooking booking) =
                CreatePendingPaymentBooking();

        DateTimeOffset paymentCreatedAtUtc =
            DateTimeOffset.UtcNow
                .AddMinutes(-3);

        Payment payment =
            Payment.Create(
                booking.Id,
                Money.Create(
                    200m,
                    "USD")
                .Value,
                paymentCreatedAtUtc)
            .Value;

        Result<PaymentAttempt> attemptResult =
            payment.AddAttempt(
                $"status-success-{Guid.NewGuid():N}",
                $"pi_{Guid.NewGuid():N}",
                paymentCreatedAtUtc
                    .AddMinutes(1));

        Assert.True(
            attemptResult.IsSuccess);

        Result paymentSucceededResult =
            payment.MarkAttemptAsSucceeded(
                attemptResult.Value
                    .ExternalReference,
                paymentCreatedAtUtc
                    .AddMinutes(2));

        Assert.True(
            paymentSucceededResult.IsSuccess);

        Result bookingPaidResult =
            booking.MarkAsPaid(BookingTestTime.PaidAtUtc);

        Assert.True(
            bookingPaidResult.IsSuccess);

        await PersistAsync(
            property,
            rentableUnit,
            booking,
            payment,
            cancellationToken);

        return booking.Id;
    }

    private async Task PersistAsync(
        Property property,
        RentableUnit rentableUnit,
        DomainBooking booking,
        Payment payment,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IPaymentRepository paymentRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPaymentRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        bookingRepository.Add(
            booking);

        paymentRepository.Add(
            payment);

        await unitOfWork
            .SaveChangesAsync(
                cancellationToken);
    }

    private static (
        Property Property,
        RentableUnit RentableUnit,
        DomainBooking Booking)
        CreatePendingPaymentBooking()
    {
        Property property =
            Property.Create(
                $"Payment Status {Guid.NewGuid():N}",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0))
            .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                property.Id,
                $"Status Room {Guid.NewGuid():N}",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;

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

        DomainBooking booking =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(
                    2)
                .Value,
                GuestDetails.Create(
                "John Doe",
                "john@example.com",
                "+50377778888").Value,
                BookingTestTime.CreatedAtUtc)
            .Value;

        Assert.True(
            booking.Approve(BookingTestTime.ApprovedAtUtc).IsSuccess);

        return (
            property,
            rentableUnit,
            booking);
    }

    private static string BuildEndpoint(
        Guid bookingId)
    {
        return
            $"/api/v1/payments/bookings/" +
            $"{bookingId:D}/status";
    }
}
