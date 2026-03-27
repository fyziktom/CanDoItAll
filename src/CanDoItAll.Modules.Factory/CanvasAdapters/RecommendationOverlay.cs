using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Factory.CanvasAdapters;

public static class RecommendationOverlay
{
    public static RecommendationOverlaySummary BuildSummary(PromptFactorySessionGraphRequest request)
    {
        var items = new List<RecommendationOverlayItem>();

        if (!request.SetupIsReady)
        {
            items.Add(new RecommendationOverlayItem(
                "Setup gaps",
                $"{request.MissingSetupFieldCount} field(s) still need repository, provider, or project context before the session is fully operational.",
                "warn"));
        }

        var blueprint = request.Blueprints.FirstOrDefault(item => item.Id == request.Editor.BlueprintId);
        if (blueprint is not null && !string.IsNullOrWhiteSpace(blueprint.RecommendedFlowKey))
        {
            var currentFlow = request.Templates.FirstOrDefault(item => item.Id == request.Editor.FlowTemplateId);
            if (!string.Equals(currentFlow?.Key, blueprint.RecommendedFlowKey, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new RecommendationOverlayItem(
                    "Recommended flow",
                    $"'{blueprint.Name}' prefers the '{blueprint.RecommendedFlowKey}' flow template.",
                    "accent"));
            }

            var selectedKeys = request.Blocks
                .Where(block => request.Editor.SelectedBlockIds.Contains(block.Id))
                .Select(block => block.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingRecommendedBlocks = blueprint.RecommendedBlockKeys
                .Where(key => !selectedKeys.Contains(key))
                .Take(3)
                .ToList();
            if (missingRecommendedBlocks.Count > 0)
            {
                items.Add(new RecommendationOverlayItem(
                    "Missing recommended blocks",
                    $"Add {string.Join(", ", missingRecommendedBlocks)} to align the session baseline with the blueprint.",
                    "accent"));
            }
        }

        var emptyBranchCount = request.Editor.Nodes
            .GroupBy(node => node.BranchKey, StringComparer.OrdinalIgnoreCase)
            .Count(group => group.Count() == 0);
        if (request.Editor.Nodes.Count == 0 || emptyBranchCount > 0)
        {
            items.Add(new RecommendationOverlayItem(
                "Build branch lanes",
                "Run the build flow to materialize prompt steps and replace empty lanes with concrete prompt nodes.",
                "mint"));
        }

        if (request.Editor.Warnings.Count > 0)
        {
            items.Add(new RecommendationOverlayItem(
                "Session warnings",
                string.IsNullOrWhiteSpace(request.Editor.WarningSummary)
                    ? request.Editor.Warnings[0]
                    : request.Editor.WarningSummary,
                "warn"));
        }

        return new RecommendationOverlaySummary(
            items.Count > 0,
            "Recommendation overlay",
            "Surface the next best Prompt Factory actions directly on the canvas instead of burying them in the inspector.",
            items,
            CanApplyRecommendations: blueprint is not null || request.Editor.Warnings.Count > 0,
            CanBuildFlow: request.Editor.Nodes.Count == 0);
    }

    public static IReadOnlyList<CanvasWorkbenchAnnotation> BuildSessionAnnotations(PromptFactorySessionGraphRequest request)
    {
        if (request.Editor.Warnings.Count == 0)
        {
            return [];
        }

        return
        [
            new CanvasWorkbenchAnnotation
            {
                Id = "session:warnings",
                Kind = "validation",
                Tone = "warn",
                Label = $"{request.Editor.Warnings.Count} warning(s)",
                Description = string.IsNullOrWhiteSpace(request.Editor.WarningSummary)
                    ? request.Editor.Warnings[0]
                    : request.Editor.WarningSummary,
                Icon = "!",
                ActionId = "apply-recommendations"
            }
        ];
    }

    public static IReadOnlyList<CanvasWorkbenchAnnotation> BuildBlueprintAnnotations(PromptBlueprintSummary blueprint)
    {
        if (string.IsNullOrWhiteSpace(blueprint.RecommendedFlowKey))
        {
            return [];
        }

        return
        [
            new CanvasWorkbenchAnnotation
            {
                Id = $"blueprint:{blueprint.Id:N}:recommendation",
                Kind = "recommendation",
                Tone = "accent",
                Label = "Recommended flow",
                Description = $"This blueprint prefers '{blueprint.RecommendedFlowKey}'. Apply recommendations to sync blocks and flow.",
                Icon = "REC",
                ActionId = "apply-recommendations"
            }
        ];
    }

    public static IReadOnlyList<CanvasWorkbenchAnnotation> BuildComponentAnnotations(PromptBlockSummary block)
    {
        if (!block.IsRecommendedByDefault)
        {
            return [];
        }

        return
        [
            new CanvasWorkbenchAnnotation
            {
                Id = $"component:{block.Key}:recommended",
                Kind = "recommendation",
                Tone = "accent",
                Label = "Recommended",
                Description = "This block is part of the recommended baseline for the current blueprint or phase.",
                Icon = "REC"
            }
        ];
    }

    public static IReadOnlyList<CanvasWorkbenchAnnotation> BuildBranchAnnotations(string branchKey, int itemCount)
    {
        if (itemCount > 0)
        {
            return [];
        }

        return
        [
            new CanvasWorkbenchAnnotation
            {
                Id = $"branch:{branchKey}:empty",
                Kind = "recommendation",
                Tone = "accent",
                Label = "Build branch",
                Description = "Run the build flow to materialize prompt steps for this branch.",
                Icon = "BR",
                ActionId = "build-flow"
            }
        ];
    }
}

public sealed record RecommendationOverlaySummary(
    bool IsVisible,
    string Title,
    string Description,
    IReadOnlyList<RecommendationOverlayItem> Items,
    bool CanApplyRecommendations,
    bool CanBuildFlow);

public sealed record RecommendationOverlayItem(
    string Label,
    string Detail,
    string Tone);


