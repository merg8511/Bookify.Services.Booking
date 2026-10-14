using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class HttpClientTracePropagationTests
{
    private const string TestSourceName =
        "Bookify.Tests.OutboundPropagation";

    [Fact]
    public async Task HttpClient_ShouldPropagateW3CTraceContext()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using ServiceProvider services =
            CreateTracingProvider();

        using var source =
            new ActivitySource(TestSourceName);

        using Activity? parent =
            source.StartActivity(
                "outbound.test",
                ActivityKind.Internal);

        Assert.NotNull(parent);

        await using var server =
            new LoopbackHttpServer();

        Task<IReadOnlyDictionary<string, string>> requestTask =
            server.AcceptRequestAsync(
                cancellationToken);

        using var client =
            new HttpClient();

        using HttpResponseMessage response =
            await client.GetAsync(
                server.Uri,
                cancellationToken);

        IReadOnlyDictionary<string, string> headers =
            await requestTask;

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.True(
            headers.TryGetValue(
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

        Assert.Equal(
            2,
            parts[3].Length);

        Assert.NotEqual(
            "0000000000000000",
            parts[2]);

        Assert.False(
            headers.ContainsKey(
                "Authorization"));
    }

    private static ServiceProvider CreateTracingProvider()
    {
        var services =
            new ServiceCollection();

        services.AddLogging();

        services
            .AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(TestSourceName)
                    .AddHttpClientInstrumentation()
                    .SetSampler(
                        new AlwaysOnSampler());
            });

        ServiceProvider provider =
            services.BuildServiceProvider();

        provider.GetRequiredService<
            TracerProvider>();

        return provider;
    }

    private sealed class LoopbackHttpServer :
        IAsyncDisposable
    {
        private readonly TcpListener _listener;

        public LoopbackHttpServer()
        {
            _listener =
                new TcpListener(
                    IPAddress.Loopback,
                    0);

            _listener.Start();

            var endpoint =
                (IPEndPoint)_listener.LocalEndpoint;

            Uri =
                new Uri(
                    $"http://127.0.0.1:{endpoint.Port}/health");
        }

        public Uri Uri { get; }

        public async Task<IReadOnlyDictionary<string, string>>
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

            var headers =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            string? requestLine =
                await reader.ReadLineAsync(
                    cancellationToken);

            Assert.False(
                string.IsNullOrWhiteSpace(
                    requestLine));

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

            byte[] response =
                Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Length: 0\r\n" +
                    "Connection: close\r\n" +
                    "\r\n");

            await stream.WriteAsync(
                response,
                cancellationToken);

            await stream.FlushAsync(
                cancellationToken);

            return headers;
        }

        public ValueTask DisposeAsync()
        {
            _listener.Stop();

            return ValueTask.CompletedTask;
        }
    }
}
