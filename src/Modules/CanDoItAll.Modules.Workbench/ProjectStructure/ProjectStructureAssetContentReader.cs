using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureAssetContentReader(
    IStorageCatalogService storageCatalog,
    IStorageDriverRegistry storageDrivers,
    ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy,
    ILogger<ProjectStructureAssetContentReader> logger)
{
    private const long MaximumContentBytes = ProjectStructureAssetUploadLimits.MaximumFileBytes;
    private const int MaximumStorageReferenceJsonCharacters = 64 * 1024;
    private const int BufferSize = 80 * 1024;

    internal async Task<byte[]> ReadAsync(
        ProjectStructureNode node,
        ProjectStructureAssetDescriptor asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(asset);

        StorageObjectReference reference = ParseReference(node.StorageObjectReferenceJson);
        if (reference.ContentLength > MaximumContentBytes)
        {
            throw AssetContentTooLarge();
        }

        bool isProtectedProcessScreenshotBinding = IsProtectedProcessScreenshotBinding(node);
        bool isProjectedProcessScreenshot = isProtectedProcessScreenshotBinding &&
                                            IsProjectedProcessScreenshot(node);
        if (isProtectedProcessScreenshotBinding && !isProjectedProcessScreenshot)
        {
            throw AssetContentNotFound();
        }

        ValidateManagedAssetBinding(reference, asset.MediaRelativePath, isProjectedProcessScreenshot);

        StorageCatalogRecord storage = await ResolveStorageAsync(
            reference,
            isProjectedProcessScreenshot,
            cancellationToken);
        ValidateStorageBinding(reference, storage);
        ValidateCurrentManagedStorage(reference, asset.MediaRelativePath, storage);
        IStorageDriver driver = ResolveDriver(reference, storage);

        try
        {
            Stream stream = await driver.OpenReadAsync(storage, reference, cancellationToken);
            if (stream is null)
            {
                throw AssetContentUnavailable();
            }

            await using (stream)
            {
                return await ReadBoundedAsync(reference, stream, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ProjectStructureAgentException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            LogReadFailure(asset, reference, exception);
            throw AssetContentNotFound();
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            LogReadFailure(asset, reference, exception);
            throw AssetContentUnavailable();
        }
    }

    private static StorageObjectReference ParseReference(string storageReferenceJson)
    {
        if (string.IsNullOrWhiteSpace(storageReferenceJson))
        {
            throw InvalidStorageReference("The asset does not have a bound storage reference.");
        }

        if (storageReferenceJson.Length > MaximumStorageReferenceJsonCharacters)
        {
            throw InvalidStorageReference("The asset storage reference exceeds the supported bounded size.");
        }

        try
        {
            StorageObjectReference? reference = StorageJson.ParseReference(storageReferenceJson);
            if (reference is null)
            {
                throw InvalidStorageReference("The asset storage reference is missing.");
            }

            ValidateReferenceSyntax(reference);
            return reference;
        }
        catch (ProjectStructureAgentException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw InvalidStorageReference("The asset storage reference is malformed.");
        }
    }

    private static void ValidateReferenceSyntax(StorageObjectReference reference)
    {
        if (reference.StorageId == Guid.Empty ||
            !Enum.IsDefined(reference.ProviderKind) ||
            !Enum.IsDefined(reference.LocatorKind) ||
            reference.ContentLength < 0)
        {
            throw InvalidStorageReference("The asset storage reference contains invalid values.");
        }

        try
        {
            _ = ProjectManagedStorageObjectKey.FromReference(reference);
            using JsonDocument metadata = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(reference.MetadataJson) ? "{}" : reference.MetadataJson);
            if (metadata.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw InvalidStorageReference("The asset storage metadata must be a JSON object.");
            }
        }
        catch (ProjectStructureAgentException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            throw InvalidStorageReference("The asset storage reference is malformed.");
        }
    }

    private static void ValidateManagedAssetBinding(
        StorageObjectReference reference,
        string mediaRelativePath,
        bool isProjectedProcessScreenshot)
    {
        if (isProjectedProcessScreenshot)
        {
            if (reference.StorageId.HasValue ||
                reference.ProviderKind != StorageProviderKind.FileSystem ||
                reference.LocatorKind != StorageLocatorKind.RelativePath ||
                !ProjectManagedStorageObjectKey.LocatorEquals(
                    StorageProviderKind.FileSystem,
                    reference.Locator,
                    NormalizePath(mediaRelativePath)))
            {
                throw InvalidStorageReference("The projected screenshot storage binding is invalid.");
            }

            return;
        }

        bool hasManagedMediaPath = ProjectManagedStorageProvenancePolicy
            .IsManagedProjectMediaPath(mediaRelativePath);
        if (!reference.StorageId.HasValue)
        {
            bool hasManagedLocator = ProjectManagedStorageProvenancePolicy
                .IsManagedProjectMediaPath(reference.Locator);
            if (!hasManagedMediaPath ||
                !hasManagedLocator ||
                reference.ProviderKind != StorageProviderKind.FileSystem ||
                reference.LocatorKind != StorageLocatorKind.RelativePath)
            {
                throw AssetContentNotFound();
            }

            if (!ProjectManagedStorageObjectKey.LocatorEquals(
                    StorageProviderKind.FileSystem,
                    reference.Locator,
                    ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(mediaRelativePath)))
            {
                throw InvalidStorageReference(
                    "The legacy asset storage locator does not match its managed media path.");
            }

            return;
        }

        if (hasManagedMediaPath &&
            (reference.ProviderKind is StorageProviderKind.FileSystem or StorageProviderKind.Ftp) &&
            !ProjectManagedStorageObjectKey.LocatorEquals(
                reference.ProviderKind,
                reference.Locator,
                ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(mediaRelativePath)))
        {
            throw InvalidStorageReference("The asset storage locator does not match its managed media path.");
        }
    }

    private async Task<StorageCatalogRecord> ResolveStorageAsync(
        StorageObjectReference reference,
        bool isProjectedProcessScreenshot,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!reference.StorageId.HasValue)
            {
                StorageCatalogRecord authoritativeStorage = await storageCatalog
                    .EnsureBootstrapFileSystemStorageAsync(cancellationToken);
                if (!authoritativeStorage.IsSystemDefault ||
                    authoritativeStorage.ProviderKind != StorageProviderKind.FileSystem ||
                    authoritativeStorage.Id == Guid.Empty)
                {
                    throw ProjectStructureAgentException.CreateAgentVisible(
                        503,
                        "AssetStorageBootstrapUnavailable",
                        "The authoritative workspace storage is unavailable for asset reads.",
                        canRetryWithCorrectedInput: false);
                }

                return authoritativeStorage;
            }

            StorageCatalogRecord? storage = await storageCatalog.GetAsync(
                reference.StorageId.Value,
                cancellationToken);
            if (storage is null)
            {
                throw ProjectStructureAgentException.CreateAgentVisible(
                    409,
                    "AssetStorageCatalogMissing",
                    "The asset's bound storage catalog no longer exists.",
                    canRetryWithCorrectedInput: false);
            }

            if (storage.Id != reference.StorageId.Value)
            {
                throw InvalidStorageReference(
                    "The resolved storage catalog does not match the asset storage reference.");
            }

            if (!isProjectedProcessScreenshot)
            {
                return storage;
            }

            StorageCatalogRecord bootstrap = await storageCatalog
                .EnsureBootstrapFileSystemStorageAsync(cancellationToken);
            if (storage.Id != bootstrap.Id)
            {
                throw InvalidStorageReference(
                    "Projected screenshots must use the authoritative workspace storage.");
            }

            return storage;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ProjectStructureAgentException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            logger.LogWarning(
                "Asset storage catalog resolution failed for provider {ProviderKind} with failure {FailureType}.",
                reference.ProviderKind,
                exception.GetType().Name);
            throw AssetContentUnavailable();
        }
    }

    private static void ValidateStorageBinding(
        StorageObjectReference reference,
        StorageCatalogRecord storage)
    {
        if (!storage.IsEnabled)
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                503,
                "AssetStorageDisabled",
                "The asset's bound storage catalog is disabled.",
                canRetryWithCorrectedInput: false);
        }

        if (storage.ProviderKind != reference.ProviderKind)
        {
            throw InvalidStorageReference(
                "The asset storage provider does not match its bound catalog.");
        }

        if (!storage.CapabilityMask.HasFlag(StorageCapability.Read))
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                503,
                "AssetStorageReadUnavailable",
                "The asset's bound storage catalog does not allow reads.",
                canRetryWithCorrectedInput: false);
        }
    }

    private void ValidateCurrentManagedStorage(
        StorageObjectReference reference,
        string mediaRelativePath,
        StorageCatalogRecord storage)
    {
        if (!ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference))
        {
            return;
        }

        if (!ProjectManagedStorageProvenancePolicy.TryValidate(
                reference,
                mediaRelativePath,
                out _))
        {
            throw InvalidStorageReference("The managed asset storage provenance is invalid.");
        }

        if (!ProjectManagedStorageProvenancePolicy.TryValidateCurrentStorage(
                reference,
                storage,
                physicalIdentityPolicy,
                out _))
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                409,
                "AssetStorageBindingChanged",
                "The asset's current storage namespace differs from its creation namespace.",
                canRetryWithCorrectedInput: false);
        }
    }

    private IStorageDriver ResolveDriver(
        StorageObjectReference reference,
        StorageCatalogRecord storage)
    {
        if (!storageDrivers.TryResolve(reference.ProviderKind, out IStorageDriver driver) ||
            driver.ProviderKind != reference.ProviderKind ||
            !driver.SupportedCapabilities.HasFlag(StorageCapability.Read))
        {
            throw ProjectStructureAgentException.CreateAgentVisible(
                503,
                "AssetStorageDriverUnavailable",
                "The asset's storage provider is unavailable for reads.",
                canRetryWithCorrectedInput: false);
        }

        return driver;
    }

    private static async Task<byte[]> ReadBoundedAsync(
        StorageObjectReference reference,
        Stream stream,
        CancellationToken cancellationToken)
    {
        long? reportedLength = ReadReportedLength(stream);
        if (reportedLength < 0)
        {
            throw AssetContentUnavailable();
        }

        if (reportedLength > MaximumContentBytes)
        {
            throw AssetContentTooLarge();
        }

        int initialCapacity = (int)Math.Min(
            reportedLength ?? reference.ContentLength ?? 0,
            MaximumContentBytes);
        using var memory = new MemoryStream(initialCapacity);
        var buffer = new byte[BufferSize];
        long totalBytes = 0;
        while (true)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (read > MaximumContentBytes - totalBytes)
            {
                throw AssetContentTooLarge();
            }

            totalBytes += read;
            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    private static long? ReadReportedLength(Stream stream)
        => stream.CanSeek ? stream.Length : null;

    private static bool IsProjectedProcessScreenshot(ProjectStructureNode node)
    {
        string normalizedPath = NormalizePath(node.MediaRelativePath);
        string[] segments = normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!node.IsSystemManaged ||
            node.ObjectType != ProjectObjectType.ImageAsset ||
            !string.Equals(
                node.ArtifactKind,
                ProjectStructureProcessNodeKeys.ProcessRunScreenshotArtifactKind,
                StringComparison.Ordinal) ||
            Path.IsPathRooted(node.MediaRelativePath) ||
            segments.Any(segment => segment is "." or "..") ||
            !ProjectStructureProcessNodeKeys.TryParseProcessRunScreenshotNodeKey(node.Id, out Guid runId) ||
            node.ArtifactId != runId ||
            !string.Equals(
                node.Id,
                ProjectStructureProcessNodeKeys.BuildProcessRunScreenshotNodeKey(runId, normalizedPath),
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsProtectedProcessScreenshotBinding(ProjectStructureNode node)
    {
        string normalizedPath = NormalizePath(node.MediaRelativePath);
        string[] segments = normalizedPath.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!string.Equals(
                node.ArtifactKind,
                ProjectStructureProcessNodeKeys.ProcessRunScreenshotArtifactKind,
                StringComparison.Ordinal) ||
            node.ArtifactId is not { } runId ||
            Path.IsPathRooted(node.MediaRelativePath) ||
            segments.Any(segment => segment is "." or ".."))
        {
            return false;
        }

        ProcessRunArtifactRootResolution runRoot = ProcessRunArtifactRootPolicy.Resolve(
            normalizedPath,
            runId);
        string managedArtifactRoot = ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(
            new ProcessRunId(runId));
        return runRoot.Kind == ProcessRunArtifactRootKind.ManagedArtifactRunRoot &&
               string.Equals(
                   runRoot.DirectoryPath,
                   managedArtifactRoot,
                   StringComparison.OrdinalIgnoreCase) &&
               normalizedPath.StartsWith(
                   $"{managedArtifactRoot}/",
                   StringComparison.OrdinalIgnoreCase);
    }

    private void LogReadFailure(
        ProjectStructureAssetDescriptor asset,
        StorageObjectReference reference,
        Exception exception)
    {
        logger.LogWarning(
            "Asset storage read failed for project {ProjectId}, node {NodeId}, storage {StorageId}, provider {ProviderKind}, failure {FailureType}.",
            asset.ProjectId,
            asset.NodeId,
            reference.StorageId,
            reference.ProviderKind,
            exception.GetType().Name);
    }

    private static bool IsExpectedStorageFailure(Exception exception)
        => exception is IOException
            or UnauthorizedAccessException
            or StorageBrowseException
            or HttpRequestException
            or InvalidOperationException
            or NotSupportedException
            or ArgumentException
            or TimeoutException;

    private static string NormalizePath(string path)
        => path.Trim().Replace('\\', '/');

    private static ProjectStructureAgentException InvalidStorageReference(string safeMessage)
        => ProjectStructureAgentException.CreateAgentVisible(
            400,
            "AssetStorageReferenceInvalid",
            safeMessage,
            canRetryWithCorrectedInput: false);

    private static ProjectStructureAgentException AssetContentNotFound()
        => ProjectStructureAgentException.CreateAgentVisible(
            404,
            "AssetContentNotFound",
            "The asset content was not found.",
            canRetryWithCorrectedInput: false);

    private static ProjectStructureAgentException AssetContentUnavailable()
        => ProjectStructureAgentException.CreateAgentVisible(
            503,
            "AssetContentUnavailable",
            "The asset content is temporarily unavailable from its bound storage provider.",
            canRetryWithCorrectedInput: false);

    private static ProjectStructureAgentException AssetContentTooLarge()
        => ProjectStructureAgentException.CreateAgentVisible(
            413,
            "AssetContentTooLarge",
            $"Asset content exceeds the {MaximumContentBytes} byte limit.",
            canRetryWithCorrectedInput: false);
}
