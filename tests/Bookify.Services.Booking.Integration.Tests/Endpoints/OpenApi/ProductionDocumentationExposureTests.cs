using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.OpenApi;

[Trait("Category", "Integration")]
[Trait("Category", "Contract")]
[Trait("Category", "Security")]
public sealed class ProductionDocumentationExposureTests
{
    private const string StrictContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

    [Theory]
    [InlineData("/scalar")]
    [InlineData("/openapi/v1.json")]
    public async Task Production_ShouldNotExposeInteractiveOrRawDocumentation(
        string path)
    {
        // CAMBIO 24.10: Host aislado y realmente configurado como Production.
        // No reutilizamos BookingApiFactory porque esa factoría siempre usa Testing.
        using WebApplicationFactory<Program> factory = CreateProductionFactory();
        using HttpClient client = CreateHttpsClient(factory);

        using HttpResponseMessage response = await client.GetAsync(
            path,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(StrictContentSecurityPolicy, GetContentSecurityPolicy(response));
    }

    [Fact]
    public async Task Production_ShouldKeepHealthAvailableWithStrictContentSecurityPolicy()
    {
        using WebApplicationFactory<Program> factory = CreateProductionFactory();
        using HttpClient client = CreateHttpsClient(factory);

        using HttpResponseMessage response = await client.GetAsync(
            "/health",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(StrictContentSecurityPolicy, GetContentSecurityPolicy(response));

        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateProductionFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseContentRoot(FindApiContentRoot());

            // Configuración exclusiva del TestServer. No se modifican las
            // variables de entorno del proceso ni los secretos del desarrollador.
            // Ninguna petición de esta clase necesita abrir una conexión DB.
            builder.UseSetting(
                "ConnectionStrings:Database",
                "Host=127.0.0.1;Port=1;Database=bookify_docs_test;Username=bookify_test");
            builder.UseSetting("Payments:Provider", "Fake");
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("DemoSeed:Enabled", "false");
            builder.UseSetting(
                "Identity:Authority",
                "https://identity.bookify.test/realms/bookify");
            builder.UseSetting("Identity:Audience", "bookify-booking-api");
        });
    }

    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> factory)
    {
        // En Production está activo HTTPS redirection. La petición ya llega
        // por HTTPS para comprobar 404 real, no un 307 por redirección.
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    private static string GetContentSecurityPolicy(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues(
            "Content-Security-Policy",
            out IEnumerable<string>? policies));

        return Assert.Single(policies!);
    }

    private static string FindApiContentRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string projectDirectory = Path.Combine(
                directory.FullName,
                "src",
                "Bookify.Services.Booking.Api");

            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "Bookify.Services.Booking.slnx")) &&
                File.Exists(Path.Combine(
                    projectDirectory,
                    "Bookify.Services.Booking.Api.csproj")))
            {
                return projectDirectory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Bookify API project from the test output directory.");
    }
}
