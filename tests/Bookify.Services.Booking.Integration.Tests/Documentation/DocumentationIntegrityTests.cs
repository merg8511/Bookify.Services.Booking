using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bookify.Services.Booking.Integration.Tests.Documentation;

[Trait("Category", "Documentation")]
public sealed class DocumentationIntegrityTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();

    private static readonly string[] RequiredDocumentationFiles =
    [
        "README.md",
        "docs/architecture/bookify-system-diagrams.md",
        "docs/architecture/adr/README.md",
        "docs/payments/stripe-cli-local-webhooks.md",
        "docs/postman/Bookify.Booking.postman_collection.json",
        "docs/postman/Bookify.Local.postman_environment.json",
        "docs/postman/README.md",
        "docs/api/rest-conventions.md",
        "docs/security/endpoint-access-matrix.md",
        "docs/testing/test-coverage-inventory.md"
    ];

    private static readonly string[] InitialAdrs =
    [
        "ADR-0001-ef-core-and-dapper.md",
        "ADR-0002-explicit-cqrs-without-mediator.md",
        "ADR-0003-postgresql-locks-and-optimistic-concurrency.md",
        "ADR-0004-scoped-http-and-provider-idempotency.md",
        "ADR-0005-separate-payment-attempt-and-booking-state.md",
        "ADR-0006-transactional-outbox-and-delivery-idempotency.md",
        "ADR-0007-backgroundservice-for-scheduled-work.md",
        "ADR-0008-oidc-authorization-and-guest-credentials.md",
        "ADR-0009-api-domain-project-reference-boundary.md"
    ];

    [Fact]
    public void Module24_ShouldContainItsRequiredDocumentationArtifacts()
    {
        foreach (string relativePath in RequiredDocumentationFiles)
        {
            Assert.True(File.Exists(InRepository(relativePath)),
                $"Missing documentation artifact: {relativePath}");
        }
    }

    [Fact]
    public void ArchitectureAdrs_ShouldBeCompleteAndIndexed()
    {
        string adrDirectory = InRepository("docs/architecture/adr");
        string index = File.ReadAllText(Path.Combine(adrDirectory, "README.md"));
        string projectReadme = File.ReadAllText(InRepository("README.md"));

        Assert.Contains("docs/architecture/adr/README.md", projectReadme, StringComparison.Ordinal);

        string[] adrFiles = Directory.GetFiles(adrDirectory, "ADR-*.md");
        Assert.True(adrFiles.Length >= InitialAdrs.Length);

        foreach (string filename in InitialAdrs)
        {
            string fullPath = Path.Combine(adrDirectory, filename);
            Assert.True(File.Exists(fullPath), $"Missing ADR: {filename}");
        }

        foreach (string file in adrFiles)
        {
            string filename = Path.GetFileName(file);
            string body = File.ReadAllText(file);

            Assert.Contains($"{filename}", index, StringComparison.Ordinal);
            Assert.Contains("## Context", body, StringComparison.Ordinal);
            Assert.Contains("## Decision", body, StringComparison.Ordinal);
            Assert.Contains("## Alternatives considered", body, StringComparison.Ordinal);
            Assert.Contains("## Consequences and trade-offs", body, StringComparison.Ordinal);
            Assert.Contains("## Verification and review triggers", body, StringComparison.Ordinal);
            Assert.Contains("## Source of truth", body, StringComparison.Ordinal);
        }

        string boundary = File.ReadAllText(Path.Combine(
            adrDirectory,
            "ADR-0009-api-domain-project-reference-boundary.md"));

        Assert.Contains("API", boundary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Domain", boundary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bookify.Services.Booking.Api.csproj", boundary, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_ShouldDocumentSetupAndLinkModule24Resources()
    {
        string readme = File.ReadAllText(InRepository("README.md"));

        foreach (string section in new[]
        {
            "## ⚡ Quick start",
            "## 🧱 Architecture",
            "## 🔐 Configuration reference",
            "## ✅ Testing and quality",
            "## 📚 Documentation"
        })
        {
            Assert.Contains(section, readme, StringComparison.Ordinal);
        }

        foreach (string destination in new[]
        {
            "docs/architecture/bookify-system-diagrams.md",
            "docs/architecture/adr/README.md",
            "docs/postman/README.md",
            "docs/payments/stripe-cli-local-webhooks.md"
        })
        {
            Assert.Contains(destination, readme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void StripeCliGuide_ShouldDocumentActualWebhookAndThreeSupportedEvents()
    {
        string guide = File.ReadAllText(InRepository(
            "docs/payments/stripe-cli-local-webhooks.md"));

        foreach (string required in new[]
        {
            "stripe listen",
            "--forward-to",
            "/api/v1/payments/webhooks/stripe",
            "Stripe-Signature",
            "payment_intent.succeeded",
            "payment_intent.payment_failed",
            "payment_intent.canceled",
            "stripe trigger"
        })
        {
            Assert.Contains(required, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MarkdownDocumentation_ShouldHaveNoBrokenRelativeLinks()
    {
        string[] sources =
        [
            "README.md",
            "docs/architecture/bookify-system-diagrams.md",
            "docs/architecture/adr/README.md",
            "docs/payments/stripe-cli-local-webhooks.md",
            "docs/postman/README.md"
        ];

        var allSources = sources.Select(InRepository).ToList();
        allSources.AddRange(Directory.GetFiles(
            InRepository("docs/architecture/adr"),
            "ADR-*.md"));

        foreach (string sourcePath in allSources)
        {
            string contents = File.ReadAllText(sourcePath);

            // Enlaces Markdown [label](relative/path.md).
            // No se auditan enlaces remotos ni anchors, porque
            // dependen de destinos externos o del renderer.
            MatchCollection links = Regex.Matches(
                contents,
                @"(?<!!)\[[^\]\r\n]+\]\((?<url>[^)\r\n]+)\)",
                RegexOptions.CultureInvariant);

            foreach (Match match in links)
            {
                string destination = match.Groups["url"].Value.Trim().Trim('<', '>');

                if (destination.StartsWith('#') ||
                    Uri.TryCreate(destination, UriKind.Absolute, out _))
                {
                    continue;
                }

                string relativeLink = Uri.UnescapeDataString(
                    destination.Split('#')[0].Split('?')[0]);

                if (string.IsNullOrWhiteSpace(relativeLink))
                {
                    continue;
                }

                string resolvedPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(sourcePath)!,
                    relativeLink.Replace('/', Path.DirectorySeparatorChar)));

                string projectRelative = Path.GetRelativePath(SolutionRoot, resolvedPath);

                Assert.False(projectRelative == ".." ||
                    projectRelative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal),
                    $"Link outside repository in {sourcePath}: {destination}");

                Assert.True(File.Exists(resolvedPath) || Directory.Exists(resolvedPath),
                    $"Broken link in {sourcePath}: {destination}");
            }
        }
    }

    [Fact]
    public void SystemDiagrams_ShouldHaveTenNonEmptyMermaidBlocks()
    {
        string text = File.ReadAllText(InRepository(
            "docs/architecture/bookify-system-diagrams.md"));

        MatchCollection openingFences = Regex.Matches(
            text,
            @"^```mermaid[ \t]*\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        MatchCollection allFences = Regex.Matches(
            text,
            @"^```(?:mermaid)?[ \t]*\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        Assert.Equal(10, openingFences.Count);
        Assert.Equal(20, allFences.Count);

        MatchCollection diagrams = Regex.Matches(
            text,
            @"^```mermaid[ \t]*\r?\n(?<content>.*?)^```[ \t]*\r?$",
            RegexOptions.Multiline |
            RegexOptions.Singleline |
            RegexOptions.CultureInvariant);

        Assert.Equal(10, diagrams.Count);

        foreach (Match diagram in diagrams)
        {
            string content = diagram.Groups["content"].Value.Trim();

            Assert.True(
                content.StartsWith("flowchart ", StringComparison.Ordinal) ||
                content.StartsWith("sequenceDiagram", StringComparison.Ordinal) ||
                content.StartsWith("stateDiagram", StringComparison.Ordinal),
                $"Unexpected Mermaid diagram type: {content}");
        }
    }

    [Fact]
    public void Postman_ShouldContainOrderedFlowAndNoExportedOwnerToken()
    {
        string collectionPath = InRepository(
            "docs/postman/Bookify.Booking.postman_collection.json");
        string environmentPath = InRepository(
            "docs/postman/Bookify.Local.postman_environment.json");

        using JsonDocument collection = JsonDocument.Parse(File.ReadAllText(collectionPath));
        using JsonDocument environment = JsonDocument.Parse(File.ReadAllText(environmentPath));

        JsonElement root = collection.RootElement;
        Assert.Contains("collection/v2.1.0", root.GetProperty("info")
            .GetProperty("schema").GetString()!, StringComparison.Ordinal);

        JsonElement folders = root.GetProperty("item");
        Assert.Equal(5, folders.GetArrayLength());
        Assert.Equal(23, CountRequests(folders));

        string[] expectedFolderPrefixes = ["00", "01", "02", "03", "04"];
        for (int index = 0; index < expectedFolderPrefixes.Length; index++)
        {
            string folderName = folders[index].GetProperty("name").GetString()!;
            Assert.StartsWith(expectedFolderPrefixes[index], folderName, StringComparison.Ordinal);
        }

        JsonElement variables = environment.RootElement.GetProperty("values");

        JsonElement ownerToken = Assert.Single(variables.EnumerateArray(), item => item.GetProperty("key").GetString() == "ownerAccessToken");

        Assert.Equal(string.Empty, ownerToken.GetProperty("value").GetString());

        // El entorno exportado no debe contener valores de secretos.
        foreach (JsonElement variable in variables.EnumerateArray())
        {
            string key = variable.GetProperty("key").GetString() ?? string.Empty;

            if (key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("password", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(string.IsNullOrEmpty(variable.GetProperty("value").GetString()),
                    $"Sensitive Postman variable has a stored value: {key}");
            }
        }
    }

    private static int CountRequests(JsonElement items)
    {
        int total = 0;

        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.TryGetProperty("request", out _))
            {
                total++;
            }

            if (item.TryGetProperty("item", out JsonElement children))
            {
                total += CountRequests(children);
            }
        }

        return total;
    }

    private static string InRepository(string relativePath)
    {
        return Path.Combine(SolutionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Bookify.Services.Booking.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Bookify solution root from the test output directory.");
    }
}
