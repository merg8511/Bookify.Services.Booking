namespace Bookify.Services.Booking.Api.Observability;

internal static class ApiLogEvents
{
    // 2000-2099 — Host and background worker
    public static readonly EventId BackgroundWorkerDisabled =
        new(2000, nameof(BackgroundWorkerDisabled));

    public static readonly EventId BackgroundWorkerStarted =
        new(2001, nameof(BackgroundWorkerStarted));

    public static readonly EventId BackgroundWorkerStopping =
        new(2002, nameof(BackgroundWorkerStopping));

    public static readonly EventId BackgroundWorkerCycleFailed =
        new(2003, nameof(BackgroundWorkerCycleFailed));

    public static readonly EventId BackgroundJobProcessed =
        new(2010, nameof(BackgroundJobProcessed));

    public static readonly EventId BackgroundJobFailed =
        new(2011, nameof(BackgroundJobFailed));

    // HTTP request logging
    public static readonly EventId HttpRequestCompleted =
        new(2020, nameof(HttpRequestCompleted));

    public static readonly EventId HttpRequestFailed =
        new(2021, nameof(HttpRequestFailed));
}
