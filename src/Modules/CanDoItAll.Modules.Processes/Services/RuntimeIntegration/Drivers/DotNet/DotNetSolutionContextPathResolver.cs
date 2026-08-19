using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Processes;

internal sealed record DotNetResolvedSolutionContext(
    string ProductRoot,
    string SolutionFile,
    string SolutionFileAlias,
    IReadOnlyList<string> SolutionCandidatePaths,
    IReadOnlyList<string> RequiredProjectFiles,
    IReadOnlyList<string> TestProjectFiles,
    string WorkspaceAlias);

internal sealed class DotNetSolutionContextPathResolver(
    IExternalTargetPathRegistry externalTargetPathRegistry,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory)
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
        if (!TryResolveProductRoot(variables, out var productRootPolicy, out issue) ||
            !TryResolveRelativePath(productRootPolicy, context.SolutionFile, "solution.file", out var solutionFile, out issue) ||
            !TryResolveRelativePaths(productRootPolicy, context.SolutionCandidateFiles, "solution.candidateFiles", out var solutionCandidates, out issue) ||
            !TryResolveRelativePaths(productRootPolicy, context.RequiredProjectFiles, "requiredProjectFiles", out var requiredProjectFiles, out issue) ||
            !TryResolveRelativePaths(productRootPolicy, context.TestProjectFiles, "testProjectFiles", out var testProjectFiles, out issue))
        {
            return false;
        }

        string productRoot = productRootPolicy.RootPath;

        if (!solutionCandidates.Contains(solutionFile, productRootPolicy.PathComparer))
        {
            solutionCandidates = [solutionFile, .. solutionCandidates];
        }

        solutionCandidates = IncludeSupportedSolutionFormatAlternatives(
            solutionCandidates,
            productRootPolicy.PathComparer);

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

    internal static IReadOnlyList<string> IncludeSupportedSolutionFormatAlternatives(
        IEnumerable<string> candidates,
        StringComparer pathComparer)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var resolved = new List<string>();
        foreach (var candidate in candidates)
        {
            Add(candidate);

            var alternativeExtension = Path.GetExtension(candidate).ToLowerInvariant() switch
            {
                ".sln" => ".slnx",
                ".slnx" => ".sln",
                _ => string.Empty
            };
            if (!string.IsNullOrEmpty(alternativeExtension))
            {
                Add(Path.ChangeExtension(candidate, alternativeExtension));
            }
        }

        return resolved;

        void Add(string candidate)
        {
            if (!resolved.Contains(candidate, pathComparer))
            {
                resolved.Add(candidate);
            }
        }
    }

    public bool TryResolveRelativePath(
        string productRoot,
        string value,
        string fieldName,
        out string path,
        out string issue)
    {
        if (!TryCreateProductRootPolicy(productRoot, out var productRootPolicy, out issue))
        {
            path = string.Empty;
            return false;
        }

        return TryResolveRelativePath(productRootPolicy, value, fieldName, out path, out issue);
    }

    private static bool TryResolveRelativePath(
        IPhysicalFileSystemPathPolicy productRootPolicy,
        string value,
        string fieldName,
        out string path,
        out string issue)
    {
        path = string.Empty;
        issue = string.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            PhysicalPathSyntaxClassifier.Classify(value.Trim()) != PhysicalPathSyntax.Relative)
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
            path = productRootPolicy.ResolveContainedPath(value.Trim());
            return true;
        }
        catch (PhysicalPathValidationException exception) when (
            exception.ErrorCode == PhysicalPathValidationErrorCode.OutsideRoot)
        {
            issue = $"The .NET solution context field '{fieldName}' escapes ProductRoot.";
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException or PhysicalPathValidationException)
        {
            issue = $"The .NET solution context field '{fieldName}' is not a valid product-relative path.";
            return false;
        }
    }

    private static bool TryResolveRelativePaths(
        IPhysicalFileSystemPathPolicy productRootPolicy,
        IReadOnlyList<string> values,
        string fieldName,
        out IReadOnlyList<string> paths,
        out string issue)
    {
        var resolved = new List<string>();
        foreach (var value in values)
        {
            if (!TryResolveRelativePath(productRootPolicy, value, fieldName, out var path, out issue))
            {
                paths = [];
                return false;
            }

            if (!resolved.Contains(path, productRootPolicy.PathComparer))
            {
                resolved.Add(path);
            }
        }

        paths = resolved;
        issue = string.Empty;
        return true;
    }

    private bool TryResolveProductRoot(
        IDictionary<string, string> variables,
        out IPhysicalFileSystemPathPolicy productRootPolicy,
        out string issue)
    {
        string productRoot = ResolveVariable(variables, "ProductRoot");
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            productRoot = ResolveVariable(variables, "OutputRoot");
        }

        if (string.IsNullOrWhiteSpace(productRoot))
        {
            productRootPolicy = null!;
            issue = "The .NET solution context requires ProductRoot or OutputRoot.";
            return false;
        }

        return TryCreateProductRootPolicy(productRoot, out productRootPolicy, out issue);
    }

    internal bool TryCreateProductRootPolicy(
        string productRoot,
        out IPhysicalFileSystemPathPolicy productRootPolicy,
        out string issue)
    {
        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(
                productRoot,
                ".NET solution context ProductRoot");
            if (!Path.IsPathRooted(productRoot))
            {
                productRootPolicy = null!;
                issue = "The .NET solution context requires ProductRoot or OutputRoot to be an absolute path.";
                return false;
            }

            productRootPolicy = physicalPathPolicyFactory.Create(Path.GetFullPath(productRoot));
            issue = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException or PhysicalPathValidationException)
        {
            productRootPolicy = null!;
            issue = "The .NET solution context has an invalid product root.";
            return false;
        }
    }

    private static string ResolveVariable(IDictionary<string, string> variables, string key)
        => variables.TryGetValue(key, out var value)
            ? value?.Trim() ?? string.Empty
            : string.Empty;

    private string Alias(string path)
        => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            path,
            externalTargetPathRegistry) ?? string.Empty;

}
