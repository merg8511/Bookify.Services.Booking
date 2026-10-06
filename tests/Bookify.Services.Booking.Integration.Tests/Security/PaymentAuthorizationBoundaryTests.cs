using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PaymentAuthorizationBoundaryTests
{
    private readonly BookingApiFactory _factory;

    public PaymentAuthorizationBoundaryTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InitiatePayment_WithoutBookingAccess_ShouldNotTouchIdempotencyStore()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                BookingStatus.PendingPayment,
                customerSubjectId: TestIdentitySubjects.Customer,
                cancellationToken: cancellationToken);

        string key = $"unauthorized-payment-{Guid.NewGuid():N}";

        using HttpClient client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments");

        request.Headers.Add("Idempotency-Key", key);

        request.Content = JsonContent.Create(new InitiatePaymentRequest(booking.Id));

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();

        IDbConnectionFactory connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        long count =
            await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    """
                    SELECT COUNT(*)
                    FROM idempotency_requests
                    WHERE key = @Key;
                    """,
                    new
                    {
                        Key = key
                    },
                    cancellationToken: cancellationToken));

        Assert.Equal(0, count);
    }
}
