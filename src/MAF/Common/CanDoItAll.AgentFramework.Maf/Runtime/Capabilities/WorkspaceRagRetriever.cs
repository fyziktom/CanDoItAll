using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceRagRetriever
{
    private readonly string workspaceRoot;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IPhysicalFileSystemPathPolicy workspacePathPolicy;

    public WorkspaceRagRetriever(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        workspacePathPolicy = physicalPathPolicyFactory.Create(workspaceRoot);
        this.workspaceRoot = workspacePathPolicy.RootPath;
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
    }

    public IReadOnlyList<string> ResolveSearchRoots(string configuredRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredRoot);

        string normalizedRoot;
        try
        {
            normalizedRoot = workspacePathPolicy.ResolveContainedPath(configuredRoot);
            workspacePathPolicy.EnsureSafePath(normalizedRoot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            return [];
        }
        if (workspaceScope.Kind != WorkspaceScopeKind.Project)
        {
            return IsSafeSearchRoot(normalizedRoot)
                ? [normalizedRoot]
                : [];
        }

        if (!workspacePathPolicy.PathComparer.Equals(
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
            .Where(path => IsSafeSearchRoot(path, allowMissingLeaf: true))
            .Distinct(workspacePathPolicy.PathComparer)
            .OrderBy(
                path => NormalizeEnumerationKey(Path.GetRelativePath(workspaceRoot, path)),
                StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
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
            .OrderBy(
                path => NormalizeEnumerationKey(Path.GetRelativePath(workspaceRoot, path)),
                StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
            .SelectMany(rootPath => WorkspaceSearchSupport.EnumerateSearchFiles(
                rootPath,
                extensions,
                excludedPaths))
            .Distinct(workspacePathPolicy.PathComparer)
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
            .ThenBy(
                item => NormalizeEnumerationKey(Path.GetRelativePath(workspaceRoot, item.Path)),
                StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
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
        => IsSafeSearchRoot(path, allowMissingLeaf: false);

    private bool IsSafeSearchRoot(string path, bool allowMissingLeaf)
    {
        if (!allowMissingLeaf && !File.Exists(path) && !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            workspacePathPolicy.EnsureSafePath(path, allowMissingLeaf);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string NormalizeEnumerationKey(string path)
        => path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
}
