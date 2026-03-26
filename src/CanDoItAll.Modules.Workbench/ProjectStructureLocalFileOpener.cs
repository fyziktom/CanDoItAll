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
    IWorkspacePathResolver workspacePathResolver,
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

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });

            logger.LogInformation("Opened project structure attachment locally from {Path}.", fullPath);
            return Task.FromResult(new ProjectStructureLocalFileOpenResult(true, "Opened locally."));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to open project structure attachment locally from {Path}.", fullPath);
            return Task.FromResult(new ProjectStructureLocalFileOpenResult(false, "Local open is unavailable for this file on the current host."));
        }
    }

    private bool TryResolveTrustedPath(ProjectStructureNode? node, out string fullPath, out string failureMessage)
    {
        fullPath = string.Empty;
        failureMessage = "Local open is only available for trusted managed files on this host.";
        if (!IsAvailable)
        {
            failureMessage = "Local open is not available on this host.";
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

        if (BlockedExtensions.Contains(Path.GetExtension(relativePath)))
        {
            failureMessage = "This file type is blocked from local launch.";
            return false;
        }

        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var managedRoot = Path.GetFullPath(workspacePathResolver.ResolveManagedFilesRoot());
        var candidatePath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
        var managedRootPrefix = managedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(managedRootPrefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidatePath, managedRoot, StringComparison.OrdinalIgnoreCase))
        {
            failureMessage = "Only files stored under managed project file roots can open locally.";
            return false;
        }

        if (!File.Exists(candidatePath))
        {
            failureMessage = "The managed file is no longer available on disk.";
            return false;
        }

        fullPath = candidatePath;
        return true;
    }

    private static string ResolveRelativePath(ProjectStructureNode node)
    {
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
