using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceRagRetriever
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;

    public WorkspaceRagRetriever(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        this.workspaceRoot = Path.GetFullPath(workspaceRoot);
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
    }

    public IReadOnlyList<string> ResolveSearchRoots(string configuredRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredRoot);

        var normalizedRoot = Path.GetFullPath(configuredRoot);
        if (workspaceScope.Kind != WorkspaceScopeKind.Project)
        {
            return IsSafeSearchRoot(normalizedRoot)
                ? [normalizedRoot]
                : [];
        }

        if (!PathComparer.Equals(
                Path.TrimEndingDirectorySeparator(normalizedRoot),
                Path.TrimEndingDirectorySeparator(workspaceRoot)))
        {
            var relativeRoot = Path.GetRelativePath(workspaceRoot, normalizedRoot)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (ManagedProjectMediaPath.IsProjectMediaPath(relativeRoot) &&
                !ManagedProjectMediaPath.IsForProject(relativeRoot, workspaceScope.Key))
            {
                return [];
            }

            return IsSafeSearchRoot(normalizedRoot)
                ? [normalizedRoot]
                : [];
        }

        return ManagedProjectMediaPath.ResolveTextAssetRelativeRoots(workspaceScope.Key)
            .Select(relativePath => Path.Combine(
                workspaceRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .Order(PathComparer)
            .ToArray();
    }

    public async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(
        IReadOnlyList<string> rootPaths,
        string query,
        int maxResults,
        int maxFilesToScan,
        int minQueryTerms,
        int minMatchedTerms,
        int minScore,
        HashSet<string>? extensions,
        HashSet<string>? excludedPaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rootPaths);

        var terms = WorkspaceSearchSupport.TokenizeRagQuery(query);
        if (!WorkspaceSearchSupport.HasEnoughRagSignal(terms, minQueryTerms))
        {
            return [];
        }

        var effectiveMinMatchedTerms = Math.Min(minMatchedTerms, terms.Count);
        var files = rootPaths
            .Where(IsSafeSearchRoot)
            .Order(PathComparer)
            .SelectMany(rootPath => WorkspaceSearchSupport.EnumerateSearchFiles(
                rootPath,
                extensions,
                excludedPaths))
            .Distinct(PathComparer)
            .Take(maxFilesToScan)
            .ToArray();
        var scoredResults = new List<(int Score, int MatchedTerms, string Path, string Snippet)>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string text;
            try
            {
                text = await File.ReadAllTextAsync(file, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var score = 0;
            var matchedTerms = 0;
            foreach (var term in terms)
            {
                var occurrences = WorkspaceSearchSupport.CountWholeTermOccurrences(text, term);
                if (occurrences <= 0)
                {
                    continue;
                }

                score += occurrences;
                matchedTerms++;
            }

            if (matchedTerms < effectiveMinMatchedTerms || score < minScore)
            {
                continue;
            }

            scoredResults.Add((
                score,
                matchedTerms,
                file,
                WorkspaceSearchSupport.BuildSearchSnippet(text, terms)));
        }

        return scoredResults
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.MatchedTerms)
            .ThenBy(item => item.Path, PathComparer)
            .Take(maxResults)
            .Select(item => new TextSearchProvider.TextSearchResult
            {
                SourceName = Path.GetRelativePath(workspaceRoot, item.Path),
                SourceLink = item.Path,
                Text = item.Snippet
            })
            .ToArray();
    }

    private bool IsSafeSearchRoot(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return false;
        }

        var currentPath = Path.GetFullPath(path);
        while (!PathComparer.Equals(
                   Path.TrimEndingDirectorySeparator(currentPath),
                   Path.TrimEndingDirectorySeparator(workspaceRoot)))
        {
            if (!IsWithinWorkspace(currentPath))
            {
                return false;
            }

            try
            {
                if (File.GetAttributes(currentPath).HasFlag(FileAttributes.ReparsePoint))
                {
                    return false;
                }
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException)
            {
                return false;
            }

            var parentPath = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrWhiteSpace(parentPath) ||
                PathComparer.Equals(parentPath, currentPath))
            {
                return false;
            }

            currentPath = parentPath;
        }

        return true;
    }

    private bool IsWithinWorkspace(string path)
    {
        var normalizedWorkspaceRoot = Path.TrimEndingDirectorySeparator(workspaceRoot);
        var normalizedPath = Path.TrimEndingDirectorySeparator(path);
        return PathComparer.Equals(normalizedPath, normalizedWorkspaceRoot) ||
               normalizedPath.StartsWith(
                   normalizedWorkspaceRoot + Path.DirectorySeparatorChar,
                   OperatingSystem.IsWindows()
                       ? StringComparison.OrdinalIgnoreCase
                       : StringComparison.Ordinal);
    }
}
