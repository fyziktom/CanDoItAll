using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using System.Text.RegularExpressions;
using DispatchArtifactExpectation = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using ProjectStructureRequiredArtifactPath = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProjectStructureRequiredArtifactPath;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProjectStructureArtifactPathRules
{
    internal static IReadOnlyList<ProjectStructureRequiredArtifactPath> ResolveProjectStructureRequiredArtifactPaths(
        string? text,
        TryMapAbsoluteExternalPathToAlias tryMapAbsoluteExternalPathToAlias)
    {
        ArgumentNullException.ThrowIfNull(tryMapAbsoluteExternalPathToAlias);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var artifacts = new List<ProjectStructureRequiredArtifactPath>();
        foreach (Match match in Regex.Matches(
                     text,
                     @"Required file\s+`(?<file>[^`]+\.md)`\s+must be written at\s+`(?<path>[^`]+)`",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            AddProjectStructureRequiredArtifactPath(
                artifacts,
                match.Groups["file"].Value,
                match.Groups["path"].Value,
                tryMapAbsoluteExternalPathToAlias);
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"Governed path:\s*(?<path>external-target/[^\r\n\s`]+)",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var path = WorkspaceScopeDescriptor.NormalizeRelativePath(match.Groups["path"].Value);
            AddProjectStructureRequiredArtifactPath(
                artifacts,
                Path.GetFileName(path),
                path,
                tryMapAbsoluteExternalPathToAlias);
        }

        return artifacts
            .GroupBy(item => item.AliasPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static bool TryResolveProjectStructureExpectedArtifactPath(
        DispatchArtifactExpectation expectedArtifact,
        IReadOnlyList<ProjectStructureRequiredArtifactPath> requiredArtifactPaths,
        out string governedPath)
        => TryResolveProjectStructureExpectedArtifactPath(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            requiredArtifactPaths,
            out governedPath);

    internal static bool TryResolveProjectStructureExpectedArtifactPath(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        IReadOnlyList<ProjectStructureRequiredArtifactPath> requiredArtifactPaths,
        out string governedPath)
    {
        governedPath = string.Empty;
        if (requiredArtifactPaths.Count == 0)
        {
            return false;
        }

        var bestMatch = requiredArtifactPaths
            .Select(path => new
            {
                Path = path,
                Score = ScoreProjectStructureArtifactPathMatch(expectedArtifact, path.FileName)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Path.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (bestMatch is null)
        {
            return false;
        }

        governedPath = bestMatch.Path.AliasPath;
        return !string.IsNullOrWhiteSpace(governedPath);
    }

    internal static int ScoreProjectStructureArtifactPathMatch(
        DispatchArtifactExpectation expectedArtifact,
        string fileName)
        => ScoreProjectStructureArtifactPathMatch(
            ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation(expectedArtifact),
            fileName);

    internal static int ScoreProjectStructureArtifactPathMatch(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return 0;
        }

        var expectedTokens = TokenizeProjectStructureArtifactName(expectedArtifact.Title);
        var fileTokens = TokenizeProjectStructureArtifactName(Path.GetFileNameWithoutExtension(fileName));
        if (expectedTokens.Count == 0 || fileTokens.Count == 0)
        {
            return 0;
        }

        var matchedTokenCount = expectedTokens.Count(fileTokens.Contains);
        if (matchedTokenCount >= Math.Min(2, expectedTokens.Count))
        {
            return matchedTokenCount * 10 + (expectedTokens.Count == matchedTokenCount ? 5 : 0);
        }

        var expectedSlug = FileSafeSlugBuilder.Build(string.Join('-', expectedTokens));
        var fileSlug = FileSafeSlugBuilder.Build(string.Join('-', fileTokens));
        return !string.IsNullOrWhiteSpace(expectedSlug) &&
               !string.IsNullOrWhiteSpace(fileSlug) &&
               (fileSlug.Contains(expectedSlug, StringComparison.Ordinal) ||
                expectedSlug.Contains(fileSlug, StringComparison.Ordinal))
            ? 1
            : 0;
    }

    internal static bool ArtifactPathMatchesGovernedProjectStructurePath(
        string observedPath,
        string governedPath,
        TryMapAbsoluteExternalPathToAlias tryMapAbsoluteExternalPathToAlias)
    {
        return string.Equals(
            NormalizeProjectStructureArtifactPathForComparison(observedPath, tryMapAbsoluteExternalPathToAlias),
            NormalizeProjectStructureArtifactPathForComparison(governedPath, tryMapAbsoluteExternalPathToAlias),
            StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> TokenizeProjectStructureArtifactName(string value)
    {
        return ProcessArtifactTextMatchRules.TokenizeArtifactComparisonText(value)
            .Where(token => !ProcessArtifactTextMatchRules.IsArtifactTitleNoiseToken(token))
            .Where(token => !ProcessArtifactTextMatchRules.IsArtifactContentNoiseToken(token))
            .Where(token => !token.All(char.IsDigit))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void AddProjectStructureRequiredArtifactPath(
        ICollection<ProjectStructureRequiredArtifactPath> artifacts,
        string fileName,
        string aliasPath,
        TryMapAbsoluteExternalPathToAlias tryMapAbsoluteExternalPathToAlias)
    {
        var normalizedFileName = fileName.Trim();
        var normalizedPath = NormalizeProjectStructureArtifactPathForComparison(
            aliasPath,
            tryMapAbsoluteExternalPathToAlias);
        if (string.IsNullOrWhiteSpace(normalizedFileName) ||
            string.IsNullOrWhiteSpace(normalizedPath) ||
            artifacts.Any(item => string.Equals(item.AliasPath, normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        artifacts.Add(new ProjectStructureRequiredArtifactPath(normalizedFileName, normalizedPath));
    }

    private static string NormalizeProjectStructureArtifactPathForComparison(
        string path,
        TryMapAbsoluteExternalPathToAlias tryMapAbsoluteExternalPathToAlias)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (tryMapAbsoluteExternalPathToAlias(normalized, out var mappedAlias))
        {
            normalized = mappedAlias;
        }

        return ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(normalized);
    }
}
