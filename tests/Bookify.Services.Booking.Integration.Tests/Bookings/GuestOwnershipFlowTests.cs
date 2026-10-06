using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bookify.Services.Booking.Integration.Tests.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class GuestOwnershipFlowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Endpoint = "/api/v1/bookings";
    private const string GuestTokenHeader = "Booking-Guest-Token";

    private readonly BookingApiFactory _factory;

    public GuestOwnershipFlowTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GuestBooking_ShouldReturnTokenOnceAndPersistOnlyHash()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeedData seed = await SeedPropertyAsync(cancellationToken);

        var request = CreateRequest(seed);
        string key = Guid.NewGuid().ToString("N");

        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage first = await SendAsync(client, request, key, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        string token = Assert.Single(first.Headers.GetValues(GuestTokenHeader));
        Assert.Equal(43, token.Length);

        string firstBody = await first.Content.ReadAsStringAsync(cancellationToken);
        CreateBookingResponse created = JsonSerializer.Deserialize<CreateBookingResponse>(firstBody, JsonOptions)
            ?? throw new InvalidOperationException("The create booking response could not be deserialized.");

        Assert.DoesNotContain(token, firstBody, StringComparison.Ordinal);

        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var persisted = await dbContext.Bookings.AsNoTracking()
            .SingleAsync(booking => booking.Id == created.Id, cancellationToken);

        Assert.Null(persisted.CustomerSubjectId);
        Assert.NotNull(persisted.GuestAccessTokenHash);
        Assert.Equal(64, persisted.GuestAccessTokenHash.Length);
        Assert.NotEqual(token, persisted.GuestAccessTokenHash);
        Assert.True(persisted.VerifyGuestAccessToken(token));
        Assert.False(persisted.VerifyGuestAccessToken(new string('x', 43)));

        IDbConnectionFactory connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        string storedBody = await connection.QuerySingleAsync<string>(
            new CommandDefinition(
                """
                SELECT response_body
                FROM idempotency_requests
                WHERE key = @Key;
                """,
                new { Key = key },
                cancellationToken: cancellationToken));

        Assert.Equal(firstBody, storedBody);
        Assert.DoesNotContain(token, storedBody, StringComparison.Ordinal);

        using HttpResponseMessage retry = await SendAsync(client, request, key, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.False(retry.Headers.Contains(GuestTokenHeader));

        string replayedBody = await retry.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal(firstBody, replayedBody);

        Assert.Equal(
            1,
            await dbContext.Bookings.CountAsync(
                booking => booking.PropertyId == seed.PropertyId,
                cancellationToken));
    }

    [Fact]
    public async Task AuthenticatedBooking_ShouldPersistCustomerWithoutGuestToken()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeedData seed = await SeedPropertyAsync(cancellationToken);
        string subject = $"customer-{Guid.NewGuid():N}";

        using HttpClient client = _factory.CreateAuthenticatedClient(subject, BookifyRoles.Customer);

        using HttpResponseMessage response = await SendAsync(
            client,
            CreateRequest(seed),
            Guid.NewGuid().ToString("N"),
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(response.Headers.Contains(GuestTokenHeader));

        CreateBookingResponse created = Assert.IsType<CreateBookingResponse>(
            await response.Content.ReadFromJsonAsync<CreateBookingResponse>(cancellationToken));

        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var booking = await dbContext.Bookings.AsNoTracking()
            .SingleAsync(item => item.Id == created.Id, cancellationToken);

        Assert.Equal(subject, booking.CustomerSubjectId);
        Assert.Null(booking.GuestAccessTokenHash);
        Assert.False(booking.VerifyGuestAccessToken("anything"));
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        CreateBookingRequest request,
        string key,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        message.Headers.Add("Idempotency-Key", key);
        message.Content = JsonContent.Create(request);

        return await client.SendAsync(message, cancellationToken);
    }

    private async Task<SeedData> SeedPropertyAsync(CancellationToken cancellationToken)
    {
        return await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
            _factory.Services,
            propertyName: $"Guest Ownership Test {Guid.NewGuid():N}",
            cancellationToken: cancellationToken);
    }

    private static CreateBookingRequest CreateRequest(SeedData data)
    {
        return BookingRequestTestFactory.CreateBookingRequest(
            propertyId: data.PropertyId,
            rentableUnitId: data.RentableUnitId,
            checkInDate: new DateOnly(2026, 10, 10),
            checkOutDate: new DateOnly(2026, 10, 12),
            guestCount: 2);
    }
}
