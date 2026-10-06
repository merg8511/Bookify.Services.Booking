using System.Net;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Security")]
public sealed class OwnerBookingAuthorizationMatrixTests
{
    private readonly BookingApiFactory _factory;

    public OwnerBookingAuthorizationMatrixTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<
        string,
        string,
        HttpStatusCode> AccessCases
    { get; } =
        new()
        {
            {
                "approve",
                "anonymous",
                HttpStatusCode.Unauthorized
            },
            {
                "approve",
                "wrong-owner",
                HttpStatusCode.Forbidden
            },
            {
                "approve",
                "correct-owner",
                HttpStatusCode.NoContent
            },
            {
                "approve",
                "admin",
                HttpStatusCode.NoContent
            },
            {
                "approve",
                "customer",
                HttpStatusCode.Forbidden
            },
            {
                "approve",
                "invalid-guest",
                HttpStatusCode.Unauthorized
            },

            {
                "reject",
                "anonymous",
                HttpStatusCode.Unauthorized
            },
            {
                "reject",
                "wrong-owner",
                HttpStatusCode.Forbidden
            },
            {
                "reject",
                "correct-owner",
                HttpStatusCode.NoContent
            },
            {
                "reject",
                "admin",
                HttpStatusCode.NoContent
            },
            {
                "reject",
                "customer",
                HttpStatusCode.Forbidden
            },
            {
                "reject",
                "invalid-guest",
                HttpStatusCode.Unauthorized
            }
        };

    [Theory]
    [MemberData(nameof(AccessCases))]
    public async Task OwnerAction_ShouldEnforceAuthorizationMatrix(
        string action,
        string actor,
        HttpStatusCode expectedStatusCode)
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.PendingApproval,
                    ownerSubjectId:
                        TestIdentitySubjects.Owner,
                    customerSubjectId:
                        TestIdentitySubjects.Customer,
                    cancellationToken:
                        cancellationToken);

        using HttpClient client =
            CreateClient(actor);

        string endpoint =
            $"/api/v1/bookings/" +
            $"{booking.Id:D}/" +
            $"{action}";

        // Act
        using HttpResponseMessage response =
            await client.PostAsync(
                endpoint,
                content: null,
                cancellationToken);

        // Assert
        Assert.Equal(
            expectedStatusCode,
            response.StatusCode);
    }

    private HttpClient CreateClient(
        string actor)
    {
        return actor switch
        {
            "anonymous" =>
                _factory.CreateClient(),

            "wrong-owner" =>
                _factory.CreateOwnerClient(
                    $"different-owner-{Guid.NewGuid():N}"),

            "correct-owner" =>
                _factory.CreateOwnerClient(
                    TestIdentitySubjects.Owner),

            "admin" =>
                _factory.CreateAdminClient(),

            "customer" =>
                _factory.CreateCustomerClient(),

            "invalid-guest" =>
                _factory.CreateGuestClient(
                    new string('x', 43)),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(actor),
                    actor,
                    "Unsupported authorization actor.")
        };
    }
}
