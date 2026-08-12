namespace Bookify.Services.Booking.Api.Extensions;

internal static class ProblemTypeUris
{
    internal const string Validation =
        "urn:bookify:problem-type:validation";

    internal const string NotFound =
        "urn:bookify:problem-type:not-found";

    internal const string Conflict =
        "urn:bookify:problem-type:conflict";

    internal const string ServerError =
        "urn:bookify:problem-type:server-error";
}
