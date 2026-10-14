using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class SerilogEnvironmentConfigurationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Environment_ShouldApplyEffectiveMinimumLevel(
        bool development, bool debugExpected)
    {
        IConfiguration config = ReadApiSettings(development);
        var sink = new InMemorySink();

        using var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Debug("Bookify observability debug event");
        logger.Information("Bookify observability information event");

        Assert.Contains(sink.Events, e => e.Level == LogEventLevel.Information);
        Assert.Equal(debugExpected,
            sink.Events.Any(e => e.Level == LogEventLevel.Debug));
    }

    [Fact]
    public void Development_ShouldKeepEfCoreSqlCommandLoggingAtWarning()
    {
        IConfiguration config = ReadApiSettings(development: true);

        Assert.Equal("Warning",
            config["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command"]);
        Assert.Equal("Debug", config["Serilog:MinimumLevel:Default"]);
    }

    private static IConfiguration ReadApiSettings(bool development)
    {
        DirectoryInfo? root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && root.GetFiles("*.slnx").Length == 0)
        {
            root = root.Parent;
        }
        if (root is null)
        {
            throw new DirectoryNotFoundException("Bookify solution root was not found.");
        }

        string apiDirectory = Path.Combine(
            root.FullName, "src", "Bookify.Services.Booking.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false);

        if (development)
        {
            configuration.AddJsonFile("appsettings.Development.json", optional: false);
        }

        return configuration.Build();
    }

    private sealed class InMemorySink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];
        public IReadOnlyList<LogEvent> Events => _events;
        public void Emit(LogEvent logEvent) => _events.Add(logEvent);
    }
}
