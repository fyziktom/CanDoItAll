using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectAssetStorageService(
    IStoragePlacementService storagePlacementService,
    ProjectAssetCreationService assetCreationService,
    ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy,
    StorageStablePlacementService? stablePlacementService = null)
{
    internal async Task<SavedMediaDescriptor?> SaveAsync(
        Guid projectId,
        ProjectObjectType objectType,
        string objectSubtype,
        ProjectObjectMediaPayload? media,
        CancellationToken cancellationToken = default)
    {
        if (media is null)
        {
            return null;
        }

        ProjectAssetContent content = Decode(media);
        content = NormalizeTypedTextContent(
            objectType,
            objectSubtype,
            content,
            cancellationToken);
        MermaidDiagramKind mermaidDiagramKind = ResolveMermaidDiagramKind(
            objectType,
            objectSubtype,
            content.Content,
            cancellationToken);

        string extension = Path.GetExtension(content.FileName);
        string safeExtension = string.IsNullOrWhiteSpace(extension)
            ? objectType == ProjectObjectType.ImageAsset ? ".png" : ".bin"
            : extension;
        string safeFileName = $"{SanitizeSlug(Path.GetFileNameWithoutExtension(content.FileName))}-{Guid.NewGuid():N}{safeExtension}";
        string category = objectType switch
        {
            ProjectObjectType.ImageAsset => "project-media/images",
            ProjectObjectType.VideoAsset => "project-media/videos",
            _ => "project-media/files"
        };
        string relativePath = Path.Combine(
                "managed-files",
                category,
                projectId.ToString("N"),
                safeFileName)
            .Replace('\\', '/');
        StorageContentKind contentKind = StorageContentClassifier.Resolve(
            content.ContentType,
            content.FileName);
        StoragePlacementResult placement = await storagePlacementService.PlaceAsync(
            new StoragePlacementRequest(
                content.FileName,
                content.ContentType,
                content.Content.ToArray(),
                StorageUsagePurpose.ProjectAsset,
                contentKind,
                projectId,
                RelativePathHint: relativePath,
                PreviewRequired: StorageContentClassifier.SupportsInlinePreview(contentKind)),
            cancellationToken);
        StorageObjectReference storageObjectReference = ProjectManagedStorageProvenancePolicy.Stamp(
            placement.WriteResult.Reference,
            relativePath,
            placement.Storage,
            physicalIdentityPolicy);

        return new SavedMediaDescriptor(
            placement.RelativePath,
            placement.Route,
            storageObjectReference.ContentType,
            content.FileName,
            objectType.ToString(),
            StorageJson.SerializeReference(storageObjectReference),
            mermaidDiagramKind);
    }

    internal async Task<(SavedMediaDescriptor Media, Exception? ObservationException)> SaveStableAsync(
        StoragePlacementIntentId intentId, Guid projectId, ProjectObjectType objectType, string objectSubtype,
        ProjectObjectMediaPayload media, CancellationToken cancellationToken) {
        var service = RequireStablePlacements();
        var prepared = PrepareStableMedia(intentId, projectId, objectType, objectSubtype, media, cancellationToken);
        var outcome = await service.PlaceAsync(intentId, prepared.Request, cancellationToken);
        if (outcome.State != StorageStablePlacementState.Completed || outcome.Receipt is null) {
            throw new StorageStablePlacementPendingException(outcome);
        }
        return (MapStableMedia(objectType, prepared, outcome.Receipt), outcome.ObservationException);
    }

    internal async Task<(SavedMediaDescriptor Media, StorageStablePlacementReceipt Receipt)> ReadCompletedStableAsync(
        StoragePlacementIntentId intentId, Guid projectId, ProjectObjectType objectType, string objectSubtype,
        ProjectObjectMediaPayload media, CancellationToken cancellationToken) {
        var service = RequireStablePlacements();
        var prepared = PrepareStableMedia(intentId, projectId, objectType, objectSubtype, media, cancellationToken);
        var receipt = await service.ReadCompletedForNativeContinuationAsync(intentId, prepared.Request, cancellationToken);
        return (MapStableMedia(objectType, prepared, receipt), receipt);
    }

    internal Task RequireCompletedStableForMutationAsync(StorageStablePlacementReceipt receipt, CancellationToken cancellationToken)
        => RequireStablePlacements().RequireCompletedForNativeMutationAsync(receipt, cancellationToken);

    private StorageStablePlacementService RequireStablePlacements() => stablePlacementService
        ?? throw new InvalidOperationException("Stable managed asset placement requires the registered Storage owner service.");

    private PreparedStableMedia PrepareStableMedia(StoragePlacementIntentId intentId, Guid projectId, ProjectObjectType objectType,
        string objectSubtype, ProjectObjectMediaPayload media, CancellationToken cancellationToken) {
        var content = NormalizeTypedTextContent(objectType, objectSubtype, Decode(media), cancellationToken);
        var diagramKind = ResolveMermaidDiagramKind(objectType, objectSubtype, content.Content, cancellationToken);
        var extension = Path.GetExtension(content.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? objectType == ProjectObjectType.ImageAsset ? ".png" : ".bin" : extension;
        var safeFileName = $"{SanitizeSlug(Path.GetFileNameWithoutExtension(content.FileName))}-{intentId.Value:N}{safeExtension}";
        var category = objectType switch {
            ProjectObjectType.ImageAsset => "images",
            ProjectObjectType.VideoAsset => "videos",
            _ => "files"
        };
        var relativePath = $"managed-files/project-media/{category}/{projectId:N}/{safeFileName}";
        var contentKind = StorageContentClassifier.Resolve(content.ContentType, content.FileName);
        return new(new(content.FileName, content.ContentType, content.Content.ToArray(), StorageUsagePurpose.ProjectAsset,
            contentKind, projectId, RelativePathHint: relativePath,
            PreviewRequired: StorageContentClassifier.SupportsInlinePreview(contentKind)), relativePath, diagramKind);
    }

    private SavedMediaDescriptor MapStableMedia(ProjectObjectType objectType, PreparedStableMedia prepared,
        StorageStablePlacementReceipt receipt) {
        var reference = ProjectManagedStorageProvenancePolicy.StampStable(receipt.WriteResult.Reference, prepared.RelativePath,
            receipt.Storage, physicalIdentityPolicy, receipt.IntentId);
        return new(receipt.RelativePath, receipt.Route, reference.ContentType, prepared.Request.FileName, objectType.ToString(),
            StorageJson.SerializeReference(reference), prepared.DiagramKind);
    }

    private sealed record PreparedStableMedia(StoragePlacementRequest Request, string RelativePath, MermaidDiagramKind DiagramKind);

    private static ProjectAssetContent Decode(ProjectObjectMediaPayload media)
    {
        if (string.IsNullOrWhiteSpace(media.FileName))
        {
            throw new InvalidDataException("Uploaded project assets require a file name.");
        }

        if (string.IsNullOrWhiteSpace(media.Base64Data))
        {
            throw new InvalidDataException("Uploaded project assets require file content.");
        }

        if (media.Base64Data.Length > ProjectStructureAssetUploadLimits.MaximumBase64Characters)
        {
            throw AssetTooLarge();
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(media.Base64Data);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "Uploaded project asset content is not valid base64.",
                exception);
        }

        if (bytes.LongLength > ProjectStructureAssetUploadLimits.MaximumFileBytes)
        {
            throw AssetTooLarge();
        }

        return new ProjectAssetContent(
            media.FileName,
            media.ContentType ?? string.Empty,
            bytes);
    }

    private ProjectAssetContent NormalizeTypedTextContent(
        ProjectObjectType objectType,
        string objectSubtype,
        ProjectAssetContent content,
        CancellationToken cancellationToken)
    {
        if (objectType != ProjectObjectType.File)
        {
            return content;
        }

        ProjectFileSubtype subtype = ProjectNodeKindRegistry.ResolveFileSubtype(
            objectType,
            objectSubtype);
        if (!ProjectTextAssetFormatCatalog.IsSupported(subtype))
        {
            return content;
        }

        try
        {
            return assetCreationService.NormalizeTextUpload(
                subtype,
                content.FileName,
                content.ContentType,
                content.Content,
                cancellationToken);
        }
        catch (ProjectAssetCreationException exception)
        {
            throw new InvalidDataException(exception.Message, exception);
        }
    }

    private static MermaidDiagramKind ResolveMermaidDiagramKind(
        ProjectObjectType objectType,
        string objectSubtype,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (objectType != ProjectObjectType.File ||
            !string.Equals(objectSubtype, "mermaid", StringComparison.OrdinalIgnoreCase))
        {
            return MermaidDiagramKind.Unknown;
        }

        try
        {
            return ProjectTextAssetContentPolicy.DetectMermaidDiagramKind(
                content,
                cancellationToken);
        }
        catch (ProjectAssetCreationException exception)
        {
            throw new InvalidDataException("Mermaid asset content is invalid.", exception);
        }
    }

    private static InvalidDataException AssetTooLarge()
        => new(
            $"Uploaded project assets are limited to " +
            $"{ProjectStructureAssetUploadLimits.MaximumFileBytes / (1024 * 1024)} MiB.");

    private static string SanitizeSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "asset";
        }

        string slug = new(value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return slug.Length == 0 ? "asset" : slug;
    }
}
