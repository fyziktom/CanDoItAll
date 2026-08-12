using CanDoItAll.AgentFramework.Core;

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

internal sealed class ProcessProductFilesystemInspector(IWorkspaceFileService workspaceFiles)
{
    private const int MaximumProductFileCount = 400;

    internal ProcessProductPathInspection InspectPath(string productRoot, string path)
    {
        try
        {
            var result = workspaceFiles.StatPath(path, productRoot);
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

    internal ProcessProductTextInspection ReadText(string productRoot, string path)
    {
        try
        {
            var result = workspaceFiles.ReadTextFile(
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

    internal ProductRootInspection InspectProductRoot(string productRoot)
    {
        var rootInspection = InspectPath(productRoot, productRoot);
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
            var result = workspaceFiles.ListFiles(
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
}
