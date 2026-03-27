using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Factory.CanvasAdapters;

public static class PromptRunBranchLane
{
    public static CanvasWorkbenchNode BuildNode(
        string parentCanvasId,
        string branchKey,
        string branchLabel,
        int itemCount,
        double x,
        double y)
    {
        return new CanvasWorkbenchNode
        {
            Id = BuildCanvasId(branchKey),
            ParentId = parentCanvasId,
            Family = "group",
            Kind = "branch",
            Icon = "BR",
            Title = branchLabel,
            Subtitle = branchKey,
            LeadText = itemCount == 0
                ? "Build the prompt to materialize steps in this branch."
                : $"{itemCount} step(s) in this branch.",
            Status = "Branch",
            StatusPill = itemCount == 0 ? "Ready" : $"{itemCount} steps",
            AccentColor = "#8b5cf6",
            PaletteKey = "violet",
            IsRequired = true,
            IsCollapsible = itemCount > 0,
            X = x,
            Y = y,
            Chips =
            [
                new CanvasWorkbenchChip
                {
                    Text = string.Equals(branchKey, "main", StringComparison.OrdinalIgnoreCase) ? "Primary" : "Follow-up",
                    Tone = "accent"
                }
            ],
            FooterChips =
            [
                new CanvasWorkbenchChip
                {
                    Text = itemCount == 0 ? "No steps yet" : $"{itemCount} steps",
                    Tone = itemCount == 0 ? "warning" : "success"
                }
            ],
            Annotations = RecommendationOverlay.BuildBranchAnnotations(branchKey, itemCount).ToList(),
            ContextActions = BuildContextActions().ToList()
        };
    }

    public static string BuildCanvasId(string branchKey) => $"branch:{branchKey}";

    public static (double X, double Y) ResolveStepPosition(string branchKey, int stepIndex, double baseX, double baseY)
        => (
            baseX + 290 + (stepIndex * 238),
            baseY + (string.Equals(branchKey, "main", StringComparison.OrdinalIgnoreCase) ? 0 : stepIndex * 84));

    public static IReadOnlyList<CanvasWorkbenchAction> BuildContextActions()
        =>
        [
            new CanvasWorkbenchAction { ActionId = "build-flow", Label = "Build", Icon = "flow", Tone = "mint" },
            new CanvasWorkbenchAction { ActionId = "branch-selected", Label = "Branch", Icon = "fork", Tone = "warn" }
        ];
}


