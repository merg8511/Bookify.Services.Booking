using Bookify.Services.Booking.Infrastructure.DemoSeeding;

namespace Bookify.Services.Booking.Api.DemoSeeding;

internal static class DemoSeedStartupExtensions
{
    public static async Task RunDemoSeedAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        bool enabled = app.Configuration.GetValue<bool>("DemoSeed:Enabled");

        if (!enabled)
        {
            return;
        }

        bool allowedEnvironment = app.Environment.IsDevelopment() ||
                                  app.Environment.IsEnvironment("Demo");

        if (!allowedEnvironment)
        {
            throw new InvalidOperationException(
                "Demo seed can only be enabled in Development or Demo enviroment.");
        }

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

        DemoSeedRunner runner = scope.ServiceProvider.GetRequiredService<DemoSeedRunner>();

        await runner.RunAsync(app.Lifetime.ApplicationStopping);

        DemoPaymentConsistencyValidator validator = scope.ServiceProvider.GetRequiredService<DemoPaymentConsistencyValidator>();

        await validator.ValidateAsync(app.Lifetime.ApplicationStopping);
    }
}
