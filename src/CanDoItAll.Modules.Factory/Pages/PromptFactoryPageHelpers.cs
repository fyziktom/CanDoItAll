using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Factory.CanvasAdapters;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Factory.Pages;

internal static class PromptFactoryPageHelpers
{
    private const string PromptNodeCanvasNodePrefix = "node:";
    private const string BranchCanvasNodePrefix = "branch:";

    public static PromptFactorySessionGraphRequest BuildGraphRequest(
        PromptFactoryEditorModel editor,
        PromptLibraryCatalogSummary libraryCatalog,
        IReadOnlyList<PromptBlueprintSummary> blueprints,
        IReadOnlyList<PromptFlowTemplateSummary> templates,
        IReadOnlyList<PromptBlockSummary> blocks,
        IReadOnlyList<PromptSessionAttachmentSummary> visibleSessionAttachments,
        PromptSessionSetupProfile sessionSetup,
        CanvasWorkbenchUiState uiState,
        string setupSummaryLine,
        string setupLeadCopy,
        string setupStatusLabel,
        bool setupIsReady,
        int missingSetupFieldCount)
        => new(
            editor,
            libraryCatalog,
            blueprints,
            templates,
            blocks,
            visibleSessionAttachments,
            sessionSetup,
            uiState,
            setupSummaryLine,
            setupLeadCopy,
            setupStatusLabel,
            setupIsReady,
            missingSetupFieldCount);

    public static RecommendationOverlaySummary ResolveRecommendationOverlay(PromptFactorySessionGraphRequest request)
    {
        var summary = RecommendationOverlay.BuildSummary(request);
        if (summary.IsVisible)
        {
            return summary;
        }

        var fallbackItems = new List<RecommendationOverlayItem>();
        if (request.MissingSetupFieldCount > 0)
        {
            fallbackItems.Add(new RecommendationOverlayItem(
                "Setup gaps",
                $"{request.MissingSetupFieldCount} field(s) still need attention before the session is fully configured.",
                "warn"));
        }

        if (request.Editor.Nodes.Count == 0)
        {
            fallbackItems.Add(new RecommendationOverlayItem(
                "Build branch lanes",
                "The session has selected components but no prompt steps yet. Run the build flow to materialize the branch graph.",
                "mint"));
        }

        return fallbackItems.Count > 0
            ? new RecommendationOverlaySummary(
                true,
                "Recommendation overlay",
                "Surface the next best Prompt Factory actions directly on the canvas instead of burying them in the inspector.",
                fallbackItems,
                CanApplyRecommendations: request.Editor.Warnings.Count > 0 || request.Editor.BlueprintId.HasValue,
                CanBuildFlow: request.Editor.Nodes.Count == 0)
            : summary;
    }

    public static Guid? ParseOptionalGuid(object? value)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Guid.TryParse(text, out var parsed) ? parsed : null;
    }

    public static bool TryParsePromptCanvasNodeId(string? canvasNodeId, out Guid nodeId)
    {
        nodeId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(canvasNodeId) &&
               canvasNodeId.StartsWith(PromptNodeCanvasNodePrefix, StringComparison.Ordinal) &&
               Guid.TryParse(canvasNodeId[PromptNodeCanvasNodePrefix.Length..], out nodeId);
    }

    public static string ResolveBranchLabel(string canvasNodeId)
        => canvasNodeId.StartsWith(BranchCanvasNodePrefix, StringComparison.Ordinal)
            ? canvasNodeId[BranchCanvasNodePrefix.Length..].Replace('-', ' ')
            : "Branch";

    public static bool ReadCheckboxValue(ChangeEventArgs args) => args.Value switch
    {
        bool value => value,
        string text when bool.TryParse(text, out var value) => value,
        _ => false
    };
}
