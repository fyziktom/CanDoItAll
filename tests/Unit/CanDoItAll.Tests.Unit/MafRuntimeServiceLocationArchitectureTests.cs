using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// Guards the typed-dependency composition of the MAF runtime: runtime code consumes constructor-injected
/// typed dependencies instead of retained <see cref="IServiceProvider"/> fields or runtime service location.
/// Composition roots (service-collection registration files and workspace factories) stay the only
/// places allowed to translate an <see cref="IServiceProvider"/> into typed dependencies.
/// </summary>
public sealed class MafRuntimeServiceLocationArchitectureTests
{
    private static readonly string[] ScannedRelativeRoots =
    [
        Path.Combine("src", "MAF", "Common", "CanDoItAll.AgentFramework.Maf", "Runtime"),
        Path.Combine("src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Execution")
    ];

    private static readonly string[] CompositionRootFileNames =
    [
        "ServiceCollectionExtensions.cs",
        "WorkspaceRuntimeScopeFactory.cs",
        "AgentFrameworkWorkspaceFactory.cs"
    ];

    private static readonly Regex RetainedServiceProviderFieldPattern = new(
        @"\b(private|protected|internal|public)\s+readonly\s+IServiceProvider\b",
        RegexOptions.Compiled);

    private static readonly Regex ServiceLocationCallPattern = new(
        @"\bservices?\.(GetService|GetRequiredService|GetServices)\s*<",
        RegexOptions.Compiled);

    [Fact]
    public void Runtime_composition_has_no_retained_service_provider_fields()
    {
        var violations = EnumerateScannedFiles()
            .SelectMany(path => FindPatternViolations(path, RetainedServiceProviderFieldPattern))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Runtime_composition_has_no_service_location_calls()
    {
        var violations = EnumerateScannedFiles()
            .Where(path => !IsCompositionRootFile(path))
            .SelectMany(path => FindPatternViolations(path, ServiceLocationCallPattern))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Runtime_dependency_resolver_service_locator_stays_deleted()
    {
        var root = FindRepoRoot();
        var resolverPath = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafRuntimeDependencyResolver.cs");

        Assert.False(
            File.Exists(resolverPath),
            "MafRuntimeDependencyResolver.cs was removed by the typed-dependency composition refactor and must not return. " +
            "Compose runtime collaborators through MafAgentRuntimeDependencies instead.");
    }

    [Fact]
    public void Typed_dependency_composition_file_exists()
    {
        var root = FindRepoRoot();
        var compositionPath = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "Runtime",
            "MafAgentRuntimeDependencies.cs");

        Assert.True(
            File.Exists(compositionPath),
            "MafAgentRuntimeDependencies.cs is the single sanctioned IServiceProvider-to-typed-dependencies bridge for the MAF runtime.");
        var source = File.ReadAllText(compositionPath);
        Assert.Contains("MafAgentRuntimeDependencies", source, StringComparison.Ordinal);
        Assert.Contains("FromServices(IServiceProvider serviceProvider)", source, StringComparison.Ordinal);
    }

    private static bool IsCompositionRootFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return CompositionRootFileNames.Any(compositionRootFileName =>
            fileName.EndsWith(compositionRootFileName, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateScannedFiles()
    {
        var root = FindRepoRoot();
        foreach (var relativeRoot in ScannedRelativeRoots)
        {
            var absoluteRoot = Path.Combine(root, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> FindPatternViolations(string path, Regex pattern)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (pattern.IsMatch(line))
            {
                yield return $"{path}:{lineNumber}:{line.Trim()}";
            }
        }
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
