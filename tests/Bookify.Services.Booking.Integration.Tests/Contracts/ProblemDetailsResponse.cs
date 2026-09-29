namespace Bookify.Services.Booking.Integration.Tests.Contracts;

internal sealed record ProblemDetailsResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    string Code,
    string TraceId);
