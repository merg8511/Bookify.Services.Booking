
using Bookify.Services.Booking.Api.ExceptionHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Timeouts;

[Trait("Category", "Integration")]
public sealed class RequestCancellationPropagationTests
{
    [Fact]
    public async Task ClientCancellation_ShouldReachEndpointCancellationToken()
    {
        // Arrange
        CancellationToken testToken =
            TestContext.Current.CancellationToken;

        var requestStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var cancellationObserved =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        WebApplicationBuilder builder =
            WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    EnvironmentName = "Testing"
                });

        builder.WebHost.UseTestServer();

        builder.Services.AddProblemDetails();

        builder.Services.AddExceptionHandler<
            GlobalExceptionHandler>();

        await using WebApplication app = builder.Build();

        app.UseExceptionHandler();

        app.MapGet(
            "/client-cancellation",
            async (CancellationToken cancellationToken) =>
            {
                requestStarted.TrySetResult(true);

                try
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);
                }
                finally
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancellationObserved.TrySetResult(true);
                    }
                }

                return Results.Ok();
            });

        await app.StartAsync(testToken);

        using HttpClient client = app.GetTestClient();

        using var clientCancellation =
            new CancellationTokenSource();

        // Act
        Task<HttpResponseMessage> pendingRequest =
            client.GetAsync(
                "/client-cancellation",
                clientCancellation.Token);

        await requestStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            testToken);

        clientCancellation.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await pendingRequest);

        await cancellationObserved.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            testToken);
    }
}
