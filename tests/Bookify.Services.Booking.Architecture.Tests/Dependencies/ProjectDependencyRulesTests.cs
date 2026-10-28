
using System.Reflection;
using System.Xml.Linq;

namespace Bookify.Services.Booking.Architecture.Tests.Dependencies;

public sealed class ProjectDependencyRulesTests
{
    private const string ProjectPrefix =
        "Bookify.Services.Booking.";

    private static readonly HashSet<string> ProductionAssemblies =
    [
        ProjectPrefix + "Domain",
        ProjectPrefix + "Application",
        ProjectPrefix + "Infrastructure",
        ProjectPrefix + "Api"
    ];

    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    [InlineData("Infrastructure")]
    [InlineData("Api")]
    public void ProjectReferences_ShouldRespectLayerGraph(
        string layer)
    {
        // ARRANGE
        string[] expected = ExpectedDependencies(layer);

        XDocument project = LoadProject(layer);

        // ACT
        string[] actual = project
            .Descendants("ProjectReference")
            .Select(reference =>
                (string?)reference.Attribute("Include"))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path =>
                Path.GetFileNameWithoutExtension(
                    path!.Replace('\\', '/')))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // ASSERT
        Assert.Equal(
            expected.OrderBy(
                name => name,
                StringComparer.Ordinal),
            actual);
    }

    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    [InlineData("Infrastructure")]
    [InlineData("Api")]
    public void CompiledAssemblies_ShouldNotReferenceForbiddenLayers(
        string layer)
    {
        // ARRANGE
        Assembly assembly = LoadAssembly(layer);

        string[] allowed = ExpectedDependencies(layer);

        // ACT
        string[] forbiddenReferences = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => ProductionAssemblies.Contains(name))
            .Where(name => !allowed.Contains(
                name,
                StringComparer.Ordinal))
            .ToArray();

        // ASSERT
        Assert.True(
            forbiddenReferences.Length == 0,
            $"{layer} references forbidden assemblies: " +
            string.Join(", ", forbiddenReferences));
    }

    [Fact]
    public void Domain_ShouldNotDeclareExternalNuGetDependencies()
    {
        // ARRANGE
        XDocument project = LoadProject("Domain");

        // ACT
        XElement[] packages = project
            .Descendants("PackageReference")
            .ToArray();

        // ASSERT
        Assert.Empty(packages);
    }

    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    public void CoreLayers_ShouldNotReferenceStripe(
        string layer)
    {
        // ARRANGE
        Assembly assembly = LoadAssembly(layer);

        XDocument project = LoadProject(layer);

        // ACT
        string[] stripeAssemblies = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith(
                "Stripe",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        string[] stripePackages = project
            .Descendants("PackageReference")
            .Select(package =>
                (string?)package.Attribute("Include") ?? string.Empty)
            .Where(name => name.StartsWith(
                "Stripe",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // ASSERT
        Assert.Empty(stripeAssemblies);
        Assert.Empty(stripePackages);
    }

    private static string[] ExpectedDependencies(string layer)
    {
        return layer switch
        {
            "Domain" => [],

            "Application" =>
            [
                ProjectPrefix + "Domain"
            ],

            "Infrastructure" =>
            [
                ProjectPrefix + "Application",
                ProjectPrefix + "Domain"
            ],

            // CAMBIO 22.5:
            // API usa actualmente Domain.Shared directamente.
            // La excepción respecto al resumen del roadmap
            // queda explícita hasta acordar su precisión.
            "Api" =>
            [
                ProjectPrefix + "Application",
                ProjectPrefix + "Infrastructure",
                ProjectPrefix + "Domain"
            ],

            _ => throw new ArgumentOutOfRangeException(
                nameof(layer),
                layer,
                "Unknown production layer.")
        };
    }

    private static Assembly LoadAssembly(string layer)
    {
        return Assembly.Load(
            new AssemblyName(ProjectPrefix + layer));
    }

    private static XDocument LoadProject(string layer)
    {
        string projectName = ProjectPrefix + layer;

        string projectPath = Path.Combine(
            FindSolutionRoot(),
            "src",
            projectName,
            projectName + ".csproj");

        Assert.True(
            File.Exists(projectPath),
            $"Project file not found: {projectPath}");

        return XDocument.Load(projectPath);
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string solutionPath = Path.Combine(
                directory.FullName,
                "Bookify.Services.Booking.slnx");

            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate Bookify.Services.Booking.slnx.");
    }
}
