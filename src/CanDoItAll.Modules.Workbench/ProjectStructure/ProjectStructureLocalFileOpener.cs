using System.Diagnostics;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
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
    private sealed record LocalPathCandidate(string Path);

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
        failureMessage = "Open in File Explorer is only available for trusted managed files, local files, or folders on this host.";
        if (!IsAvailable)
        {
            failureMessage = "Open in File Explorer is not available on this host.";
            return false;
        }

        if (node is null)
        {
            return false;
        }

        if (TryResolveMetadataPath(node, out fullPath, out failureMessage))
        {
            return true;
        }

        var relativePath = ResolveRelativePath(node);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var resolution = TryResolveTrustedArtifactPath(relativePath, out var artifactResolution)
            ? artifactResolution
            : pathAccessGuard.ResolveManagedFilePath(relativePath);
        if (!resolution.IsSuccess)
        {
            failureMessage = "Only files or folders stored under managed files or managed artifact roots can open in File Explorer.";
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

    private bool TryResolveMetadataPath(ProjectStructureNode node, out string fullPath, out string failureMessage)
    {
        fullPath = string.Empty;
        failureMessage = "Open in File Explorer requires an existing local file or folder path.";

        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        foreach (var candidate in ResolveMetadataPathCandidates(node, metadata))
        {
            if (!TryResolveLocalPath(candidate.Path, out var candidatePath))
            {
                continue;
            }

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

            if (File.Exists(candidatePath))
            {
                fullPath = candidatePath;
                return true;
            }
        }

        return false;
    }

    private bool TryResolveLocalPath(string value, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || LooksLikeUrl(value))
        {
            return false;
        }

        try
        {
            var trimmedValue = value.Trim();
            if (Path.IsPathRooted(trimmedValue))
            {
                fullPath = Path.GetFullPath(trimmedValue);
                return true;
            }

            var resolution = pathAccessGuard.ResolveWorkspacePath(trimmedValue);
            if (!resolution.IsSuccess)
            {
                return false;
            }

            fullPath = resolution.FullPath;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    private static IEnumerable<LocalPathCandidate> ResolveMetadataPathCandidates(ProjectStructureNode node, ProjectObjectMetadataEnvelope metadata)
    {
        switch (node.ObjectType)
        {
            case ProjectObjectType.File:
                if (!string.IsNullOrWhiteSpace(metadata.File?.ExternalPath))
                {
                    yield return new LocalPathCandidate(metadata.File.ExternalPath);
                }

                break;
            case ProjectObjectType.Repository:
                if (!string.IsNullOrWhiteSpace(metadata.Repository?.LocalPath) &&
                    !string.IsNullOrWhiteSpace(metadata.Repository.RelativePath))
                {
                    yield return new LocalPathCandidate(
                        CombinePath(metadata.Repository.LocalPath, metadata.Repository.RelativePath));
                }

                if (!string.IsNullOrWhiteSpace(metadata.Repository?.LocalPath))
                {
                    yield return new LocalPathCandidate(metadata.Repository.LocalPath);
                }

                break;
            case ProjectObjectType.Infrastructure:
                if (!string.IsNullOrWhiteSpace(metadata.Infrastructure?.WorkingDirectory))
                {
                    yield return new LocalPathCandidate(metadata.Infrastructure.WorkingDirectory);
                }

                if (!string.IsNullOrWhiteSpace(metadata.Infrastructure?.FolderPath))
                {
                    yield return new LocalPathCandidate(metadata.Infrastructure.FolderPath);
                }

                break;
        }
    }

    private static string CombinePath(string rootPath, string relativePath)
        => Path.IsPathRooted(relativePath.Trim())
            ? relativePath.Trim()
            : Path.Combine(rootPath.Trim(), relativePath.Trim());

    private static bool LooksLikeUrl(string value)
    {
        var trimmedValue = value.Trim();
        if (Path.IsPathRooted(trimmedValue))
        {
            return false;
        }

        return Uri.TryCreate(trimmedValue, UriKind.Absolute, out var uri) &&
               !string.IsNullOrWhiteSpace(uri.Scheme) &&
               !string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryResolveTrustedArtifactPath(string relativePath, out WorkspacePathAccessResult resolution)
    {
        resolution = WorkspacePathAccessResult.Failure("A trusted artifact path is required.");
        var normalizedPath = relativePath
            .Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        var firstSegment = normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (!string.Equals(firstSegment, "artifacts", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resolution = pathAccessGuard.ResolveWorkspacePath(normalizedPath);
        return resolution.IsSuccess;
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


