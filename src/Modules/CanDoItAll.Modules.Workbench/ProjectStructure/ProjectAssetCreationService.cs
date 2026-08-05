namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectAssetCreationService
{
    private readonly ProjectAssetContentGeneratorResolver? compatibilityGeneratorResolver;

    public ProjectAssetCreationService()
    {
    }

    public ProjectAssetCreationService(ProjectAssetContentGeneratorResolver generatorResolver)
    {
        ArgumentNullException.ThrowIfNull(generatorResolver);
        compatibilityGeneratorResolver = generatorResolver;
    }

    public async ValueTask<ProjectObjectMediaPayload> CreateTextAsync(
        ProjectFileSubtype subtype,
        string fileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        ProjectAssetContent generated;
        if (compatibilityGeneratorResolver is not null)
        {
            IProjectAssetContentGenerator generator = compatibilityGeneratorResolver.Resolve(
                ProjectAssetGenerationKind.Text);
            generated = await generator.GenerateAsync(
                new ProjectTextAssetContentGenerationRequest(subtype, fileName, content),
                cancellationToken);
        }
        else
        {
            ProjectTextAssetFormat format = ProjectTextAssetFormatCatalog.Resolve(subtype);
            string normalizedFileName = ProjectTextAssetFormatCatalog.NormalizeGeneratedFileName(fileName, format);
            byte[] encodedContent = ProjectTextAssetContentPolicy.Encode(subtype, content, cancellationToken);
            generated = new ProjectAssetContent(
                normalizedFileName,
                format.CanonicalContentType,
                encodedContent);
        }

        return AdaptContent(generated, ProjectAssetCreationLimits.MaximumEditableTextBytes);
    }

    public ProjectObjectMediaPayload AdaptTextUpload(
        ProjectFileSubtype subtype,
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        return AdaptContent(
            NormalizeTextUpload(subtype, fileName, contentType, content, cancellationToken),
            ProjectAssetCreationLimits.MaximumEditableTextBytes);
    }

    public ProjectObjectMediaPayload AdaptEncodedTextUpload(
        ProjectFileSubtype subtype,
        string fileName,
        string contentType,
        string base64Content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(base64Content))
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.InvalidContent,
                "Uploaded text asset content is required.");
        }

        int maximumEncodedCharacters = ((ProjectAssetCreationLimits.MaximumEditableTextBytes + 2) / 3) * 4;
        if (base64Content.Length > maximumEncodedCharacters)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.ContentTooLarge,
                $"Text assets are limited to {ProjectAssetCreationLimits.MaximumEditableTextBytes / (1024 * 1024)} MiB.");
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(base64Content);
        }
        catch (FormatException exception)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.InvalidContent,
                "Uploaded text asset content is not valid base64 data.",
                exception);
        }

        return AdaptTextUpload(
            subtype,
            fileName,
            contentType,
            content,
            cancellationToken);
    }

    internal ProjectAssetContent NormalizeTextUpload(
        ProjectFileSubtype subtype,
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        ProjectTextAssetFormat format = ProjectTextAssetFormatCatalog.Resolve(subtype);
        string normalizedFileName = ProjectTextAssetFormatCatalog.NormalizeUploadedFileName(fileName, format);
        string trustedContentType = ProjectTextAssetFormatCatalog.ResolveTrustedUploadContentType(contentType, format);
        ProjectTextAssetContentPolicy.Validate(subtype, content, cancellationToken);

        return new ProjectAssetContent(normalizedFileName, trustedContentType, content);
    }

    public ProjectObjectMediaPayload AdaptUpload(
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content)
        => AdaptContent(
            new ProjectAssetContent(fileName, contentType, content),
            ProjectStructureAssetUploadLimits.MaximumFileBytes);

    private static ProjectObjectMediaPayload AdaptContent(ProjectAssetContent content, long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(content);

        string normalizedFileName = ProjectAssetFileNamePolicy.NormalizeLeafName(content.FileName);
        string normalizedContentType = content.ContentType?.Trim() ?? string.Empty;
        if (normalizedContentType.Length == 0 || normalizedContentType.Length > 160)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.InvalidContentType,
                "A valid content type of at most 160 characters is required.");
        }

        if (content.Content.IsEmpty)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.InvalidContent,
                "Asset content is required.");
        }

        if (content.Content.Length > maximumBytes)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.ContentTooLarge,
                $"Asset content is limited to {maximumBytes / (1024 * 1024)} MiB.");
        }

        return new ProjectObjectMediaPayload(
            normalizedFileName,
            normalizedContentType,
            Convert.ToBase64String(content.Content.Span));
    }
}
