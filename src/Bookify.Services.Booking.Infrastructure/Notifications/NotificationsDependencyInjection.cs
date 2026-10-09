using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Infrastructure.Notifications.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Bookify.Services.Booking.Infrastructure.Notifications;

public static class NotificationsDependencyInjection
{
    public static IServiceCollection AddNotifications(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<SmtpNotificationOptions>()
            .Bind(configuration.GetSection(SmtpNotificationOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host),
                "Notifications:Smtp:Host is required when SMTP delivery is enabled.")
            .Validate(options => !options.Enabled || options.Port is >= 1 and <= 65535,
                "Notifications:Smtp:Port must be between 1 and 65535.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.FromName),
                "Notifications:Smtp:FromName is requried when SMTP delivery is enabled.")
            .Validate(options => !options.Enabled || (!string.IsNullOrWhiteSpace(options.FromAddress) &&
                MailboxAddress.TryParse(options.FromAddress.Trim(), out _)),
                "Notifications:Smtp:FromAddress must contain a valid email address.")
            .Validate(options => !options.Enabled || options.TimeoutMilliseconds > 0,
                "Notifications:Smtp:TimeouteMilliseconds must be greater than zero.")
            .Validate(options =>
            {
                if (!options.Enabled)
                {
                    return true;
                }

                bool hasUsername = !string.IsNullOrWhiteSpace(options.Username);
                bool hasPassword = !string.IsNullOrWhiteSpace(options.Password);

                return hasUsername == hasPassword;
            },
            "Notifications:Smtp:Username and Password must either both be configured or both be empty.")
            .ValidateOnStart();

        services.AddScoped<
            IBookingNotificationSender,
            SmtpBookingNotificationSender>();

        return services;
    }
}
