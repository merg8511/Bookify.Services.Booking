
using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

internal sealed record CapturedLog(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);

internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<CapturedLog> _entries = [];

    public IReadOnlyList<CapturedLog> Entries =>
        _entries.AsReadOnly();

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return new NoopScope();
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties =
            new Dictionary<string, object?>(
                StringComparer.Ordinal);

        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (KeyValuePair<string, object?> pair in pairs)
            {
                properties[pair.Key] = pair.Value;
            }
        }

        _entries.Add(
            new CapturedLog(
                logLevel,
                eventId,
                formatter(state, exception),
                exception,
                properties));
    }

    private sealed class NoopScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
