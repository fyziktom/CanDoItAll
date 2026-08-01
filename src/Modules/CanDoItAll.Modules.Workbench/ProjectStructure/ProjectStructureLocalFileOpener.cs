using CanDoItAll.FileTools.Desktop;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public interface IProjectStructureLocalFileOpener
{
    bool IsAvailable { get; }

    bool CanOpen(ProjectStructureNode? node);

    bool CanOpenInPreferredApplication(ProjectStructureNode? node);

    Task<ProjectStructureLocalFileOpenResult> OpenAsync(ProjectStructureNode node, CancellationToken cancellationToken = default);

    Task<ProjectStructureLocalFileOpenResult> OpenInPreferredApplicationAsync(
        ProjectStructureNode node,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectStructureLocalFileOpenResult(bool IsSuccess, string Message);

public sealed class ProjectStructureLocalFileOpener(
    IWorkspacePathAccessGuard pathAccessGuard,
    FileSystemStoragePathPolicy fileSystemPathPolicy,
    IFileApplicationPreferenceService applicationPreferences,
    IDesktopFileLauncher desktopFileLauncher,
    ILogger<ProjectStructureLocalFileOpener> logger) : IProjectStructureLocalFileOpener
{
    private sealed record LocalPathCandidate(string Path);

    public bool IsAvailable => desktopFileLauncher.IsAvailable;

    public bool CanOpen(ProjectStructureNode? node)
        => TryResolveTrustedPath(node, out _, out _);

    public bool CanOpenInPreferredApplication(ProjectStructureNode? node)
        => TryResolveTrustedPath(node, out string fullPath, out _) &&
            File.Exists(fullPath) &&
            (FileToolsExternalOpenPolicy.IsAllowedSystemAssociatedFile(fullPath) ||
             HasExplicitPreferredApplication(fullPath));

    public Task<ProjectStructureLocalFileOpenResult> OpenAsync(
        ProjectStructureNode node,
        CancellationToken cancellationToken = default)
        => LaunchAsync(
            node,
            DesktopFileLaunchOperation.OpenContainingFolder,
            usePreferredApplication: false,
            cancellationToken);

    public Task<ProjectStructureLocalFileOpenResult> OpenInPreferredApplicationAsync(
        ProjectStructureNode node,
        CancellationToken cancellationToken = default)
        => LaunchAsync(
            node,
            DesktopFileLaunchOperation.Open,
            usePreferredApplication: true,
            cancellationToken);

    private async Task<ProjectStructureLocalFileOpenResult> LaunchAsync(
        ProjectStructureNode node,
        DesktopFileLaunchOperation operation,
        bool usePreferredApplication,
        CancellationToken cancellationToken)
    {
        if (!TryResolveTrustedPath(node, out var fullPath, out var failureMessage))
        {
            return new ProjectStructureLocalFileOpenResult(false, failureMessage);
        }

        if (usePreferredApplication && Directory.Exists(fullPath))
        {
            return new ProjectStructureLocalFileOpenResult(
                false,
                "This item cannot be opened in an external application.");
        }

        FileApplicationPreference? preference = null;
        if (usePreferredApplication)
        {
            try
            {
                preference = applicationPreferences.ResolveForFile(Path.GetFileName(fullPath));
            }
            catch (Exception exception) when (
                exception is ArgumentException
                    or IOException
                    or InvalidOperationException
                    or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    "Preferred application resolution failed. NodeId={NodeId} Extension={Extension} FailureType={FailureType}.",
                    node.Id,
                    Path.GetExtension(fullPath),
                    exception.GetType().Name);
                return new ProjectStructureLocalFileOpenResult(
                    false,
                    "The preferred application settings are invalid or unavailable.");
            }

            if (preference is null &&
                !FileToolsExternalOpenPolicy.IsAllowedSystemAssociatedFile(fullPath))
            {
                return new ProjectStructureLocalFileOpenResult(
                    false,
                    "This file type requires an explicit preferred application before it can be opened externally.");
            }
        }

        var request = new DesktopFileLaunchRequest(fullPath, operation, preference?.ExecutablePath);
        DesktopFileLaunchResult result = await desktopFileLauncher.LaunchAsync(request, cancellationToken);
        logger.Log(
            result.Succeeded ? LogLevel.Information : LogLevel.Warning,
            "Project Structure native file action completed. NodeId={NodeId} Extension={Extension} Operation={Operation} Success={Success} FailureCode={FailureCode}.",
            node.Id,
            Path.GetExtension(fullPath),
            operation,
            result.Succeeded,
            result.Failure?.Code);
        return MapResult(result, operation);
    }

    private static ProjectStructureLocalFileOpenResult MapResult(
        DesktopFileLaunchResult result,
        DesktopFileLaunchOperation operation)
    {
        if (result.Succeeded)
        {
            return new ProjectStructureLocalFileOpenResult(
                true,
                operation == DesktopFileLaunchOperation.Open
                    ? "Opened in the preferred application."
                    : "Opened the containing folder.");
        }

        string message = result.Failure?.Code switch
        {
            DesktopFileLaunchFailureCode.DesktopUnavailable => "Native file launching is not available on this host.",
            DesktopFileLaunchFailureCode.TargetNotFound => "The local file or folder is no longer available.",
            DesktopFileLaunchFailureCode.ApplicationNotFound => "The configured preferred application is no longer available.",
            _ => operation == DesktopFileLaunchOperation.Open
                ? "The preferred application could not be started."
                : "The containing folder could not be opened."
        };
        return new ProjectStructureLocalFileOpenResult(false, message);
    }

    private bool TryResolveTrustedPath(ProjectStructureNode? node, out string fullPath, out string failureMessage)
    {
        fullPath = string.Empty;
        failureMessage = "Local file actions are only available for trusted managed files, local files, or folders on this host.";
        if (!IsAvailable)
        {
            failureMessage = "Native file launching is not available on this host.";
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
            failureMessage = "Only files or folders stored under managed files or managed artifact roots can be opened locally.";
            return false;
        }

        if (!TryResolveReparseSafeWorkspacePath(resolution.FullPath, out string candidatePath))
        {
            failureMessage = "Local file actions cannot traverse symbolic links or filesystem reparse points.";
            return false;
        }

        if (Directory.Exists(candidatePath))
        {
            fullPath = candidatePath;
            return true;
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
        failureMessage = "A local file action requires an existing file or folder path.";

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
            var resolution = pathAccessGuard.ResolveWorkspacePath(value.Trim());
            if (!resolution.IsSuccess)
            {
                return false;
            }

            return TryResolveReparseSafeWorkspacePath(resolution.FullPath, out fullPath);
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

    private bool TryResolveReparseSafeWorkspacePath(string path, out string fullPath)
    {
        fullPath = string.Empty;
        try
        {
            fullPath = fileSystemPathPolicy.ResolveTrustedWorkspacePath(path);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or NotSupportedException
                or StorageBrowseException
                or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool HasExplicitPreferredApplication(string fullPath)
    {
        try
        {
            return applicationPreferences.ResolveForFile(Path.GetFileName(fullPath)) is not null;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or InvalidOperationException
                or UnauthorizedAccessException)
        {
            logger.LogWarning(
                "Unable to resolve preferred application availability. Extension={Extension} FailureType={FailureType}.",
                Path.GetExtension(fullPath),
                exception.GetType().Name);
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


