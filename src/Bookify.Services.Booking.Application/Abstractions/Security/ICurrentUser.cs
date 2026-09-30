namespace Bookify.Services.Booking.Application.Abstractions.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string? Subject { get; }
    IReadOnlyCollection<string> Roles { get; }
}
