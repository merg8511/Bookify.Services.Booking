using MailKit.Security;

namespace Bookify.Services.Booking.Infrastructure.Notifications.Smtp;

internal sealed class SmtpNotificationOptions
{
    public const string SectionName = "Notifications:Smtp";
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Bookify";
    public int TimeoutMilliseconds { get; set; } = 10000;
}
