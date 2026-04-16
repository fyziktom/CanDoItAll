using System.Diagnostics;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public interface IProjectStructureLocalFileOpener
{
    bool IsAvailable { get; }

    bool CanOpen(ProjectStructureNode? node);

    Task<ProjectStructureLocalFileOpenResult> OpenAsync(ProjectStructureNode node, CancellationToken cancellationToken = default);
}

public sealed record ProjectStructureLocalFileOpenResult(bool IsSuccess, string Message);

public sealed class ProjectStructureLocalFileOpener(
    IWorkspacePathAccessGuard pathAccessGuard,
    ILogger<ProjectStructureLocalFileOpener> logger) : IProjectStructureLocalFileOpener
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".app",
        ".bat",
        ".cmd",
        ".com",
        ".exe",
        ".js",
        ".msi",
        ".ps1",
        ".reg",
        ".scr",
        ".sh",
        ".vbs"
    };

    public bool IsAvailable => OperatingSystem.IsWindows();

    public bool CanOpen(ProjectStructureNode? node)
        => TryResolveTrustedPath(node, out _, out _);

    public Task<ProjectStructureLocalFileOpenResult> OpenAsync(ProjectStructureNode node, CancellationToken cancellationToken = default)
    {
        if (!TryResolveTrustedPath(node, out var fullPath, out var failureMessage))
        {
            return Task.FromResult(new ProjectStructureLocalFileOpenResult(false, failureMessage));
        }

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new ProjectStructureLocalFileOpenResult(false, "Open in File Explorer is not available on this host."));
        }

        try
        {
            var processStartInfo = Directory.Exists(fullPath)
                ? new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{fullPath}\"",
                    UseShellExecute = true
                }
                : new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{fullPath}\"",
                    UseShellExecute = true
                };
            using var process = Process.Start(processStartInfo);

            logger.LogInformation("Opened project structure attachment in File Explorer from {Path}.", fullPath);
            return Task.FromResult(new ProjectStructureLocalFileOpenResult(true, "Opened in File Explorer."));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to open project structure attachment in File Explorer from {Path}.", fullPath);
            return Task.FromResult(new ProjectStructureLocalFileOpenResult(false, "Open in File Explorer is unavailable for this path on the current host."));
        }
    }

    private bool TryResolveTrustedPath(ProjectStructureNode? node, out string fullPath, out string failureMessage)
    {
        fullPath = string.Empty;
        failureMessage = "Open in File Explorer is only available for trusted managed files on this host.";
        if (!IsAvailable)
        {
            failureMessage = "Open in File Explorer is not available on this host.";
            return false;
        }

        if (node is null)
        {
            return false;
        }

        var relativePath = ResolveRelativePath(node);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var resolution = pathAccessGuard.ResolveManagedFilePath(relativePath);
        if (!resolution.IsSuccess)
        {
            failureMessage = "Only files or folders stored under managed project file roots can open in File Explorer.";
            return false;
        }

        var candidatePath = resolution.FullPath;
        if (Directory.Exists(candidatePath))
        {
            fullPath = candidatePath;
            return true;
        }

        if (BlockedExtensions.Contains(Path.GetExtension(candidatePath)))
        {
            failureMessage = "This file type is blocked from File Explorer launch.";
            return false;
        }

        if (!File.Exists(candidatePath))
        {
            failureMessage = "The managed file or folder is no longer available on disk.";
            return false;
        }

        fullPath = candidatePath;
        return true;
    }

    private static string ResolveRelativePath(ProjectStructureNode node)
    {
        if (StorageJson.TryParseReference(node.StorageObjectReferenceJson, out var storageReference) &&
            storageReference is not null &&
            storageReference.ProviderKind == StorageProviderKind.FileSystem &&
            storageReference.LocatorKind == StorageLocatorKind.RelativePath &&
            !string.IsNullOrWhiteSpace(storageReference.Locator))
        {
            return storageReference.Locator
                .Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
        }

        if (!string.IsNullOrWhiteSpace(node.MediaRelativePath))
        {
            return node.MediaRelativePath
                .Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
        }

        if (string.IsNullOrWhiteSpace(node.Route) ||
            !node.Route.StartsWith("/managed-files/", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return node.Route
            .Trim()
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
    }
}


