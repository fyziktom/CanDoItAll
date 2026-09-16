using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureAgentRuntimeImageAssetPolicy {
    private const long MaxImageAnalysisBytes = 10 * 1024 * 1024;

    public static AgentImageAnalysisSource CreateAnalysisSource(
        ProjectStructureAssetBinaryContent content) {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Bytes.LongLength > MaxImageAnalysisBytes) {
            throw ProjectStructureAgentException.CreateAgentVisible(
                413,
                "AssetImageAnalysisTooLarge",
                $"Image asset '{content.Asset.NodeId}' exceeds the {MaxImageAnalysisBytes / (1024 * 1024)} MiB image-analysis limit.",
                canRetryWithCorrectedInput: false);
        }

        var detectedContentType = DetectContentType(content.Bytes);
        if (detectedContentType is null) {
            var nextAction = ProjectStructureAgentRuntimeAssetTextReader.IsSupported(content.Asset)
                ? $" Use {ProjectStructureToolPolicy.ProjectStructureAssetTextGet} for textual assets such as SVG."
                : string.Empty;
            throw ProjectStructureAgentException.CreateAgentVisible(
                415,
                "AssetImageFormatUnsupported",
                $"Asset '{content.Asset.NodeId}' is not a supported PNG, JPEG, GIF, or WebP image.{nextAction}",
                canRetryWithCorrectedInput: false);
        }

        var declaredContentType = NormalizeRasterContentType(content.Asset.MediaContentType);
        if (declaredContentType is not null &&
            !declaredContentType.Equals(detectedContentType, StringComparison.OrdinalIgnoreCase)) {
            throw ProjectStructureAgentException.CreateAgentVisible(
                400,
                "AssetImageContentTypeMismatch",
                $"Asset '{content.Asset.NodeId}' declares '{content.Asset.MediaContentType}' but its bytes are '{detectedContentType}'.",
                canRetryWithCorrectedInput: false);
        }

        var fileName = Path.GetFileName(content.Asset.MediaOriginalFileName);
        if (string.IsNullOrWhiteSpace(fileName)) {
            fileName = "project-asset-image";
        }

        return new AgentImageAnalysisSource(fileName, detectedContentType, content.Bytes);
    }

    private static string? DetectContentType(ReadOnlySpan<byte> bytes) {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A) {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) {
            return "image/jpeg";
        }

        if (bytes.Length >= 6 &&
            (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8))) {
            return "image/gif";
        }

        if (bytes.Length >= 12 &&
            bytes[..4].SequenceEqual("RIFF"u8) &&
            bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) {
            return "image/webp";
        }

        return null;
    }

    private static string? NormalizeRasterContentType(string contentType) {
        var normalized = (contentType ?? string.Empty)
            .Split(';', 2, StringSplitOptions.TrimEntries)[0]
            .ToLowerInvariant();
        return normalized switch {
            "image/png" => "image/png",
            "image/jpeg" or "image/jpg" => "image/jpeg",
            "image/gif" => "image/gif",
            "image/webp" => "image/webp",
            _ => null
        };
    }
}
