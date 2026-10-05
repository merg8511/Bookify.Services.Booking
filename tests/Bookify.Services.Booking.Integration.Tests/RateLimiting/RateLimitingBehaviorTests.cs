using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bookify.Services.Booking.Integration.Tests.RateLimiting;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class RateLimitingBehaviorTests
{
    private readonly BookingApiFactory _factory;

    public RateLimitingBehaviorTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PublicRead_WhenLimitIsExceeded_ShouldReturn429WithRetryAfter()
    {
        // Arrange
        using WebApplicationFactory<Program> limitedFactory =
            CreateLimitedFactory(
                permitLimit: 2);

        using HttpClient firstUserClient =
            CreateAuthenticatedClient(
                limitedFactory,
                $"rate-limit-user-{Guid.NewGuid():N}");

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Act
        using HttpResponseMessage first =
            await firstUserClient.GetAsync(
                "/api/v1/properties",
                cancellationToken);

        using HttpResponseMessage second =
            await firstUserClient.GetAsync(
                "/api/v1/properties",
                cancellationToken);

        using HttpResponseMessage rejected =
            await firstUserClient.GetAsync(
                "/api/v1/properties",
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            second.StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejected.StatusCode);

        Assert.True(
            rejected.Headers.Contains(
                "Retry-After"));

        Assert.Equal(
            "application/problem+json",
            rejected.Content.Headers
                .ContentType?
                .MediaType);

        ProblemDetailsResponse? problem =
            await rejected.Content
                .ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                        cancellationToken);

        Assert.NotNull(problem);

        Assert.Equal(
            "RateLimit.Exceeded",
            problem.Code);
    }

    [Fact]
    public async Task PublicRead_DifferentAuthenticatedSubjects_ShouldUseIndependentPartitions()
    {
        // Arrange
        using WebApplicationFactory<Program> limitedFactory =
            CreateLimitedFactory(
                permitLimit: 1);

        using HttpClient firstUser =
            CreateAuthenticatedClient(
                limitedFactory,
                $"rate-limit-a-{Guid.NewGuid():N}");

        using HttpClient secondUser =
            CreateAuthenticatedClient(
                limitedFactory,
                $"rate-limit-b-{Guid.NewGuid():N}");

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Act
        using HttpResponseMessage firstUserFirstRequest =
            await firstUser.GetAsync(
                "/api/v1/properties",
                cancellationToken);

        using HttpResponseMessage firstUserRejected =
            await firstUser.GetAsync(
                "/api/v1/properties",
                cancellationToken);

        using HttpResponseMessage secondUserFirstRequest =
            await secondUser.GetAsync(
                "/api/v1/properties",
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            firstUserFirstRequest.StatusCode);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            firstUserRejected.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            secondUserFirstRequest.StatusCode);
    }

    private WebApplicationFactory<Program>
        CreateLimitedFactory(
            int permitLimit)
    {
        return _factory.WithWebHostBuilder(
            builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:Database",
                    _factory.DatabaseConnectionString);

                builder.UseSetting(
                    "RateLimiting:PublicReads:PermitLimit",
                    permitLimit.ToString(
                        System.Globalization
                            .CultureInfo.InvariantCulture));

                builder.UseSetting(
                    "RateLimiting:PublicReads:Window",
                    "00:01:00");
            });
    }

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        string subject)
    {
        HttpClient client =
            factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    BaseAddress =
                        new Uri("http://localhost")
                });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                JwtBearerDefaults.AuthenticationScheme,
                TestIdentityTokens.Create(
                    subject,
                    BookifyRoles.Customer));

        return client;
    }
}
