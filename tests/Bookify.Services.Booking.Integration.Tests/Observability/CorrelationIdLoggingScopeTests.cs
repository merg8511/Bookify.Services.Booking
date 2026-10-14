
using Bookify.Services.Booking.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class CorrelationIdLoggingScopeTests
{
    [Fact]
    public async Task ValidHeader_ShouldBeAvailableInsideLoggingScope()
    {
        var logger = new ScopeCaptureLogger();

        string? observedId = null;

        RequestDelegate next = context =>
        {
            observedId = logger.ActiveCorrelationId;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(
            next,
            logger);

        var context = new DefaultHttpContext();

        context.Request.Headers[
            CorrelationIdMiddleware.HeaderName] =
                "request-001";

        await middleware.InvokeAsync(context);

        Assert.Equal("request-001", observedId);

        Assert.Equal(
            "request-001",
            context.Response.Headers[
                CorrelationIdMiddleware.HeaderName].ToString());

        Assert.Null(logger.ActiveCorrelationId);
    }

    [Fact]
    public async Task MissingHeader_ShouldGenerateScopeAndResponseId()
    {
        var logger = new ScopeCaptureLogger();

        string? observedId = null;

        RequestDelegate next = context =>
        {
            observedId = logger.ActiveCorrelationId;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(
            next,
            logger);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        string responseId =
            context.Response.Headers[
                CorrelationIdMiddleware.HeaderName].ToString();

        Assert.Matches("^[a-f0-9]{32}$", responseId);
        Assert.Equal(responseId, observedId);
        Assert.Null(logger.ActiveCorrelationId);
    }

    [Fact]
    public async Task Exception_ShouldDisposeLoggingScope()
    {
        var logger = new ScopeCaptureLogger();

        RequestDelegate next = _ =>
            Task.FromException(
                new InvalidOperationException(
                    "Simulated failure"));

        var middleware = new CorrelationIdMiddleware(
            next,
            logger);

        var context = new DefaultHttpContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

        Assert.Null(logger.ActiveCorrelationId);
    }

    private sealed class ScopeCaptureLogger :
        ILogger<CorrelationIdMiddleware>
    {
        private readonly AsyncLocal<string?> _correlationId = new();

        public string? ActiveCorrelationId =>
            _correlationId.Value;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            string? previousValue = _correlationId.Value;

            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                foreach (KeyValuePair<string, object?> property
                    in properties)
                {
                    if (property.Key == "CorrelationId")
                    {
                        _correlationId.Value =
                            property.Value?.ToString();

                        break;
                    }
                }
            }

            return new RestoreScope(
                _correlationId,
                previousValue);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class RestoreScope : IDisposable
        {
            private readonly AsyncLocal<string?> _current;
            private readonly string? _previousValue;

            public RestoreScope(
                AsyncLocal<string?> current,
                string? previousValue)
            {
                _current = current;
                _previousValue = previousValue;
            }

            public void Dispose()
            {
                _current.Value = _previousValue;
            }
        }
    }
}
