using System.Net.Http.Headers;
using Bookify.Services.Booking.Application.Abstractions.Security;
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

public sealed class BookingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ConnectionStringVariable = "ConnectionStrings__Database";
    internal const string StripeWebhookSecret = "whsec_bookify_integration_tests";
    internal const string IdentityAuthority = "https://identity.bookify.test/realms/bookify";
    internal const string IdentityAudience = "bookify-booking-api";

    private readonly PostgreSqlTestDatabase _database = new();
    private HttpClient? _client;

    public HttpClient Client => _client ?? throw new InvalidOperationException("The API factory has not been initialized");

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        _client = CreateApiClient();
        await ApplyMigrationsAsync();
    }

    public HttpClient CreateAuthenticatedClient(string subject, params string[] roles)
    {
        HttpClient client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            TestIdentityTokens.Create(subject, roles));

        return client;
    }

    public HttpClient CreateOwnerClient(string subject = TestIdentitySubjects.Owner)
    {
        return CreateAuthenticatedClient(subject, BookifyRoles.Owner);
    }

    public HttpClient CreateCustomerClient(string subject = TestIdentitySubjects.Customer)
    {
        return CreateAuthenticatedClient(subject, BookifyRoles.Customer);
    }

    public HttpClient CreateAdminClient(string subject = TestIdentitySubjects.Admin)
    {
        return CreateAuthenticatedClient(subject, BookifyRoles.Admin);
    }

    public HttpClient CreateGuestClient(string guestAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestAccessToken);

        HttpClient client = CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });

        bool headerAdded = client.DefaultRequestHeaders.TryAddWithoutValidation("Booking-Guest-Token", guestAccessToken);

        if (!headerAdded)
        {
            client.Dispose();

            throw new InvalidOperationException("The guest booking access header could not be configured.");
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(GetApiContentRoot());
        builder.UseSetting("Payments:Provider", "Fake");
        builder.UseSetting("Payments:Stripe:WebhookSecret", StripeWebhookSecret);
        builder.UseSetting("Payments:Stripe:WebhookToleranceSeconds", "300");
        builder.UseSetting("Identity:Authority", IdentityAuthority);
        builder.UseSetting("Identity:Audience", IdentityAudience);

        builder.ConfigureTestServices(
            services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        var configuration = new OpenIdConnectConfiguration
                        {
                            Issuer = IdentityAuthority
                        };

                        configuration.SigningKeys.Add(TestIdentityTokens.SigningKey);

                        // Prevent OIDC network discovery in integration tests.
                        options.Configuration = configuration;
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                        options.TokenValidationParameters.IssuerSigningKey = TestIdentityTokens.SigningKey;
                        options.TokenValidationParameters.ValidIssuer = IdentityAuthority;
                        options.TokenValidationParameters.ValidAudience = IdentityAudience;
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
            await _database.DisposeAsync();
        }
    }

    private HttpClient CreateApiClient()
    {
        string? previousConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        Environment.SetEnvironmentVariable(ConnectionStringVariable, _database.ConnectionString);

        try
        {
            return CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, previousConnectionString);
        }
    }

    private async Task ApplyMigrationsAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    private static string GetApiContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && Directory.GetFiles(directory.FullName, "*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new DirectoryNotFoundException("The solution root directory could not be found.");
        }

        string[] projectFiles = Directory.GetFiles(directory.FullName, "Bookify.Services.Booking.Api.csproj", SearchOption.AllDirectories);

        if (projectFiles.Length == 0)
        {
            throw new DirectoryNotFoundException("The 'Bookify.Services.Booking.Api.csproj' file could not be found in the solution.");
        }

        return Path.GetDirectoryName(projectFiles[0])!;
    }
}
