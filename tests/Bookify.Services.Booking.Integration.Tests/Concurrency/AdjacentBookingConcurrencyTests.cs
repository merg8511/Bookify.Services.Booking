using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Concurrency;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Concurrency")]
public sealed class AdjacentBookingConcurrencyTests
{
    private const string Endpoint = "/api/v1/bookings";

    private readonly BookingApiFactory _factory;

    public AdjacentBookingConcurrencyTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentBookings_WithAdjacentDates_ShouldBothBeCreated()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        SeedData property =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: cancellationToken);

        CreateBookingRequest firstRequest =
            BookingRequestTestFactory.CreateBookingRequest(
                propertyId: property.PropertyId,
                rentableUnitId: property.RentableUnitId,
                checkInDate: new DateOnly(2027, 1, 10),
                checkOutDate: new DateOnly(2027, 1, 12));

        CreateBookingRequest secondRequest =
            BookingRequestTestFactory.CreateBookingRequest(
                propertyId: property.PropertyId,
                rentableUnitId: property.RentableUnitId,
                checkInDate: new DateOnly(2027, 1, 12),
                checkOutDate: new DateOnly(2027, 1, 14));

        var startGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Las dos solicitudes esperan la misma señal.
        Task<HttpResponseMessage> firstTask =
            SendAfterGateAsync(
                firstRequest,
                startGate.Task,
                cancellationToken);

        Task<HttpResponseMessage> secondTask =
            SendAfterGateAsync(
                secondRequest,
                startGate.Task,
                cancellationToken);

        // ACT
        startGate.SetResult(true);

        using HttpResponseMessage firstResponse =
            await firstTask;

        using HttpResponseMessage secondResponse =
            await secondTask;

        // ASSERT — HTTP
        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            secondResponse.StatusCode);

        CreateBookingResponse? firstBooking =
            await firstResponse.Content
                .ReadFromJsonAsync<CreateBookingResponse>(
                    cancellationToken);

        CreateBookingResponse? secondBooking =
            await secondResponse.Content
                .ReadFromJsonAsync<CreateBookingResponse>(
                    cancellationToken);

        Assert.NotNull(firstBooking);
        Assert.NotNull(secondBooking);

        Assert.NotEqual(firstBooking.Id, secondBooking.Id);

        // ASSERT — PostgreSQL
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBookingRepository repository =
            scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        var persistedFirst =
            await repository.GetByIdAsync(
                firstBooking.Id,
                cancellationToken);

        var persistedSecond =
            await repository.GetByIdAsync(
                secondBooking.Id,
                cancellationToken);

        Assert.NotNull(persistedFirst);
        Assert.NotNull(persistedSecond);

        Assert.Equal(
            BookingStatus.PendingApproval,
            persistedFirst.Status);

        Assert.Equal(
            BookingStatus.PendingApproval,
            persistedSecond.Status);

        Assert.Equal(
            property.RentableUnitId,
            persistedFirst.RentableUnitId);

        Assert.Equal(
            property.RentableUnitId,
            persistedSecond.RentableUnitId);
    }

    private async Task<HttpResponseMessage> SendAfterGateAsync(
        CreateBookingRequest bookingRequest,
        Task startGate,
        CancellationToken cancellationToken)
    {
        await startGate.WaitAsync(cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            Endpoint);

        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            $"adjacent-booking-{Guid.NewGuid():N}");

        request.Content = JsonContent.Create(bookingRequest);

        return await _factory.Client.SendAsync(
            request,
            cancellationToken);
    }
}
