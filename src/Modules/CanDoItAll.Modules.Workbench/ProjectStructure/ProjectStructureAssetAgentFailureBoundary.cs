using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructureAssetAgentFailureKind
{
    AssetTypeRequired,
    FileNameRequired,
    MediaPayloadRequired,
    MediaPayloadTooLarge,
    InvalidBase64Payload,
    MediaSourceRequired,
    NodeNotFound,
    AssetRequired
}

internal static class ProjectStructureAssetAgentFailureBoundary
{
    public static ProjectStructureAgentException Create(ProjectStructureAssetAgentFailureKind kind)
    {
        var (statusCode, errorCode, safeMessage) = kind switch
        {
            ProjectStructureAssetAgentFailureKind.AssetTypeRequired =>
                (400, "AssetTypeRequired", "Asset creation requires objectType File, ImageAsset, or VideoAsset. Correct objectType and retry."),
            ProjectStructureAssetAgentFailureKind.FileNameRequired =>
                (400, "FileNameRequired", "The media payload requires a non-empty fileName. Provide one and retry."),
            ProjectStructureAssetAgentFailureKind.MediaPayloadRequired =>
                (400, "MediaPayloadRequired", "The media payload requires non-empty base64Data. Provide the encoded file content and retry."),
            ProjectStructureAssetAgentFailureKind.MediaPayloadTooLarge =>
                (413, "MediaPayloadTooLarge", $"The media payload exceeds the {ProjectStructureAssetUploadLimits.MaximumFileBytes / (1024 * 1024)} MiB asset limit. Provide a smaller file and retry."),
            ProjectStructureAssetAgentFailureKind.InvalidBase64Payload =>
                (400, "InvalidBase64Payload", "The media payload base64Data is invalid. Encode the file as valid base64 and retry."),
            ProjectStructureAssetAgentFailureKind.MediaSourceRequired =>
                (400, "MediaSourceRequired", "Asset creation requires a source accepted by the current tool, such as a media payload or source workspace path. Provide one supported source and retry."),
            ProjectStructureAssetAgentFailureKind.NodeNotFound =>
                (404, "NodeNotFound", "The requested project-structure node was not found. Read the current structure and retry with an existing node id."),
            ProjectStructureAssetAgentFailureKind.AssetRequired =>
                (400, "AssetRequired", "The requested node is not a managed asset. Read the current structure and retry with a File, ImageAsset, or VideoAsset node."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported project-structure asset failure kind.")
        };

        return ProjectStructureAgentException.CreateAgentVisible(
            statusCode,
            errorCode,
            safeMessage,
            canRetryWithCorrectedInput: true,
            effectState: AgentToolEffectState.NotCommitted);
    }
}
