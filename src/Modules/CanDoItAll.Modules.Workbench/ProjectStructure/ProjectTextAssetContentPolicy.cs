using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTextAssetContentPolicy
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Encode(
        ProjectFileSubtype subtype,
        string? content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(content))
        {
            throw InvalidContent("Text asset content is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        int byteCount;
        try
        {
            byteCount = Utf8.GetByteCount(content);
        }
        catch (EncoderFallbackException exception)
        {
            throw InvalidContent("Text asset content contains invalid Unicode data.", exception);
        }

        EnsureSupportedLength(byteCount);

        byte[] encoded;
        try
        {
            encoded = Utf8.GetBytes(content);
        }
        catch (EncoderFallbackException exception)
        {
            throw InvalidContent("Text asset content contains invalid Unicode data.", exception);
        }

        Validate(subtype, encoded, cancellationToken);
        return encoded;
    }

    public static void Validate(
        ProjectFileSubtype subtype,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.IsEmpty)
        {
            throw InvalidContent("Text asset content is required.");
        }

        EnsureSupportedLength(content.Length);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _ = Utf8.GetCharCount(content.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw InvalidContent("Text asset content must be valid UTF-8.", exception);
        }

        if (subtype == ProjectFileSubtype.Json)
        {
            ValidateJson(content);
        }
    }

    public static MermaidDiagramKind DetectMermaidDiagramKind(
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        Validate(ProjectFileSubtype.Mermaid, content, cancellationToken);
        return ProjectObjectMetadataSerializer.DetectMermaidDiagramKind(Utf8.GetString(content.Span));
    }

    private static void EnsureSupportedLength(int byteCount)
    {
        if (byteCount > ProjectAssetCreationLimits.MaximumEditableTextBytes)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.ContentTooLarge,
                $"Text assets are limited to {ProjectAssetCreationLimits.MaximumEditableTextBytes / (1024 * 1024)} MiB.");
        }
    }

    private static void ValidateJson(ReadOnlyMemory<byte> content)
    {
        try
        {
            using var _ = JsonDocument.Parse(content);
        }
        catch (JsonException exception)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.InvalidJson,
                "JSON asset content must contain valid JSON.",
                exception);
        }
    }

    private static ProjectAssetCreationException InvalidContent(string message)
        => new(ProjectAssetCreationErrorCode.InvalidContent, message);

    private static ProjectAssetCreationException InvalidContent(string message, Exception exception)
        => new(ProjectAssetCreationErrorCode.InvalidContent, message, exception);
}
