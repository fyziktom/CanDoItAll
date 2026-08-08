using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureAgentRuntimeAssetContentSanitizer
{
    private const long MaxInlineAgentAssetContentBytes = 32 * 1024;

    public static ProjectStructureAssetContentDescriptor BoundForAgentRuntime(
        ProjectStructureAssetContentDescriptor content,
        bool canTransformArtifacts = true)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (ShouldInlineAssetContent(content))
        {
            return content with
            {
                Base64DataOmitted = false,
                ContentSummary = $"Base64Data contains {content.ContentLength:N0} byte(s) from a small textual asset."
            };
        }

        var mediaPath = string.IsNullOrWhiteSpace(content.Asset.MediaRelativePath)
            ? "the returned asset media path"
            : content.Asset.MediaRelativePath;
        var reason = ResolveOmissionReason(content);
        var nextAction = ResolveOmittedContentNextAction(content.Asset, mediaPath, canTransformArtifacts);

        return content with
        {
            Base64Data = string.Empty,
            Base64DataOmitted = true,
            ContentSummary = $"{reason} {nextAction}"
        };
    }

    private static bool ShouldInlineAssetContent(ProjectStructureAssetContentDescriptor content)
    {
        return content.ContentLength <= MaxInlineAgentAssetContentBytes &&
               ProjectStructureAgentRuntimeAssetTextReader.IsSupported(content.Asset) &&
               !IsSvgContentType(content.Asset.MediaContentType);
    }

    private static string ResolveOmissionReason(ProjectStructureAssetContentDescriptor content)
    {
        if (IsBinaryMediaContentType(content.Asset.MediaContentType))
        {
            return $"Base64Data is omitted because '{content.Asset.MediaContentType}' is binary media.";
        }

        if (!ProjectStructureAgentRuntimeAssetTextReader.IsSupported(content.Asset))
        {
            return $"Base64Data is omitted because '{content.Asset.MediaContentType}' is not a safe textual content type.";
        }

        return $"Base64Data is omitted because the asset is {content.ContentLength:N0} byte(s), exceeding the {MaxInlineAgentAssetContentBytes:N0}-byte runtime inline limit.";
    }

    private static bool IsBinaryMediaContentType(string contentType)
    {
        return IsImageContentType(contentType) ||
               contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveOmittedContentNextAction(
        ProjectStructureAssetDescriptor asset,
        string mediaPath,
        bool canTransformArtifacts)
    {
        var contentType = asset.MediaContentType;
        var assetArguments = $"projectId '{asset.ProjectId:D}' and nodeId '{asset.NodeId}'";
        if (IsSvgContentType(contentType))
        {
            return $"Use {AgentToolInvocationPolicyMetadata.ProjectStructureAssetTextGet} with {assetArguments} to inspect the SVG source as inert text.";
        }

        if (IsImageContentType(contentType))
        {
            return canTransformArtifacts
                ? $"Use {AgentToolInvocationPolicyMetadata.ProjectStructureAssetImageAnalyze} with {assetArguments}; do not pass this asset path to a workspace image tool."
                : "The selected agent lacks artifact-transformation access. Choose a project-authorized agent that can analyze images; do not pass this asset path to a workspace image tool.";
        }

        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("wordprocessingml.document", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/msword", StringComparison.OrdinalIgnoreCase))
        {
            // workspace_convert_document is gated by the TransformArtifacts permission; never point an
            // agent at a tool its composition cannot contain.
            return canTransformArtifacts
                ? $"Use workspace_convert_document with '{mediaPath}' and analyze the returned markdown preview or output path."
                : "The selected agent lacks artifact-transformation access. Choose a project-authorized agent that can convert documents to markdown.";
        }

        if (IsSpreadsheetContentType(contentType))
        {
            return $"Use workspace_inspect_spreadsheet or workspace_spreadsheet_summary with '{mediaPath}', then use workspace_read_spreadsheet_range when tabular content is required.";
        }

        if (ProjectStructureAgentRuntimeAssetTextReader.IsSupported(asset))
        {
            return $"Use {AgentToolInvocationPolicyMetadata.ProjectStructureAssetTextGet} with {assetArguments} to inspect bounded UTF-8 text.";
        }

        return $"Use a bounded workspace tool against '{mediaPath}' only when the step contract requires inspecting the asset bytes.";
    }

    private static bool IsImageContentType(string contentType)
        => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static bool IsSvgContentType(string contentType)
        => contentType.Split(';', 2, StringSplitOptions.TrimEntries)[0]
            .Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsSpreadsheetContentType(string contentType)
    {
        return contentType.Contains("spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("ms-excel", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("text/csv", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("text/tab-separated-values", StringComparison.OrdinalIgnoreCase);
    }
}
