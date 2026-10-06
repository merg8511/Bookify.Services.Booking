using System.Net;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingLifecycleEndpointTests
{
    private readonly BookingApiFactory _factory;

    public BookingLifecycleEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApprovalPath_ShouldTransitionFromPendingApprovalToPendingPayment()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory.Services,
                status: BookingStatus.PendingApproval,
                cancellationToken: cancellationToken);

        using HttpClient ownerClient =
            _factory.CreateOwnerClient();

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.PendingApproval,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // Act
        await PostAndAssertNoContentAsync(
            ownerClient,
            $"/api/v1/bookings/{booking.Id}/approve",
            cancellationToken);

        // Assert
        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.PendingPayment,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);
    }

    [Fact]
    public async Task RejectionPath_ShouldCancelWithRejectedByOwner()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory.Services,
                status: BookingStatus.PendingApproval,
                cancellationToken: cancellationToken);

        using HttpClient ownerClient =
            _factory.CreateOwnerClient();

        // Act
        await PostAndAssertNoContentAsync(
            ownerClient,
            $"/api/v1/bookings/{booking.Id}/reject",
            cancellationToken);

        // Assert
        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Cancelled,
            BookingCancellationReason.RejectedByOwner,
            expectedBlocksInventory: false,
            cancellationToken);
    }

    [Fact]
    public async Task GuestCancellation_WhenPendingApproval_ShouldCancelWithCancelledByGuest()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                BookingStatus.PendingApproval,
                cancellationToken: cancellationToken);

        DomainBooking booking = guest.Booking;

        using HttpClient guestClient =
            _factory.CreateGuestClient(
                guest.GuestAccessToken);

        // Act
        await PostAndAssertNoContentAsync(
            guestClient,
            $"/api/v1/bookings/{booking.Id}/cancel",
            cancellationToken);

        // Assert
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
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                BookingStatus.PendingPayment,
                cancellationToken: cancellationToken);

        DomainBooking booking = guest.Booking;

        using HttpClient guestClient =
            _factory.CreateGuestClient(
                guest.GuestAccessToken);

        // Act
        await PostAndAssertNoContentAsync(
            guestClient,
            $"/api/v1/bookings/{booking.Id}/cancel",
            cancellationToken);

        // Assert
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
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                BookingStatus.Completed,
                cancellationToken: cancellationToken);

        DomainBooking booking = guest.Booking;

        using HttpClient guestClient =
            _factory.CreateGuestClient(
                guest.GuestAccessToken);

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Completed,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // Act
        using HttpResponseMessage response =
            await guestClient.PostAsync(
                $"/api/v1/bookings/{booking.Id}/cancel",
                content: null,
                cancellationToken);

        // Assert
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
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory.Services,
                status: BookingStatus.PendingApproval,
                cancellationToken: cancellationToken);

        using HttpClient ownerClient =
            _factory.CreateOwnerClient();

        await PostAndAssertNoContentAsync(
            ownerClient,
            $"/api/v1/bookings/{booking.Id}/reject",
            cancellationToken);

        // Act
        using HttpResponseMessage response =
            await ownerClient.PostAsync(
                $"/api/v1/bookings/{booking.Id}/approve",
                content: null,
                cancellationToken);

        // Assert
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

        DomainBooking? booking =
            await scope.ServiceProvider
                .GetRequiredService<IBookingRepository>()
                .GetByIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(booking);

        Assert.Equal(
            expectedStatus,
            booking.Status);

        Assert.Equal(
            expectedCancellationReason,
            booking.CancellationReason);

        Assert.Equal(
            expectedBlocksInventory,
            booking.BlocksInventory);

        BookingDetailsReadModel? readModel =
            await scope.ServiceProvider
                .GetRequiredService<IBookingReadService>()
                .GetByIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(readModel);

        Assert.Equal(
            expectedStatus.ToString(),
            readModel.Status);
    }
}
