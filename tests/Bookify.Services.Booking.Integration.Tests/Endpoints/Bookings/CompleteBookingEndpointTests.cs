using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class CompleteBookingEndpointTests
{
    private readonly BookingApiFactory _factory;

    public CompleteBookingEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WhenBookingIsPaid_ReturnsNoContentAndPersistsCompletedStatus()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            BookingStatus.Paid,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id}/complete",
            content: null,
            cancellationToken);

        // Assert - HTTP
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();

        // Assert - EF / PostgreSQL
        IBookingRepository bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        DomainBooking? persistedBooking = await bookingRepository.GetByIdAsync(booking.Id, cancellationToken);
        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.Completed, persistedBooking.Status);
        Assert.Null(persistedBooking.CancellationReason);
        Assert.True(persistedBooking.BlocksInventory);

        // Assert - Dapper
        IBookingReadService bookingReadService = scope.ServiceProvider.GetRequiredService<IBookingReadService>();
        BookingDetailsReadModel? readModel = await bookingReadService.GetByIdAsync(booking.Id, cancellationToken);
        Assert.NotNull(readModel);
        Assert.Equal(BookingStatus.Completed.ToString(), readModel.Status);
    }

    [Fact]
    public async Task Post_WhenBookingDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid bookingId = Guid.NewGuid();
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{bookingId}/complete",
            content: null,
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetailsResponse? problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Booking.NotFound", problem.Code);
    }

    [Fact]
    public async Task Post_WhenBookingIsPendingPayment_ReturnsConflict()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            BookingStatus.PendingPayment,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id}/complete",
            content: null,
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        ProblemDetailsResponse? problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Booking.InvalidStatusTransition", problem.Code);
    }

    [Fact]
    public async Task Post_WithEmptyBookingId_ReturnsBadRequest()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{Guid.Empty}/complete",
            content: null,
            cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetailsResponse? problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal("Booking.InvalidId", problem.Code);
    }
}
