using System.Text;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureAgentRuntimeAssetTextReader {
    private const int MaxTextCharacters = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static ProjectStructureAssetTextDescriptor Read(ProjectStructureAssetBinaryContent content) {
        ArgumentNullException.ThrowIfNull(content);
        if (!IsSupported(content.Asset)) {
            throw ProjectStructureAgentException.CreateAgentVisible(
                415,
                "AssetTextContentTypeUnsupported",
                $"Asset '{content.Asset.NodeId}' has content type '{content.Asset.MediaContentType}', which is not a supported text asset.",
                canRetryWithCorrectedInput: false);
        }

        string text;
        try {
            text = StrictUtf8.GetString(content.Bytes);
        } catch (DecoderFallbackException) {
            throw ProjectStructureAgentException.CreateAgentVisible(
                415,
                "AssetTextEncodingUnsupported",
                $"Asset '{content.Asset.NodeId}' is not valid UTF-8 text.",
                canRetryWithCorrectedInput: false);
        }

        if (text.Length > 0 && text[0] == '\uFEFF') {
            text = text[1..];
        }

        var characterCount = text.Length;
        var isTruncated = characterCount > MaxTextCharacters;
        if (isTruncated) {
            var take = MaxTextCharacters;
            if (char.IsHighSurrogate(text[take - 1])) {
                take--;
            }

            text = text[..take];
        }

        return new ProjectStructureAssetTextDescriptor(
            content.Asset,
            content.Bytes.LongLength,
            characterCount,
            text,
            isTruncated);
    }

    public static bool IsSupported(ProjectStructureAssetDescriptor asset) {
        ArgumentNullException.ThrowIfNull(asset);
        var contentType = NormalizeContentType(asset.MediaContentType);
        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/x-javascript", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeContentType(string contentType)
        => (contentType ?? string.Empty)
            .Split(';', 2, StringSplitOptions.TrimEntries)[0];
}
