using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Observability;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingCreationMetricsFlowTests
{
    private readonly BookingApiFactory _factory;

    public BookingCreationMetricsFlowTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CommittedBooking_ShouldIncrementOnce_ButReplayAndRejectedBookingShouldNot()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var values = new ConcurrentQueue<long>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == BookingMetrics.MeterName &&
                    instrument.Name == "bookify.bookings.created")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, count, _, _) => values.Enqueue(count));
        listener.Start();

        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
            _factory, cancellationToken: token);

        var valid = BookingRequestTestFactory.CreateBookingRequest(
            data.PropertyId, data.RentableUnitId,
            BookingTestData.CreateDate(10),
            BookingTestData.CreateDate(15),
            guestCount: 2);

        using HttpClient client = _factory.CreateClient();
        string idempotencyKey = $"19-9-{Guid.NewGuid():N}";

        using HttpResponseMessage created = await PostAsync(client, valid, idempotencyKey, token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(1L, values.Sum());

        // Same key and payload: HTTP idempotency must not re-run the command.
        using HttpResponseMessage replay = await PostAsync(client, valid, idempotencyKey, token);
        Assert.True(replay.IsSuccessStatusCode);
        Assert.Equal(1L, values.Sum());

        // New request, invalid guest count: should fail validation and not commit.
        var invalid = BookingRequestTestFactory.CreateBookingRequest(
            data.PropertyId, data.RentableUnitId,
            BookingTestData.CreateDate(10),
            BookingTestData.CreateDate(15),
            guestCount: 0);

        using HttpResponseMessage rejected = await PostAsync(
            client, invalid, $"19-9-{Guid.NewGuid():N}", token);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(1L, values.Sum());
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        CreateBookingRequest request,
        string idempotencyKey,
        CancellationToken token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings");
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        message.Content = JsonContent.Create(request);
        return await client.SendAsync(message, token);
    }
}
