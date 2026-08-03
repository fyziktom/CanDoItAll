using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectAssetStorageService(
    IStoragePlacementService storagePlacementService,
    ProjectAssetCreationService assetCreationService)
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
        StorageObjectReference storageObjectReference = placement.WriteResult.Reference;

        return new SavedMediaDescriptor(
            placement.RelativePath,
            placement.Route,
            storageObjectReference.ContentType,
            content.FileName,
            objectType.ToString(),
            StorageJson.SerializeReference(storageObjectReference),
            mermaidDiagramKind);
    }

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
