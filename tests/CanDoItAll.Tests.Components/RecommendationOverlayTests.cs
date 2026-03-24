using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Factory.CanvasAdapters;

namespace CanDoItAll.Tests.Components;

public sealed class RecommendationOverlayTests
{
    [Fact]
    public void Summary_collects_setup_flow_block_and_warning_recommendations()
    {
        var blueprintId = Guid.NewGuid();
        var request = new PromptFactorySessionGraphRequest(
            new PromptFactoryEditorModel
            {
                BlueprintId = blueprintId,
                FlowTemplateId = Guid.NewGuid(),
                Warnings = ["Provider profile missing"],
                WarningSummary = "Select a provider before sending prompts."
            },
            new PromptLibraryCatalogSummary([], [], [], 0, 0, 0),
            [
                new PromptBlueprintSummary(
                    blueprintId,
                    "spec-blueprint",
                    "Spec blueprint",
                    "analysis",
                    "Build a specification.",
                    string.Empty,
                    null,
                    "review-loop",
                    ["context-loader"],
                    1,
                    "bundle")
            ],
            [
                new PromptFlowTemplateSummary(
                    Guid.NewGuid(),
                    "alternate-flow",
                    "Alternate flow",
                    "Fallback flow",
                    [],
                    [],
                    [],
                    [],
                    1,
                    "bundle")
            ],
            [
                new PromptBlockSummary(
                    Guid.NewGuid(),
                    "context-loader",
                    "context-discovery",
                    "Context loader",
                    PromptBlockKind.Instruction,
                    "Load context.",
                    true,
                    true,
                    [],
                    [],
                    [],
                    [],
                    [],
                    [],
                    string.Empty,
                    string.Empty,
                    1,
                    "bundle")
            ],
            [],
            new PromptSessionSetupProfile(),
            new CanvasWorkbenchUiState(),
            "Setup incomplete",
            "Finish setup before build.",
            "Missing setup",
            false,
            2);

        var summary = RecommendationOverlay.BuildSummary(request);

        Assert.True(summary.IsVisible);
        Assert.Contains(summary.Items, item => item.Label == "Setup gaps");
        Assert.Contains(summary.Items, item => item.Label == "Recommended flow");
        Assert.Contains(summary.Items, item => item.Label == "Missing recommended blocks");
        Assert.Contains(summary.Items, item => item.Label == "Build branch lanes");
        Assert.Contains(summary.Items, item => item.Label == "Session warnings");
        Assert.True(summary.CanApplyRecommendations);
        Assert.True(summary.CanBuildFlow);
    }

    [Fact]
    public void Blueprint_annotations_expose_apply_recommendations_action()
    {
        var annotations = RecommendationOverlay.BuildBlueprintAnnotations(
            new PromptBlueprintSummary(
                Guid.NewGuid(),
                "review-blueprint",
                "Review blueprint",
                "review",
                "Review",
                string.Empty,
                null,
                "review-loop",
                [],
                1,
                "bundle"));

        Assert.Contains(annotations, annotation => annotation.ActionId == "apply-recommendations");
    }
}
