using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Stripe;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class StripeSdkOutboundTraceTests
{
    [Fact]
    public async Task RealStripeSdkTransport_ShouldPropagateW3CTraceparent()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using ServiceProvider provider =
            CreateTracingProvider();

        provider.GetRequiredService<TracerProvider>();

        await using var server =
            new StripeLoopbackServer();

        using var httpClient =
            new HttpClient();

        var stripeClient =
            new StripeClient(
                apiKey:
                    "sk_test_bookify_observability_only",
                httpClient:
                    new SystemNetHttpClient(httpClient),
                apiBase:
                    server.ApiBase);

        var gateway =
            new StripePaymentGateway(
                new PaymentIntentService(
                    stripeClient));

        using var parent =
            new Activity(
                "bookify.payment.request")
                .SetIdFormat(
                    ActivityIdFormat.W3C)
                .Start();

        Task<CapturedRequest> requestTask =
            server.AcceptRequestAsync(
                cancellationToken);

        Result<PaymentGatewayResponse> result =
            await gateway.GetPaymentStatusAsync(
                "pi_test_123",
                cancellationToken);

        CapturedRequest request =
            await requestTask;

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentGatewayStatus.Succeeded,
            result.Value.Status);

        Assert.Equal(
            "GET",
            request.Method);

        Assert.Equal(
            "/v1/payment_intents/pi_test_123",
            request.Path);

        Assert.True(
            request.Headers.TryGetValue(
                "traceparent",
                out string? traceparent));

        Assert.False(
            string.IsNullOrWhiteSpace(
                traceparent));

        string[] parts =
            traceparent.Split('-');

        Assert.Equal(
            4,
            parts.Length);

        Assert.Equal(
            "00",
            parts[0]);

        Assert.Equal(
            parent.TraceId.ToString(),
            parts[1]);

        Assert.Equal(
            16,
            parts[2].Length);

        Assert.NotEqual(
            "0000000000000000",
            parts[2]);
    }

    private static ServiceProvider CreateTracingProvider()
    {
        var services =
            new ServiceCollection();

        services.AddLogging();

        services
            .AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .AddHttpClientInstrumentation()
                    .SetSampler(
                        new AlwaysOnSampler());
            });

        return services.BuildServiceProvider();
    }

    private sealed record CapturedRequest(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers);

    private sealed class StripeLoopbackServer :
        IAsyncDisposable
    {
        private readonly TcpListener _listener;

        public StripeLoopbackServer()
        {
            _listener =
                new TcpListener(
                    IPAddress.Loopback,
                    0);

            _listener.Start();

            var endpoint =
                (IPEndPoint)_listener.LocalEndpoint;

            ApiBase =
                $"http://127.0.0.1:{endpoint.Port}";
        }

        public string ApiBase { get; }

        public async Task<CapturedRequest>
            AcceptRequestAsync(
                CancellationToken cancellationToken)
        {
            using TcpClient connection =
                await _listener.AcceptTcpClientAsync(
                    cancellationToken);

            await using NetworkStream stream =
                connection.GetStream();

            using var reader =
                new StreamReader(
                    stream,
                    Encoding.ASCII,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);

            string? requestLine =
                await reader.ReadLineAsync(
                    cancellationToken);

            Assert.False(
                string.IsNullOrWhiteSpace(
                    requestLine));

            string[] requestParts =
                requestLine.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            Assert.True(
                requestParts.Length >= 2);

            var headers =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                string? line =
                    await reader.ReadLineAsync(
                        cancellationToken);

                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                int separatorIndex =
                    line.IndexOf(
                        ':',
                        StringComparison.Ordinal);

                if (separatorIndex <= 0)
                {
                    continue;
                }

                string name =
                    line[..separatorIndex]
                        .Trim();

                string value =
                    line[(separatorIndex + 1)..]
                        .Trim();

                headers[name] =
                    value;
            }

            const string json =
                """
                {"id":"pi_test_123","object":"payment_intent","status":"succeeded"}
                """;

            byte[] body =
                Encoding.UTF8.GetBytes(
                    json);

            string responseHeaders =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n" +
                "\r\n";

            byte[] headerBytes =
                Encoding.ASCII.GetBytes(
                    responseHeaders);

            await stream.WriteAsync(
                headerBytes,
                cancellationToken);

            await stream.WriteAsync(
                body,
                cancellationToken);

            await stream.FlushAsync(
                cancellationToken);

            return new CapturedRequest(
                requestParts[0],
                requestParts[1],
                headers);
        }

        public ValueTask DisposeAsync()
        {
            _listener.Stop();

            return ValueTask.CompletedTask;
        }
    }
}
