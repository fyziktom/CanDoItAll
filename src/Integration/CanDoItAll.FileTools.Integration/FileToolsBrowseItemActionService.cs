using CanDoItAll.FileTools.Desktop;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.FileTools.Integration;

internal sealed class FileToolsBrowseItemActionService(
    StorageFileToolsBrowseItemResolver itemResolver,
    StorageFileToolsKnownFileResolver knownFileResolver,
    IFileAccessContextProvider contextProvider,
    IStorageFileAccessAuthorizationCoordinator authorizationCoordinator,
    IStorageDriverRegistry storageDrivers,
    FileSystemStoragePathPolicy pathPolicy,
    IFileApplicationPreferenceService applicationPreferences,
    AuthorizedFileContentSource contentSource,
    IDesktopFileLauncher desktopFileLauncher,
    ILogger<FileToolsBrowseItemActionService> logger) :
    IFileToolsBrowseItemActionService,
    IFileToolsKnownFileActionService
{
    public bool IsLocalLaunchAvailable => desktopFileLauncher.IsAvailable;

    public async ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        FileToolsBrowseItemActionResult? unavailable = ValidateLocalLaunch(action);
        if (unavailable is not null)
        {
            return unavailable;
        }

        (AuthorizedBrowserFile occurrence, AuthorizedStorageFile authorized) = await AuthorizeAsync(
            scope,
            itemKey,
            FileAccessOperation.OpenLocally,
            cancellationToken);
        try
        {
            return await LaunchAuthorizedAsync(
                scope,
                occurrence.FileName,
                authorized,
                action,
                cancellationToken);
        }
        finally
        {
            await authorizationCoordinator.RevokeAsync(occurrence.File, CancellationToken.None);
        }
    }

    public async ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
        FileToolsSemanticScope scope,
        FileToolsKnownFileOccurrence occurrence,
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(occurrence);
        FileToolsBrowseItemActionResult? unavailable = ValidateLocalLaunch(action);
        if (unavailable is not null)
        {
            return unavailable;
        }

        ResolvedStorageKnownFile resolved = await knownFileResolver.ResolveAsync(
            occurrence,
            cancellationToken);
        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        var grantRequest = new FileAccessGrantRequest(
            context,
            scope,
            resolved.Storage.Id,
            resolved.GrantOccurrenceId,
            FileAccessOperation.OpenLocally);
        FileReference file = await authorizationCoordinator.GrantAsync(
            grantRequest,
            resolved.Reference,
            cancellationToken);
        try
        {
            AuthorizedStorageFile authorized = await authorizationCoordinator.ResolveAsync(
                file,
                context,
                FileAccessOperation.OpenLocally,
                cancellationToken);
            return await LaunchAuthorizedAsync(
                scope,
                occurrence.FileName,
                authorized,
                action,
                cancellationToken);
        }
        finally
        {
            await authorizationCoordinator.RevokeAsync(file, CancellationToken.None);
        }
    }

    public async ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        (AuthorizedBrowserFile occurrence, AuthorizedStorageFile authorized) = await AuthorizeAsync(
            scope,
            itemKey,
            FileAccessOperation.Download,
            cancellationToken);
        try
        {
            if (!storageDrivers.TryResolve(authorized.Storage.ProviderKind, out IStorageDriver driver) ||
                !authorized.Storage.CapabilityMask.HasFlag(StorageCapability.Download) ||
                !driver.SupportedCapabilities.HasFlag(StorageCapability.Download))
            {
                throw new FileAccessDeniedException(
                    FileAccessFailureCode.Unsupported,
                    "Downloading is not supported by the selected storage source.");
            }

            logger.LogInformation(
                "Authorized browser download issued. StorageId={StorageId} ScopeKind={ScopeKind} Extension={Extension}.",
                authorized.Storage.Id,
                scope.Kind,
                Path.GetExtension(occurrence.FileName));
            return new AuthorizedFileToolsDownloadLease(
                occurrence.File,
                occurrence.FileName,
                contentSource.For(occurrence.File, FileAccessOperation.Download),
                authorizationCoordinator);
        }
        catch
        {
            await authorizationCoordinator.RevokeAsync(occurrence.File, CancellationToken.None);
            throw;
        }
    }

    private FileToolsBrowseItemActionResult? ValidateLocalLaunch(FileToolsLocalFileAction action)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        return IsLocalLaunchAvailable
            ? null
            : FileToolsBrowseItemActionResult.Failure(
                FileToolsBrowseItemActionFailureCode.LaunchFailed,
                "Native file launching is not available in this host process.");
    }

    private async ValueTask<FileToolsBrowseItemActionResult> LaunchAuthorizedAsync(
        FileToolsSemanticScope scope,
        string fileName,
        AuthorizedStorageFile authorized,
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken)
    {
        EnsureLocalOpenSupported(authorized);
        string fullPath = pathPolicy.ResolveTrustedLocalOpenPath(
            authorized.Storage,
            authorized.Reference.Locator);
        string trustedFileName = Path.GetFileName(fullPath);
        (FileApplicationPreference? preference, FileToolsBrowseItemActionResult? preferenceFailure) = ResolvePreference(
            scope,
            authorized.Storage.Id,
            trustedFileName,
            action);
        if (preferenceFailure is not null)
        {
            return preferenceFailure;
        }

        if (action == FileToolsLocalFileAction.OpenInPreferredApplication &&
            preference is null &&
            !FileToolsExternalOpenPolicy.IsAllowedSystemAssociatedFile(trustedFileName))
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Unsupported,
                "This file type is not allowed for system-associated launching. Configure an explicit preferred application to open it as data.");
        }

        var request = new DesktopFileLaunchRequest(
            fullPath,
            action == FileToolsLocalFileAction.OpenInPreferredApplication
                ? DesktopFileLaunchOperation.Open
                : DesktopFileLaunchOperation.OpenContainingFolder,
            preference?.ExecutablePath);
        DesktopFileLaunchResult result = await desktopFileLauncher.LaunchAsync(
            request,
            cancellationToken);
        FileToolsBrowseItemActionResult mapped = MapResult(result, action, fileName);
        logger.Log(
            mapped.IsSuccess ? LogLevel.Information : LogLevel.Warning,
            "Authorized file launch completed. StorageId={StorageId} ScopeKind={ScopeKind} Extension={Extension} Action={Action} Success={Success} FailureCode={FailureCode}.",
            authorized.Storage.Id,
            scope.Kind,
            Path.GetExtension(fileName),
            action,
            mapped.IsSuccess,
            mapped.FailureCode);
        return mapped;
    }

    private (FileApplicationPreference? Preference, FileToolsBrowseItemActionResult? Failure) ResolvePreference(
        FileToolsSemanticScope scope,
        Guid storageId,
        string trustedFileName,
        FileToolsLocalFileAction action)
    {
        if (action != FileToolsLocalFileAction.OpenInPreferredApplication)
        {
            return (null, null);
        }

        try
        {
            return (applicationPreferences.ResolveForFile(trustedFileName), null);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or InvalidOperationException
                or UnauthorizedAccessException)
        {
            logger.LogWarning(
                "Preferred file application resolution failed. StorageId={StorageId} ScopeKind={ScopeKind} Action={Action} FailureType={FailureType}.",
                storageId,
                scope.Kind,
                action,
                exception.GetType().Name);
            return (
                null,
                FileToolsBrowseItemActionResult.Failure(
                    FileToolsBrowseItemActionFailureCode.PreferredApplicationUnavailable,
                    "The preferred application settings are invalid or unavailable."));
        }
    }

    private async ValueTask<(AuthorizedBrowserFile Occurrence, AuthorizedStorageFile Authorized)> AuthorizeAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        FileAccessOperation operation,
        CancellationToken cancellationToken)
    {
        StorageFileBrowserProvider provider = await itemResolver.ResolveProviderAsync(
            scope,
            itemKey,
            cancellationToken);
        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        AuthorizedBrowserFile occurrence = await provider.AuthorizeItemAsync(
            itemKey,
            context,
            scope,
            operation,
            authorizationCoordinator,
            cancellationToken);
        try
        {
            AuthorizedStorageFile authorized = await authorizationCoordinator.ResolveAsync(
                occurrence.File,
                context,
                operation,
                cancellationToken);
            return (occurrence, authorized);
        }
        catch
        {
            await authorizationCoordinator.RevokeAsync(occurrence.File, CancellationToken.None);
            throw;
        }
    }

    private void EnsureLocalOpenSupported(AuthorizedStorageFile authorized)
    {
        if (authorized.Storage.ProviderKind != StorageProviderKind.FileSystem ||
            authorized.Reference.LocatorKind != StorageLocatorKind.RelativePath ||
            !authorized.Storage.CapabilityMask.HasFlag(StorageCapability.OpenLocally) ||
            !storageDrivers.TryResolve(StorageProviderKind.FileSystem, out IStorageDriver driver) ||
            !driver.SupportedCapabilities.HasFlag(StorageCapability.OpenLocally) ||
            !pathPolicy.IsTrustedForLocalOpen(authorized.Storage))
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Unsupported,
                "The selected file is not available to a trusted local application.");
        }
    }

    private static FileToolsBrowseItemActionResult MapResult(
        DesktopFileLaunchResult result,
        FileToolsLocalFileAction action,
        string fileName)
    {
        if (result.Succeeded)
        {
            return FileToolsBrowseItemActionResult.Success(
                action == FileToolsLocalFileAction.OpenInPreferredApplication
                    ? $"Opening {fileName} in the preferred application."
                    : $"Opening the folder containing {fileName}.");
        }

        DesktopFileLaunchFailure failure = result.Failure
            ?? throw new InvalidOperationException("A failed desktop launch did not include failure details.");
        FileToolsBrowseItemActionFailureCode code = failure.Code switch
        {
            DesktopFileLaunchFailureCode.TargetNotFound => FileToolsBrowseItemActionFailureCode.TargetUnavailable,
            DesktopFileLaunchFailureCode.ApplicationNotFound => FileToolsBrowseItemActionFailureCode.PreferredApplicationUnavailable,
            _ => FileToolsBrowseItemActionFailureCode.LaunchFailed
        };
        string message = failure.Code switch
        {
            DesktopFileLaunchFailureCode.DesktopUnavailable => "Native file launching is not available on this host.",
            DesktopFileLaunchFailureCode.TargetNotFound => "The local file is no longer available.",
            DesktopFileLaunchFailureCode.ApplicationNotFound => "The configured preferred application is no longer available.",
            _ => "The requested local application could not be started."
        };
        return FileToolsBrowseItemActionResult.Failure(code, message);
    }
}
