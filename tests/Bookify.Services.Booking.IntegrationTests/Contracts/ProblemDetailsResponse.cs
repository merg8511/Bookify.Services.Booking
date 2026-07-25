namespace Bookify.Services.Booking.IntegrationTests.Contracts;

internal sealed record ProblemDetailsResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    string Code,
    string TraceId);
