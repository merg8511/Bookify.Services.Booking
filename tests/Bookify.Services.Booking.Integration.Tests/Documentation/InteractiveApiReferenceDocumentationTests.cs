namespace Bookify.Services.Booking.Integration.Tests.Documentation;

[Trait("Category", "Documentation")]
public sealed class InteractiveApiReferenceDocumentationTests
{
    private const string InteractiveGuideRelativePath =
        "docs/api/interactive-reference.md";

    [Fact]
    public void Readme_ShouldLinkTheInteractiveApiReference()
    {
        string root = FindSolutionRoot();
        string readmePath = Path.Combine(root, "README.md");
        string guidePath = Path.Combine(root, "docs", "api", "interactive-reference.md");

        Assert.True(File.Exists(readmePath), "The root README.md is missing.");
        Assert.True(File.Exists(guidePath), "The Scalar API guide is missing.");

        string readme = File.ReadAllText(readmePath);
        string guide = File.ReadAllText(guidePath);

        Assert.Contains(InteractiveGuideRelativePath, readme, StringComparison.Ordinal);

        foreach (string requiredTerm in new[]
        {
            "/scalar",
            "/openapi/v1.json",
            "Development",
            "Testing",
            "Production",
            "Bearer",
            "BookingGuestToken",
            "Idempotency-Key",
            "nonce"
        })
        {
            Assert.Contains(requiredTerm, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void InteractiveGuide_ShouldLinkTheExistingStripeAndOpenApiTests()
    {
        string guidePath = Path.Combine(
            FindSolutionRoot(), "docs", "api", "interactive-reference.md");
        string guideDirectory = Path.GetDirectoryName(guidePath)!;
        string guide = File.ReadAllText(guidePath);

        string stripeGuide = "../payments/stripe-cli-local-webhooks.md";
        string openApiTests =
            "../../tests/Bookify.Services.Booking.Integration.Tests/Endpoints/OpenApi/OpenApiDocumentContractTests.cs";

        foreach (string relativeLink in new[] { stripeGuide, openApiTests })
        {
            Assert.Contains(relativeLink, guide, StringComparison.Ordinal);

            string destination = Path.GetFullPath(Path.Combine(
                guideDirectory,
                relativeLink.Replace('/', Path.DirectorySeparatorChar)));

            Assert.True(File.Exists(destination),
                $"Broken link in interactive API guide: {relativeLink}");
        }
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "Bookify.Services.Booking.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Bookify solution root from the test output directory.");
    }
}
