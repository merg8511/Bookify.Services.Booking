using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingLifecycleEndpointTests
{
    private readonly BookingApiFactory _factory;

    public BookingLifecycleEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HappyPath_ShouldTransitionFromPendingApprovalToCompleted()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.PendingApproval,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // ACT + ASSERT - APPROVE
        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/approve",
            cancellationToken);

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.PendingPayment,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // ACT + ASSERT - MARK AS PAID
        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/mark-as-paid",
            cancellationToken);

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Paid,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // ACT + ASSERT - COMPLETE
        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/complete",
            cancellationToken);

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Completed,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);
    }

    [Fact]
    public async Task RejectionPath_ShouldCancelWithRejectedByOwner()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // ACT
        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/reject",
            cancellationToken);

        // ASSERT
        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Cancelled,
            BookingCancellationReason.RejectedByOwner,
            expectedBlocksInventory: false,
            cancellationToken);
    }

    [Fact]
    public async Task PaymentExpirationPath_ShouldCancelWithPaymentExpired()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/approve",
            cancellationToken);

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.PendingPayment,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // ACT
        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/expire-payment",
            cancellationToken);

        // ASSERT
        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Cancelled,
            BookingCancellationReason.PaymentExpired,
            expectedBlocksInventory: false,
            cancellationToken);
    }

    [Fact]
    public async Task GuestCancellation_WhenPendingApproval_ShouldCancelWithCancelledByGuest()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // ACT
        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/cancel",
            cancellationToken);

        // ASSERT
        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Cancelled,
            BookingCancellationReason.CancelledByGuest,
            expectedBlocksInventory: false,
            cancellationToken);
    }

    [Fact]
    public async Task GuestCancellation_WhenPendingPayment_ShouldCancelWithCancelledByGuest()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/approve",
            cancellationToken);

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.PendingPayment,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // ACT
        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/cancel",
            cancellationToken);

        // ASSERT
        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Cancelled,
            BookingCancellationReason.CancelledByGuest,
            expectedBlocksInventory: false,
            cancellationToken);
    }

    [Fact]
    public async Task CompletedBooking_WhenCancelled_ShouldReturnConflictAndPreserveState()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/approve",
            cancellationToken);

        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/mark-as-paid",
            cancellationToken);

        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/complete",
            cancellationToken);

        // ACT
        using HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/cancel",
                content: null,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Completed,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);
    }

    [Fact]
    public async Task RejectedBooking_WhenApproved_ShouldReturnConflictAndPreserveCancellation()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await SeedBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/reject",
            cancellationToken);

        // ACT
        using HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/approve",
                content: null,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Cancelled,
            BookingCancellationReason.RejectedByOwner,
            expectedBlocksInventory: false,
            cancellationToken);
    }

    private async Task<DomainBooking> SeedBookingAsync(
        CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Lifecycle Test {Guid.NewGuid():N}",
                    "America/El_Salvador",
                    new TimeOnly(15, 0),
                    new TimeOnly(11, 0))
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
                    new DateOnly(2026, 9, 10),
                    new DateOnly(2026, 9, 12))
                .Value;

        DomainBooking booking =
            DomainBooking.Create(
                    rentableUnit,
                    stayPeriod,
                    GuestCount.Create(2).Value)
                .Value;

        using IServiceScope scope =
            _factory.Services.CreateScope();

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

    private static async Task PostAndAssertNoContentAsync(
        HttpClient client,
        string requestUri,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await client.PostAsync(
                requestUri,
                content: null,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);
    }

    private async Task AssertBookingStateAsync(
        Guid bookingId,
        BookingStatus expectedStatus,
        BookingCancellationReason? expectedCancellationReason,
        bool expectedBlocksInventory,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        DomainBooking? booking =
            await bookingRepository
                .GetByIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            booking);

        Assert.Equal(
            expectedStatus,
            booking.Status);

        Assert.Equal(
            expectedCancellationReason,
            booking.CancellationReason);

        Assert.Equal(
            expectedBlocksInventory,
            booking.BlocksInventory);

        IBookingReadService bookingReadService =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingReadService>();

        BookingDetailsReadModel? readModel =
            await bookingReadService
                .GetByIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            readModel);

        Assert.Equal(
            expectedStatus.ToString(),
            readModel.Status);

        Assert.Equal(
            expectedBlocksInventory,
            readModel.BlocksInventory);
    }
}
