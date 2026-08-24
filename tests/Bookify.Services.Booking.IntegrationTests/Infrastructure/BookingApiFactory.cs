using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Infrastructure;

public sealed class BookingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ConnectionStringVariable = "ConnectionStrings__Database";

    private readonly PostgreSqlTestDatabase _database = new();

    private HttpClient? _client;
    public HttpClient Client =>
        _client ?? throw new InvalidOperationException("The API factory has not been initialized");

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        _client = CreateApiClient();

        await ApplyMigrationsAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(GetApiContentRoot());
        builder.UseSetting("Payments:Provider", "Fake");
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

        var projectFiles = Directory.GetFiles(
            directory.FullName,
            "Bookify.Services.Booking.Api.csproj",
            SearchOption.AllDirectories);

        if (projectFiles.Length == 0)
        {
            throw new DirectoryNotFoundException("The 'Bookify.Services.Booking.Api.csproj' file could not be found in the solution.");
        }

        return Path.GetDirectoryName(projectFiles[0])!;
    }
}
