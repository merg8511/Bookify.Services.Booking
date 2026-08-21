using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class ExpireBookingPaymentEndpointTests
{
    private readonly BookingApiFactory _factory;

    public ExpireBookingPaymentEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WhenBookingIsPendingPayment_ReturnsNoContentAndPersistsPaymentExpiration()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                markAsPaidBeforeSaving: false,
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // ACT
        HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/expire-payment",
                content: null,
                cancellationToken);

        // ASSERT - HTTP
        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        // ASSERT - EF / PostgreSQL
        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        DomainBooking? persistedBooking =
            await bookingRepository
                .GetByIdAsync(
                    booking.Id,
                    cancellationToken);

        Assert.NotNull(
            persistedBooking);

        Assert.Equal(
            BookingStatus.Cancelled,
            persistedBooking.Status);

        Assert.Equal(
            BookingCancellationReason.PaymentExpired,
            persistedBooking.CancellationReason);

        Assert.False(
            persistedBooking.BlocksInventory);

        // ASSERT - Dapper
        IBookingReadService bookingReadService =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingReadService>();

        BookingDetailsReadModel? readModel =
            await bookingReadService
                .GetByIdAsync(
                    booking.Id,
                    cancellationToken);

        Assert.NotNull(
            readModel);

        Assert.Equal(
            BookingStatus.Cancelled.ToString(),
            readModel.Status);

        Assert.False(
            readModel.BlocksInventory);
    }

    [Fact]
    public async Task Post_WhenBookingDoesNotExist_ReturnsNotFound()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        HttpClient client =
            _factory.CreateClient();

        // ACT
        HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{bookingId}/expire-payment",
                content: null,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        cancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Booking.NotFound",
            problem.Code);
    }

    [Fact]
    public async Task Post_WhenBookingIsPaid_ReturnsConflict()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                markAsPaidBeforeSaving: true,
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // ACT
        HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/expire-payment",
                content: null,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        cancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Booking.InvalidStatusTransition",
            problem.Code);
    }

    [Fact]
    public async Task Post_WithEmptyBookingId_ReturnsBadRequest()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        HttpClient client =
            _factory.CreateClient();

        // ACT
        HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{Guid.Empty}/expire-payment",
                content: null,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse? problem =
            await response.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        cancellationToken);

        Assert.NotNull(
            problem);

        Assert.Equal(
            "Booking.InvalidId",
            problem.Code);
    }

    private async Task<DomainBooking> SeedBookingAsync(
        bool markAsPaidBeforeSaving,
        CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Expire Payment Test {Guid.NewGuid():N}",
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
                    "Room A",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        9,
                        10),
                    new DateOnly(
                        2026,
                        9,
                        12))
                .Value;

        DomainBooking booking =
            DomainBooking.Create(
                    rentableUnit,
                    stayPeriod,
                    GuestCount.Create(2).Value)
                .Value;

        Result approvalResult =
            booking.Approve();

        Assert.True(
            approvalResult.IsSuccess);

        if (markAsPaidBeforeSaving)
        {
            Result paidResult =
                booking.MarkAsPaid();

            Assert.True(
                paidResult.IsSuccess);
        }

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

        return booking;
    }
}
