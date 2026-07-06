using System.Text.RegularExpressions;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryFoundationCheckpointTests
{
    private const int MaxFoundationFileLines = 220;

    [Fact]
    public void SB05_CP001_Generic_memory_foundation_files_are_bounded_and_cohesive()
    {
        var sourceFiles = EnumerateSourceFiles("src", "Memory")
            .Select(path => new
            {
                Path = path,
                Lines = File.ReadLines(path).Count()
            })
            .Where(file => file.Lines > MaxFoundationFileLines)
            .Select(file => $"{Path.GetRelativePath(RepoRoot, file.Path)} has {file.Lines} lines")
            .ToArray();

        Assert.True(
            sourceFiles.Length == 0,
            "Overgrown generic memory foundation files: " + string.Join("; ", sourceFiles));
    }

    [Fact]
    public void SB05_CP002_Generic_memory_foundation_has_no_native_or_infrastructure_dependencies()
    {
        var forbiddenPatterns = new[]
        {
            "CanDoItAll.Modules.CognitiveMemory",
            "CognitiveMemory",
            "Qdrant",
            "OpenAI",
            "OpenAi",
            "CanDoItAll.AgentFramework.Rag",
            "AppDbContext",
            "IDbContextFactory",
            "Microsoft.EntityFrameworkCore"
        };
        var violations = EnumerateSourceFiles("src", "Memory", "CanDoItAll.Memory.Abstractions")
            .Concat(EnumerateSourceFiles("src", "Memory", "CanDoItAll.Memory.Application"))
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
    public void SB06_CP001_Generic_memory_persistence_has_no_native_provider_dependencies()
    {
        var forbiddenPatterns = new[]
        {
            "CanDoItAll.Modules.CognitiveMemory",
            "CognitiveMemory",
            "Qdrant",
            "OpenAI",
            "OpenAi",
            "CanDoItAll.AgentFramework.Rag"
        };
        var violations = EnumerateSourceFiles("src", "Memory", "CanDoItAll.Memory.Persistence")
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
    public void SB05_CP003_Source_snapshot_contract_is_not_forked_inside_generic_memory()
    {
        var duplicateSnapshotPattern = new Regex(
            @"\b(?:record|record\s+class|class|interface)\s+(?:I)?MemorySourceSnapshot(?:Provider)?\b",
            RegexOptions.CultureInvariant);
        var matches = EnumerateSourceFiles("src", "Memory")
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

        Assert.Empty(matches);
    }

    [Fact]
    public void SB05_CP004_Zero_provider_selection_is_typed_no_dispatch_not_fallback()
    {
        var registry = new InMemoryMemoryProviderRegistry([]);
        var result = registry.SelectProvider(
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityId.Parse("context.query.sync")),
            MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.NoProviderConfigured, result.Status);
        Assert.Equal(MemoryProviderSelectionReason.None, result.Reason);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Empty(result.CandidateProviderIds);
    }

    private static string RepoRoot => FindRepoRoot();

    private static IEnumerable<string> EnumerateSourceFiles(params string[] segments)
    {
        var root = Path.Combine(new[] { RepoRoot }.Concat(segments).ToArray());
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
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
}
