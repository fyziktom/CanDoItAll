using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed record DotNetResolvedSolutionContext(
    string ProductRoot,
    string SolutionFile,
    string SolutionFileAlias,
    IReadOnlyList<string> SolutionCandidatePaths,
    IReadOnlyList<string> RequiredProjectFiles,
    IReadOnlyList<string> TestProjectFiles,
    string WorkspaceAlias);

internal sealed class DotNetSolutionContextPathResolver
{
    public bool TryResolve(
        DotNetSolutionContext context,
        IDictionary<string, string> variables,
        out DotNetResolvedSolutionContext resolved,
        out string issue)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        resolved = null!;
        if (!TryResolveProductRoot(variables, out var productRoot, out issue) ||
            !TryResolveRelativePath(productRoot, context.SolutionFile, "solution.file", out var solutionFile, out issue) ||
            !TryResolveRelativePaths(productRoot, context.SolutionCandidateFiles, "solution.candidateFiles", out var solutionCandidates, out issue) ||
            !TryResolveRelativePaths(productRoot, context.RequiredProjectFiles, "requiredProjectFiles", out var requiredProjectFiles, out issue) ||
            !TryResolveRelativePaths(productRoot, context.TestProjectFiles, "testProjectFiles", out var testProjectFiles, out issue))
        {
            return false;
        }

        if (!solutionCandidates.Contains(solutionFile, StringComparer.OrdinalIgnoreCase))
        {
            solutionCandidates = [solutionFile, .. solutionCandidates];
        }

        resolved = new DotNetResolvedSolutionContext(
            productRoot,
            solutionFile,
            Alias(solutionFile),
            solutionCandidates,
            requiredProjectFiles,
            testProjectFiles,
            Alias(productRoot));
        issue = string.Empty;
        return true;
    }

    public static bool TryResolveRelativePath(
        string productRoot,
        string value,
        string fieldName,
        out string path,
        out string issue)
    {
        path = string.Empty;
        issue = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value.Trim()))
        {
            issue = $"The .NET solution context field '{fieldName}' must be a non-empty product-relative path.";
            return false;
        }

        var normalizedValue = value.Trim().Replace('\\', '/');
        if (string.Equals(normalizedValue, "external-target", StringComparison.OrdinalIgnoreCase) ||
            normalizedValue.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            issue = $"The .NET solution context field '{fieldName}' must not use an external-target alias; use a path relative to ProductRoot.";
            return false;
        }

        try
        {
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(productRoot));
            var root = EnsureTrailingSeparator(normalizedRoot);
            var candidate = Path.GetFullPath(Path.Combine(productRoot, value.Trim()));
            if (!string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
                !candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                issue = $"The .NET solution context field '{fieldName}' escapes ProductRoot.";
                return false;
            }

            path = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issue = $"The .NET solution context field '{fieldName}' is not a valid product-relative path.";
            return false;
        }
    }

    private static bool TryResolveRelativePaths(
        string productRoot,
        IReadOnlyList<string> values,
        string fieldName,
        out IReadOnlyList<string> paths,
        out string issue)
    {
        var resolved = new List<string>();
        foreach (var value in values)
        {
            if (!TryResolveRelativePath(productRoot, value, fieldName, out var path, out issue))
            {
                paths = [];
                return false;
            }

            if (!resolved.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                resolved.Add(path);
            }
        }

        paths = resolved;
        issue = string.Empty;
        return true;
    }

    private static bool TryResolveProductRoot(
        IDictionary<string, string> variables,
        out string productRoot,
        out string issue)
    {
        productRoot = ResolveVariable(variables, "ProductRoot");
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            productRoot = ResolveVariable(variables, "OutputRoot");
        }

        if (string.IsNullOrWhiteSpace(productRoot))
        {
            issue = "The .NET solution context requires ProductRoot or OutputRoot.";
            return false;
        }

        try
        {
            productRoot = Path.GetFullPath(productRoot);
            issue = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issue = "The .NET solution context has an invalid product root.";
            return false;
        }
    }

    private static string ResolveVariable(IDictionary<string, string> variables, string key)
        => variables.TryGetValue(key, out var value)
            ? value?.Trim() ?? string.Empty
            : string.Empty;

    private static string Alias(string path)
        => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(path) ?? string.Empty;

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
