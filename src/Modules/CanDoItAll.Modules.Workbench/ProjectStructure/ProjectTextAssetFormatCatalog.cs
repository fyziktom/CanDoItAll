using System.Net.Http.Headers;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectTextAssetFormat(
    string CanonicalExtension,
    string CanonicalContentType,
    IReadOnlyList<string> GeneratedExtensions,
    IReadOnlyList<string> UploadExtensions,
    IReadOnlyList<string> AdvisoryContentTypes);

internal static class ProjectTextAssetFormatCatalog
{
    private const string BinaryContentType = "application/octet-stream";
    private const string JsonContentType = "application/json";
    private const string MarkdownContentType = "text/markdown";
    private const string MermaidContentType = "text/vnd.mermaid";
    private const string PlainTextContentType = "text/plain";

    private static readonly ProjectTextAssetFormat Text = new(
        ".txt",
        PlainTextContentType,
        [".txt"],
        [".txt"],
        [PlainTextContentType]);

    private static readonly ProjectTextAssetFormat Json = new(
        ".json",
        JsonContentType,
        [".json"],
        [".json"],
        [JsonContentType, "text/json", PlainTextContentType]);

    private static readonly ProjectTextAssetFormat Markdown = new(
        ".md",
        MarkdownContentType,
        [".md", ".markdown"],
        [".md", ".markdown", ".txt"],
        [MarkdownContentType, PlainTextContentType]);

    private static readonly ProjectTextAssetFormat Mermaid = new(
        ".mmd",
        MermaidContentType,
        [".mmd", ".mermaid"],
        [".mmd", ".mermaid", ".txt"],
        [MermaidContentType, PlainTextContentType]);

    public static ProjectTextAssetFormat Resolve(ProjectFileSubtype subtype)
        => subtype switch
        {
            ProjectFileSubtype.Text => Text,
            ProjectFileSubtype.Json => Json,
            ProjectFileSubtype.Markdown => Markdown,
            ProjectFileSubtype.Mermaid => Mermaid,
            _ => throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.UnsupportedFileSubtype,
                $"File subtype '{subtype}' is not supported by the text asset generator.")
        };

    public static string NormalizeGeneratedFileName(string fileName, ProjectTextAssetFormat format)
        => NormalizeFileName(fileName, format, format.GeneratedExtensions);

    public static string NormalizeUploadedFileName(string fileName, ProjectTextAssetFormat format)
        => NormalizeFileName(fileName, format, format.UploadExtensions);

    public static string ResolveTrustedUploadContentType(string? contentType, ProjectTextAssetFormat format)
    {
        string advisoryContentType = contentType?.Trim() ?? string.Empty;
        if (advisoryContentType.Length == 0)
        {
            return format.CanonicalContentType;
        }

        if (advisoryContentType.Length > 160 ||
            !MediaTypeHeaderValue.TryParse(advisoryContentType, out var parsed) ||
            string.IsNullOrWhiteSpace(parsed.MediaType))
        {
            throw InvalidContentType("The uploaded file declares an invalid content type.");
        }

        string mediaType = parsed.MediaType;
        if (mediaType.Equals(BinaryContentType, StringComparison.OrdinalIgnoreCase) ||
            format.AdvisoryContentTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
        {
            return format.CanonicalContentType;
        }

        throw InvalidContentType(
            $"The uploaded file content type conflicts with the expected '{format.CanonicalContentType}' format.");
    }

    private static string NormalizeFileName(
        string fileName,
        ProjectTextAssetFormat format,
        IReadOnlyList<string> acceptedExtensions)
    {
        string safeName = ProjectAssetFileNamePolicy.NormalizeLeafName(fileName);
        string extension = Path.GetExtension(safeName);
        string stem = safeName;

        if (extension.Length > 0)
        {
            if (!acceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new ProjectAssetCreationException(
                    ProjectAssetCreationErrorCode.InvalidFileName,
                    $"The file name must use the '{format.CanonicalExtension}' extension for this asset type.");
            }

            stem = safeName[..^extension.Length];
        }

        if (string.IsNullOrWhiteSpace(stem) || stem.Trim('.', ' ').Length == 0)
        {
            throw new ProjectAssetCreationException(
                ProjectAssetCreationErrorCode.InvalidFileName,
                "The file name must include a name before its extension.");
        }

        return ProjectAssetFileNamePolicy.NormalizeLeafName($"{stem}{format.CanonicalExtension}");
    }

    private static ProjectAssetCreationException InvalidContentType(string message)
        => new(ProjectAssetCreationErrorCode.InvalidContentType, message);
}
