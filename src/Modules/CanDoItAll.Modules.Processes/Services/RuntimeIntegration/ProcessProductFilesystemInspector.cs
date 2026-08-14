using CanDoItAll.AgentFramework.Core;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal enum ProcessProductPathState
{
    Missing,
    File,
    Directory,
    Unavailable
}

internal sealed record ProcessProductPathInspection(ProcessProductPathState State);

internal sealed record ProcessProductTextInspection(bool Succeeded, string Content);

internal sealed class ProcessProductFilesystemInspector
{
    private const int MaximumProductFileCount = 400;

    private readonly WorkspaceFileInspectionScopeFactory workspaceFileInspectionScopeFactory;

    public ProcessProductFilesystemInspector(
        WorkspaceFileInspectionScopeFactory workspaceFileInspectionScopeFactory)
    {
        this.workspaceFileInspectionScopeFactory = workspaceFileInspectionScopeFactory ??
            throw new ArgumentNullException(nameof(workspaceFileInspectionScopeFactory));
    }

    internal ProcessProductPathInspection InspectPath(
        IReadOnlyDictionary<string, string> launchVariables,
        string productRoot,
        string path)
    {
        return TryCreateInspectionScope(
            launchVariables,
            productRoot,
            path,
            out var inspectionFiles)
            ? InspectResolvedPath(inspectionFiles, productRoot, path)
            : new ProcessProductPathInspection(ProcessProductPathState.Unavailable);
    }

    private static ProcessProductPathInspection InspectResolvedPath(
        IWorkspaceFileInspectionService inspectionFiles,
        string productRoot,
        string path)
    {
        try
        {
            var result = inspectionFiles.StatPath(path, productRoot);
            if (result.Succeeded && result.Exists)
            {
                return new ProcessProductPathInspection(
                    string.Equals(result.PathKind, "file", StringComparison.OrdinalIgnoreCase)
                        ? ProcessProductPathState.File
                        : ProcessProductPathState.Directory);
            }

            return new ProcessProductPathInspection(
                string.Equals(result.Receipt.Outcome, "Failed", StringComparison.OrdinalIgnoreCase) &&
                !result.Exists
                    ? ProcessProductPathState.Missing
                    : ProcessProductPathState.Unavailable);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
        {
            return new ProcessProductPathInspection(ProcessProductPathState.Unavailable);
        }
    }

    internal ProcessProductTextInspection ReadText(
        IReadOnlyDictionary<string, string> launchVariables,
        string productRoot,
        string path)
    {
        if (!TryCreateInspectionScope(
                launchVariables,
                productRoot,
                path,
                out var inspectionFiles))
        {
            return new ProcessProductTextInspection(false, string.Empty);
        }

        try
        {
            var result = inspectionFiles.ReadTextFile(
                path,
                WorkspaceFileLimits.MaxTextReadCharacters,
                productRoot);
            return result.Succeeded && !result.IsTruncated
                ? new ProcessProductTextInspection(true, result.Content)
                : new ProcessProductTextInspection(false, string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
        {
            return new ProcessProductTextInspection(false, string.Empty);
        }
    }

    internal ProductRootInspection InspectProductRoot(
        IReadOnlyDictionary<string, string> launchVariables,
        string productRoot)
    {
        if (!TryCreateInspectionScope(
                launchVariables,
                productRoot,
                productRoot,
                out var inspectionFiles))
        {
            return new ProductRootInspection(false, "the product root could not be inspected safely");
        }

        var rootInspection = InspectResolvedPath(inspectionFiles, productRoot, productRoot);
        if (rootInspection.State == ProcessProductPathState.Missing)
        {
            return new ProductRootInspection(false, "the directory does not exist");
        }

        if (rootInspection.State != ProcessProductPathState.Directory)
        {
            return new ProductRootInspection(false, "the product root could not be inspected safely");
        }

        try
        {
            var result = inspectionFiles.ListFiles(
                productRoot,
                "*",
                MaximumProductFileCount,
                productRoot);
            if (!result.Succeeded)
            {
                return new ProductRootInspection(false, "the product root could not be inspected safely");
            }

            if (result.Entries.Any(entry =>
                    string.Equals(entry.PathKind, "file", StringComparison.OrdinalIgnoreCase) &&
                    ProcessProductRootResolver.IsProductFileReference(entry.RelativePath)))
            {
                return new ProductRootInspection(true, string.Empty);
            }

            return result.IsTruncated
                ? new ProductRootInspection(false, "the product root could not be inspected safely")
                : new ProductRootInspection(false, "no product files were found");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException or NotSupportedException)
        {
            return new ProductRootInspection(false, "the product root could not be inspected safely");
        }
    }

    private bool TryCreateInspectionScope(
        IReadOnlyDictionary<string, string> launchVariables,
        string productRoot,
        string path,
        out IWorkspaceFileInspectionService inspectionFiles)
    {
        inspectionFiles = null!;

        var versionedAliases = new List<string>(2);
        if (!TryAddVersionedAlias(productRoot, versionedAliases) ||
            !TryAddVersionedAlias(path, versionedAliases))
        {
            return false;
        }

        if (versionedAliases.Count == 0 ||
            !ExternalTargetAliasCodec.IsVersionedAlias(productRoot))
        {
            return false;
        }

        try
        {
            var bindings = ProcessExternalTargetRootBindingResolver.Resolve(
                launchVariables,
                versionedAliases);
            inspectionFiles = workspaceFileInspectionScopeFactory.Create(bindings);
            return true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryAddVersionedAlias(string value, ICollection<string> aliases)
    {
        if (!ExternalTargetAliasCodec.IsVersionedAlias(value))
        {
            return true;
        }

        var normalizedAlias = ExternalTargetAliasCodec.NormalizeVersionedAlias(value);
        if (normalizedAlias is null)
        {
            return false;
        }

        if (!aliases.Contains(normalizedAlias, ExternalTargetAliasCodec.EqualityComparer))
        {
            aliases.Add(normalizedAlias);
        }

        return true;
    }
}
