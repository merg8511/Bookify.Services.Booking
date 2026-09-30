using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
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
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.PendingApproval,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // ACT
        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/approve",
            cancellationToken);

        // ASSERT
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
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.Completed,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

        await AssertBookingStateAsync(
            booking.Id,
            BookingStatus.Completed,
            expectedCancellationReason: null,
            expectedBlocksInventory: true,
            cancellationToken);

        // ACT
        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id}/cancel",
            content: null,
            cancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

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
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            status: BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

        await PostAndAssertNoContentAsync(
            client,
            $"/api/v1/bookings/{booking.Id}/reject",
            cancellationToken);

        // ACT
        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id}/approve",
            content: null,
            cancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

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
        using HttpResponseMessage response = await client.PostAsync(
            requestUri,
            content: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task AssertBookingStateAsync(
        Guid bookingId,
        BookingStatus expectedStatus,
        BookingCancellationReason? expectedCancellationReason,
        bool expectedBlocksInventory,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        DomainBooking? booking = await scope.ServiceProvider
            .GetRequiredService<IBookingRepository>()
            .GetByIdAsync(bookingId, cancellationToken);

        Assert.NotNull(booking);
        Assert.Equal(expectedStatus, booking.Status);
        Assert.Equal(expectedCancellationReason, booking.CancellationReason);
        Assert.Equal(expectedBlocksInventory, booking.BlocksInventory);

        BookingDetailsReadModel? readModel = await scope.ServiceProvider
            .GetRequiredService<IBookingReadService>()
            .GetByIdAsync(bookingId, cancellationToken);

        Assert.NotNull(readModel);
        Assert.Equal(expectedStatus.ToString(), readModel.Status);
    }
}
