using CanDoItAll.Memory.SourceGateway;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CanDoItAll.Memory.Tests.Persistence;

public sealed class MemorySourceGatewayHardeningCheckpointTests
{
    [Fact]
    public void CP001_Provider_driver_projects_do_not_reference_source_modules()
    {
        var violations = EnumerateProjectFiles("src", "Memory")
            .Where(path => path.Contains("CanDoItAll.Memory.Http", StringComparison.Ordinal) ||
                           path.Contains("CanDoItAll.Memory.Mcp", StringComparison.Ordinal))
            .SelectMany(ReadProjectReferences)
            .Where(reference => reference.Include.Contains($"{Path.DirectorySeparatorChar}Modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                                reference.Include.Contains("CanDoItAll.Modules.", StringComparison.Ordinal))
            .Select(reference => $"{Path.GetRelativePath(RepoRoot, reference.ProjectPath)} references {reference.Include}")
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CP002_Provider_driver_code_does_not_read_source_modules_or_app_db_context()
    {
        var forbiddenPatterns = new[]
        {
            "AppDbContext",
            "IDbContextFactory",
            "CanDoItAll.Modules.Workbench",
            "CanDoItAll.Modules.Processes",
            "CanDoItAll.Modules.AgentFramework",
            "CanDoItAll.Modules.CrmHr",
            "CanDoItAll.Modules.Resources",
            "ProjectResource",
            "Party",
            "Opportunity",
            "IProjectStructureSourceSnapshotProvider",
            "IProcessRuntimeEvidenceSourceProvider",
            "IWorkflowRuntimeEvidenceSourceProvider",
            "ICrmHrSourceSnapshotProvider",
            "IResourceSourceSnapshotProvider",
            "IManualSourceSnapshotProvider"
        };

        var violations = EnumerateSourceFiles("src", "Memory", "CanDoItAll.Memory.Http")
            .Concat(EnumerateSourceFiles("src", "Memory", "CanDoItAll.Memory.Mcp"))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    LineNumber = index + 1,
                    Line = line
                }))
            .SelectMany(candidate => forbiddenPatterns
                .Where(pattern => candidate.Line.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepoRoot, candidate.Path)}:{candidate.LineNumber} contains {pattern}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CP003_Source_snapshot_contract_family_is_not_duplicated_outside_source_gateway_abstractions()
    {
        var canonicalDirectory = Path.Combine(
            RepoRoot,
            "src",
            "Memory",
            "CanDoItAll.Memory.SourceGateway.Abstractions");
        var genericProtocolPath = Path.Combine(
            RepoRoot,
            "src",
            "Memory",
            "CanDoItAll.Memory.Abstractions",
            "MemoryProtocolContexts.cs");
        var duplicateSnapshotPattern = new Regex(
            @"\b(?:record(?:\s+struct|\s+class)?|class|interface|enum)\s+(?:I)?(?:MemorySourceSnapshot|MemorySourceSnapshotManifest|MemorySourceSnapshotCursor|MemorySourceSnapshotCursorDescriptor|MemorySourceSnapshotPage|MemorySourceItem|MemorySourceItemId|MemorySourceItemKey|MemorySourceProvenance|MemorySourcePermissionContext|MemorySourceLayoutMetadata|MemorySourceLink|MemorySourceReference|MemorySourceStorageReference|CrmHrSourceSnapshotRequest|ResourceSourceSnapshotRequest|ManualSourceSnapshotRequest|I\w+SourceSnapshotProvider)\b",
            RegexOptions.CultureInvariant);
        var violations = EnumerateSourceFiles("src")
            .Where(path => !Path.GetFullPath(path).StartsWith(
                canonicalDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFullPath(path).Equals(genericProtocolPath, StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    LineNumber = index + 1,
                    Line = line
                }))
            .Where(candidate => duplicateSnapshotPattern.IsMatch(candidate.Line))
            .Select(candidate => $"{Path.GetRelativePath(RepoRoot, candidate.Path)}:{candidate.LineNumber}")
            .ToArray();

        Assert.Empty(violations);
    }

    private static string RepoRoot => FindRepoRoot();

    private static IEnumerable<string> EnumerateProjectFiles(params string[] segments)
    {
        var root = Path.Combine(new[] { RepoRoot }.Concat(segments).ToArray());
        return Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateSourceFiles(params string[] segments)
    {
        var root = Path.Combine(new[] { RepoRoot }.Concat(segments).ToArray());
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<ProjectReference> ReadProjectReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        return document
            .Descendants("ProjectReference")
            .Select(element => new ProjectReference(
                projectPath,
                element.Attribute("Include")?.Value ?? string.Empty));
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

        throw new DirectoryNotFoundException("Could not locate repository root containing CanDoItAll.slnx.");
    }

    private sealed record ProjectReference(string ProjectPath, string Include);
}
