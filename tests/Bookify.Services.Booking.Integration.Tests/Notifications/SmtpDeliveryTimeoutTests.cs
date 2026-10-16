
using System.Net;
using System.Net.Sockets;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Notifications;

[Trait("Category", "Integration")]
public sealed class SmtpDeliveryTimeoutTests
{
    [Fact]
    public async Task SilentSmtpServer_ShouldReturnDeliveryFailureAfterTimeout()
    {
        CancellationToken testToken =
            TestContext.Current.CancellationToken;

        using var listener =
            new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        int port =
            ((IPEndPoint)listener.LocalEndpoint).Port;

        IConfiguration configuration =
            CreateConfiguration(port, timeoutMilliseconds: 250);

        var collection = new ServiceCollection();

        collection.AddLogging();
        collection.AddNotifications(configuration);

        using ServiceProvider services =
            collection.BuildServiceProvider();

        IBookingNotificationSender sender =
            services.GetRequiredService<
                IBookingNotificationSender>();

        Guid bookingId = Guid.NewGuid();

        var message = CreateMessage(bookingId);

        Task<TcpClient> acceptedConnection =
            listener.AcceptTcpClientAsync(testToken).AsTask();

        // The server intentionally does not send a greeting.
        Result result = await sender.SendAsync(
            message,
            testToken);

        using TcpClient acceptedClient =
            await acceptedConnection.WaitAsync(
                TimeSpan.FromSeconds(5),
                testToken);

        Assert.True(acceptedClient.Connected);

        Assert.True(result.IsFailure);

        Assert.Equal(
            BookingNotificationErrors.DeliveryFailed(
                bookingId),
            result.Error);
    }

    [Fact]
    public async Task CallerCancellation_ShouldPropagateWithoutReturningDeliveryFailure()
    {
        CancellationToken testToken =
            TestContext.Current.CancellationToken;

        using var listener =
            new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        int port =
            ((IPEndPoint)listener.LocalEndpoint).Port;

        IConfiguration configuration =
            CreateConfiguration(
                port,
                timeoutMilliseconds: 5000);

        var collection = new ServiceCollection();

        collection.AddLogging();
        collection.AddNotifications(configuration);

        using ServiceProvider services =
            collection.BuildServiceProvider();

        IBookingNotificationSender sender =
            services.GetRequiredService<
                IBookingNotificationSender>();

        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                testToken);

        var message = CreateMessage(Guid.NewGuid());

        Task<TcpClient> acceptedConnection =
            listener.AcceptTcpClientAsync(testToken).AsTask();

        Task<Result> sending =
            sender.SendAsync(
                message,
                cancellation.Token);

        using TcpClient acceptedClient =
            await acceptedConnection.WaitAsync(
                TimeSpan.FromSeconds(5),
                testToken);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await sending);
    }

    private static IConfiguration CreateConfiguration(
        int port,
        int timeoutMilliseconds)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Notifications:Smtp:Enabled"] = "true",
                    ["Notifications:Smtp:Host"] = "127.0.0.1",
                    ["Notifications:Smtp:Port"] = port.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ["Notifications:Smtp:Security"] = "None",
                    ["Notifications:Smtp:FromName"] = "Bookify",
                    ["Notifications:Smtp:FromAddress"] =
                        "no-reply@bookify.test",
                    ["Notifications:Smtp:TimeoutMilliseconds"] =
                        timeoutMilliseconds.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)
                })
            .Build();
    }

    private static BookingNotificationMessage CreateMessage(
        Guid bookingId)
    {
        return new BookingNotificationMessage(
            bookingId,
            BookingNotificationType.BookingApproved,
            "guest@example.com",
            "Booking notification",
            "Test email body.");
    }
}
