using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace Bookify.Services.Booking.Api.Observability;

internal sealed class TraceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        Activity? activity = Activity.Current;

        if(activity is null)
        {
            return;
        }

        if(activity.TraceId != default)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        }

        if(activity.SpanId != default)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
        }
    }
}
