using Bookify.Services.Booking.Application.Bookings.Policies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


namespace Bookify.Services.Booking.Infrastructure.Bookings;

internal static class BookingDeadlineServiceCollectionExtensions
{
    public static IServiceCollection AddBookingDeadlines(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BookingDeadlineOptions>()
            .Bind(configuration.GetSection(BookingDeadlineOptions.SectionName))
            .Validate(options => options.ApprovalWindow > TimeSpan.Zero, "Booking approval window must be greater than zero.")
            .Validate(options => options.PaymentWindow > TimeSpan.Zero, "Booking payment window must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<IBookingDeadlinePolicy>(serviceProvider =>
        {
            BookingDeadlineOptions options = serviceProvider
                .GetRequiredService<IOptions<BookingDeadlineOptions>>().Value;

            return new BookingDeadlinePolicy(
                options.ApprovalWindow,
                options.PaymentWindow);
        });

        return services;
    }
}
