using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Guards the compile-time dependency direction of the MAF layer: the MAF runtime project must not
/// reference product modules or the workflow adapter, and the security runtime contracts live in a
/// dependency-free <c>CanDoItAll.Security.Abstractions</c> foundation project that modules and MAF both
/// consume without the MAF layer reaching upward into <c>CanDoItAll.Modules.*</c>.
/// </summary>
public sealed class MafDependencyDirectionArchitectureTests
{
    private static readonly Regex ProjectReferencePattern = new(
        "<ProjectReference\\s+Include=\"([^\"]+)\"",
        RegexOptions.Compiled);

    [Fact]
    public void Maf_project_references_no_product_modules_and_no_workflow_adapter()
    {
        var projectReferences = ReadProjectReferences(MafProjectPath());

        var moduleReferences = projectReferences
            .Where(include => include.Replace('/', '\\').Contains(@"\Modules\", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(moduleReferences);

        var adapterReferences = projectReferences
            .Where(include => include.Contains("Workflows.MafAdapter", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(adapterReferences);
    }

    [Fact]
    public void Maf_sources_do_not_use_product_module_namespaces()
    {
        var mafRoot = Path.Combine(
            FindRepoRoot(),
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf");

        var violations = Directory.EnumerateFiles(mafRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifactPath(path))
            .Where(path => File.ReadAllText(path).Contains("using CanDoItAll.Modules.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(mafRoot, path))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    /// <summary>
    /// SB13: mirrors <c>check_architecture_guards.py</c>'s forbidden-MAF-token scan as an in-suite guard test so a
    /// process-semantics regression fails fast in the normal Unit run, without requiring the Python script.
    /// </summary>
    [Fact]
    public void Maf_sources_contain_no_process_semantics_tokens()
    {
        var mafRoot = Path.Combine(
            FindRepoRoot(),
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf");
        var forbiddenTokens = new[]
        {
            "ProcessStepOutcomeResult",
            "ProcessStepOutcomeStatus",
            "ProcessArtifactRecovery",
            "artifacts/process-runs",
            "\"process-step\""
        };

        var violations = Directory.EnumerateFiles(mafRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifactPath(path))
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return forbiddenTokens
                    .Where(token => text.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(mafRoot, path)}: contains forbidden token '{token}'");
            })
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    /// <summary>
    /// Negative proof that the SB13 extraction is real (not a delegation shim left in place): the deleted MAF
    /// recovery service file must not exist, and no MAF source may still spell out the managed process-run
    /// artifact path template it once owned.
    /// </summary>
    [Fact]
    public void Maf_no_longer_contains_the_deleted_process_artifact_recovery_service()
    {
        var mafRoot = Path.Combine(
            FindRepoRoot(),
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf");

        var matchingFiles = Directory
            .EnumerateFiles(mafRoot, "ProcessArtifactRecoveryService.cs", SearchOption.AllDirectories)
            .ToList();
        Assert.Empty(matchingFiles);

        var pathTemplateViolations = Directory.EnumerateFiles(mafRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifactPath(path))
            .Where(path => File.ReadAllText(path).Contains("artifacts/process-runs", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(mafRoot, path))
            .ToList();
        Assert.Empty(pathTemplateViolations);
    }

    [Fact]
    public void Security_abstractions_project_has_zero_project_references()
    {
        Assert.Empty(ReadProjectReferences(SecurityAbstractionsProjectPath()));
    }

    [Fact]
    public void Security_abstractions_sources_do_not_reference_product_modules()
    {
        var abstractionsRoot = Path.GetDirectoryName(SecurityAbstractionsProjectPath())!;

        var violations = Directory.EnumerateFiles(abstractionsRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifactPath(path))
            .Where(path => File.ReadAllText(path).Contains("CanDoItAll.Modules.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(abstractionsRoot, path))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    private static string MafProjectPath()
        => Path.Combine(
            FindRepoRoot(),
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Maf",
            "CanDoItAll.AgentFramework.Maf.csproj");

    private static string SecurityAbstractionsProjectPath()
        => Path.Combine(
            FindRepoRoot(),
            "src",
            "Foundation",
            "CanDoItAll.Security.Abstractions",
            "CanDoItAll.Security.Abstractions.csproj");

    private static IReadOnlyList<string> ReadProjectReferences(string projectPath)
    {
        Assert.True(File.Exists(projectPath), $"Missing project file: {projectPath}");
        return ProjectReferencePattern.Matches(File.ReadAllText(projectPath))
            .Select(match => match.Groups[1].Value)
            .ToList();
    }

    private static bool IsBuildArtifactPath(string path)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        return path
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Any(segment =>
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase));
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
