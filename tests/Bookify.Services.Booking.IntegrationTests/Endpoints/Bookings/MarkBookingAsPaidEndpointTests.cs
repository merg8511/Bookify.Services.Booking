using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
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
public sealed class MarkBookingAsPaidEndpointTests
{
    private readonly BookingApiFactory _factory;

    public MarkBookingAsPaidEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WhenBookingIsPendingPayment_ReturnsNoContentAndPersistsPaidStatus()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                approveBeforeSaving: true,
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // ACT
        HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/mark-as-paid",
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
            BookingStatus.Paid,
            persistedBooking.Status);

        Assert.Null(
            persistedBooking.CancellationReason);

        Assert.True(
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
            BookingStatus.Paid.ToString(),
            readModel.Status);

        Assert.True(
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
                $"/api/v1/bookings/{bookingId}/mark-as-paid",
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
    public async Task Post_WhenBookingIsPendingApproval_ReturnsConflict()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                approveBeforeSaving: false,
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // ACT
        HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/mark-as-paid",
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
                $"/api/v1/bookings/{Guid.Empty}/mark-as-paid",
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
        bool approveBeforeSaving,
        CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Mark Paid Test {Guid.NewGuid():N}",
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

        if (approveBeforeSaving)
        {
            Result approvalResult =
                booking.Approve();

            Assert.True(
                approvalResult.IsSuccess);
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
