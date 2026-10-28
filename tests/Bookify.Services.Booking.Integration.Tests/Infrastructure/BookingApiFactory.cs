using System.Net.Http.Headers;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

public sealed class BookingApiFactory
    : WebApplicationFactory<Program>,
      IAsyncLifetime
{
    // CAMBIO 22.8:
    // Clave de configuración del host, en lugar de
    // modificar la variable de entorno global.
    private const string DatabaseConnectionSetting =
        "ConnectionStrings:Database";

    private const string IntegrationTestPermitLimit = "100000";
    private const string GuestAccessHeader = "Booking-Guest-Token";

    internal const string StripeWebhookSecret =
        "whsec_bookify_integration_tests";

    internal const string IdentityAuthority =
        "https://identity.bookify.test/realms/bookify";

    internal const string IdentityAudience =
        "bookify-booking-api";

    internal const string TrustedFrontendOrigin =
        "https://frontend.bookify.test";

    private const string TrustedProxyAddress = "127.0.0.1";

    private readonly PostgreSqlTestDatabase _database =
        new();

    private HttpClient? _client;

    public HttpClient Client => _client ??
        throw new InvalidOperationException(
            "The API factory has not been initialized");

    internal string DatabaseConnectionString =>
        _database.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        // 1. Iniciar PostgreSQL real y esperar disponibilidad.
        await _database.StartAsync();

        // 2. Crear la API con la configuración propia del host.
        _client = CreateApiClient();

        // 3. Preparar el esquema para las pruebas existentes.
        await ApplyMigrationsAsync();
    }

    public HttpClient CreateAuthenticatedClient(
        string subject,
        params string[] roles)
    {
        HttpClient client = CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                JwtBearerDefaults.AuthenticationScheme,
                TestIdentityTokens.Create(
                    subject,
                    roles));

        return client;
    }

    public HttpClient CreateOwnerClient(
        string subject = TestIdentitySubjects.Owner)
    {
        return CreateAuthenticatedClient(
            subject,
            BookifyRoles.Owner);
    }

    public HttpClient CreateCustomerClient(
        string subject = TestIdentitySubjects.Customer)
    {
        return CreateAuthenticatedClient(
            subject,
            BookifyRoles.Customer);
    }

    public HttpClient CreateAdminClient(
        string subject = TestIdentitySubjects.Admin)
    {
        return CreateAuthenticatedClient(
            subject,
            BookifyRoles.Admin);
    }

    public HttpClient CreateGuestClient(
        string guestAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            guestAccessToken);

        HttpClient client = CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });

        bool added = client.DefaultRequestHeaders
            .TryAddWithoutValidation(
                GuestAccessHeader,
                guestAccessToken);

        if (!added)
        {
            client.Dispose();

            throw new InvalidOperationException(
                "The guest booking access header " +
                "could not be configured.");
        }

        return client;
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseContentRoot(GetApiContentRoot());

        // CAMBIO 22.8:
        // La cadena pertenece a esta instancia de host.
        // No utilizamos Environment.SetEnvironmentVariable().
        builder.UseSetting(
            DatabaseConnectionSetting,
            _database.ConnectionString);

        builder.UseSetting(
            "BackgroundJobs:Enabled",
            "false");

        builder.UseSetting(
            "Payments:Provider",
            "Fake");

        builder.UseSetting(
            "Payments:Stripe:WebhookSecret",
            StripeWebhookSecret);

        builder.UseSetting(
            "Payments:Stripe:WebhookToleranceSeconds",
            "300");

        builder.UseSetting(
            "Identity:Authority",
            IdentityAuthority);

        builder.UseSetting(
            "Identity:Audience",
            IdentityAudience);

        builder.UseSetting(
            "HttpSecurity:TrustedFrontendOrigins:0",
            TrustedFrontendOrigin);

        builder.UseSetting(
            "HttpSecurity:ForwardedHeaders:KnownProxies:0",
            TrustedProxyAddress);

        builder.UseSetting(
            "HttpSecurity:ForwardedHeaders:ForwardLimit",
            "1");

        // Cuotas altas en las pruebas generales.
        // Los tests específicos de rate limiting
        // utilizan una configuración aislada.
        builder.UseSetting(
            "RateLimiting:PublicReads:PermitLimit",
            IntegrationTestPermitLimit);

        builder.UseSetting(
            "RateLimiting:BookingCreation:PermitLimit",
            IntegrationTestPermitLimit);

        builder.UseSetting(
            "RateLimiting:PaymentInitiation:PermitLimit",
            IntegrationTestPermitLimit);

        builder.UseSetting(
            "RateLimiting:LoginFacing:PermitLimit",
            IntegrationTestPermitLimit);

        builder.UseSetting(
            "RateLimiting:Webhook:PermitLimit",
            IntegrationTestPermitLimit);

        builder.ConfigureTestServices(
            services =>
            {
                services.AddSingleton<
                    FakeBookingNotificationSender>();

                services.AddSingleton<
                    IBookingNotificationSender>(
                    serviceProvider =>
                        serviceProvider.GetRequiredService<
                            FakeBookingNotificationSender>());

                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        var configuration =
                            new OpenIdConnectConfiguration
                            {
                                Issuer = IdentityAuthority
                            };

                        configuration.SigningKeys.Add(
                            TestIdentityTokens.SigningKey);

                        // Evitar descubrimiento OIDC externo.
                        options.Configuration = configuration;

                        options.ConfigurationManager =
                            new StaticConfigurationManager<
                                OpenIdConnectConfiguration>(
                                configuration);

                        options.TokenValidationParameters
                            .IssuerSigningKey =
                                TestIdentityTokens.SigningKey;

                        options.TokenValidationParameters
                            .ValidIssuer =
                                IdentityAuthority;

                        options.TokenValidationParameters
                            .ValidAudience =
                                IdentityAudience;
                    });
            });
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            _client?.Dispose();

            await base.DisposeAsync();
        }
        finally
        {
            // Cada factoría libera su propio contenedor.
            await _database.DisposeAsync();
        }
    }

    private HttpClient CreateApiClient()
    {
        // CAMBIO 22.8:
        // Ya no necesitamos guardar, reemplazar ni restaurar
        // variables de entorno globales.
        //
        // ConfigureWebHost proporciona la cadena específica
        // de esta factoría.
        return CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });
    }

    private async Task ApplyMigrationsAsync()
    {
        await using AsyncServiceScope scope =
            Services.CreateAsyncScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    private static string GetApiContentRoot()
    {
        var directory = new DirectoryInfo(
            AppContext.BaseDirectory);

        while (
            directory != null &&
            Directory.GetFiles(
                directory.FullName,
                "*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new DirectoryNotFoundException(
                "The solution root directory could not be found.");
        }

        string[] projectFiles = Directory.GetFiles(
            directory.FullName,
            "Bookify.Services.Booking.Api.csproj",
            SearchOption.AllDirectories);

        if (projectFiles.Length == 0)
        {
            throw new DirectoryNotFoundException(
                "The 'Bookify.Services.Booking.Api.csproj' " +
                "file could not be found in the solution.");
        }

        return Path.GetDirectoryName(
            projectFiles[0])!;
    }
}
