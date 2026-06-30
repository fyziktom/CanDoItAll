namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryStagedSourceItemKind
{
    SemanticSource = 0,
    AssetNode = 1
}

public sealed record CognitiveMemoryStagedSourceManifest(
    string ProjectKey,
    string ReadOnlyRoot,
    IReadOnlyList<CognitiveMemoryStagedSourceStage> Stages,
    IReadOnlyList<string> ExcludedPaths);

public sealed record CognitiveMemoryStagedSourceStage(
    string StageId,
    string Title,
    IReadOnlyList<CognitiveMemoryStagedSourcePath> Sources);

public sealed record CognitiveMemoryStagedSourcePath(
    CognitiveMemoryStagedSourceItemKind Kind,
    string Path,
    string DisplayName);

public sealed record CognitiveMemoryStagedSourceValidationResult(
    bool IsValid,
    IReadOnlyList<string> Violations,
    IReadOnlyList<CognitiveMemoryResolvedStagedSourcePath> Sources,
    IReadOnlyList<string> ExcludedPaths);

public sealed record CognitiveMemoryResolvedStagedSourcePath(
    string StageId,
    string Title,
    CognitiveMemoryStagedSourceItemKind Kind,
    string FullPath,
    string DisplayName);

public static class CognitiveMemoryStagedSourceManifestValidator
{
    public static CognitiveMemoryStagedSourceValidationResult Validate(CognitiveMemoryStagedSourceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var violations = new List<string>();
        var sources = new List<CognitiveMemoryResolvedStagedSourcePath>();
        var excludedPaths = new List<string>();

        var projectKey = NormalizeRequired(manifest.ProjectKey, nameof(manifest.ProjectKey), violations);
        var root = NormalizeRequired(manifest.ReadOnlyRoot, nameof(manifest.ReadOnlyRoot), violations);
        if (string.IsNullOrWhiteSpace(projectKey) || string.IsNullOrWhiteSpace(root))
        {
            return new CognitiveMemoryStagedSourceValidationResult(false, violations, sources, excludedPaths);
        }

        var rootFullPath = Path.GetFullPath(root);
        if (!Directory.Exists(rootFullPath))
        {
            violations.Add($"Read-only root '{rootFullPath}' does not exist.");
        }

        foreach (var excludedPath in manifest.ExcludedPaths)
        {
            var resolved = ResolveUnderRoot(rootFullPath, excludedPath, nameof(manifest.ExcludedPaths), violations);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                excludedPaths.Add(resolved);
            }
        }

        foreach (var stage in manifest.Stages)
        {
            var stageId = NormalizeRequired(stage.StageId, nameof(stage.StageId), violations);
            var title = NormalizeRequired(stage.Title, nameof(stage.Title), violations);
            if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            foreach (var source in stage.Sources)
            {
                var resolved = ResolveUnderRoot(rootFullPath, source.Path, nameof(source.Path), violations);
                if (string.IsNullOrWhiteSpace(resolved))
                {
                    continue;
                }

                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    violations.Add($"Source path '{resolved}' does not exist.");
                    continue;
                }

                if (excludedPaths.Any(excludedPath => IsSameOrChildPath(resolved, excludedPath)))
                {
                    violations.Add($"Source path '{resolved}' is excluded by the staged ingestion manifest.");
                    continue;
                }

                sources.Add(new CognitiveMemoryResolvedStagedSourcePath(
                    stageId,
                    title,
                    source.Kind,
                    resolved,
                    string.IsNullOrWhiteSpace(source.DisplayName) ? Path.GetFileName(resolved) : source.DisplayName.Trim()));
            }
        }

        return new CognitiveMemoryStagedSourceValidationResult(
            violations.Count == 0,
            violations,
            sources,
            excludedPaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string NormalizeRequired(string value, string parameterName, ICollection<string> violations)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            violations.Add($"{parameterName} is required.");
            return string.Empty;
        }

        return value.Trim();
    }

    private static string ResolveUnderRoot(string rootFullPath, string value, string parameterName, ICollection<string> violations)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            violations.Add($"{parameterName} contains an empty path.");
            return string.Empty;
        }

        var candidate = Path.IsPathRooted(value)
            ? value
            : Path.Combine(rootFullPath, value);
        var fullPath = Path.GetFullPath(candidate);
        if (!IsSameOrChildPath(fullPath, rootFullPath))
        {
            violations.Add($"Path '{fullPath}' is outside the read-only root '{rootFullPath}'.");
            return string.Empty;
        }

        return fullPath;
    }

    private static bool IsSameOrChildPath(string candidate, string ancestor)
    {
        var normalizedCandidate = NormalizePath(candidate);
        var normalizedAncestor = NormalizePath(ancestor);
        return string.Equals(normalizedCandidate, normalizedAncestor, StringComparison.OrdinalIgnoreCase) ||
            normalizedCandidate.StartsWith(normalizedAncestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string value)
        => Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
