using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessCanvasSurfaceFactory
{
    private static CanvasWorkbenchNode BuildRunBranchNode(
        ProcessStepRunViewModel stepRun,
        IReadOnlyList<ProcessStepRunViewModel> allSteps)
    {
        var routedDependents = allSteps.Count(candidate =>
            candidate.Dependencies.Any(dependency => dependency.DependsOnStepDefinitionId == stepRun.StepDefinitionId));

        return new CanvasWorkbenchNode
        {
            Id = BuildRunBranchNodeId(stepRun.Id),
            Kind = ProcessCanvasCatalog.NodeKinds.RuntimeBranchRouter,
            Family = "special",
            Icon = "branch",
            Title = string.IsNullOrWhiteSpace(stepRun.Title)
                ? "Runtime routing"
                : $"{stepRun.Title} routing",
            Subtitle = string.IsNullOrWhiteSpace(stepRun.SelectedBranchOutcomeTitle)
                ? "Pending branch selection"
                : $"Selected: {stepRun.SelectedBranchOutcomeTitle}",
            LeadText = string.IsNullOrWhiteSpace(stepRun.DecisionSummary)
                ? "Runtime branch routing remains explicit."
                : stepRun.DecisionSummary,
            Status = stepRun.Status.ToString().ToLowerInvariant(),
            StatusPill = "Routing",
            PaletteKey = "accent",
            AccentColor = ResolveRunAccentColor(stepRun.Status),
            DurationLabel = $"{stepRun.AvailableBranchOutcomes.Count} routes",
            X = ResolveRunBranchNodeX(stepRun, allSteps),
            Y = ResolveRunBranchNodeY(stepRun, allSteps),
            Chips =
            [
                new CanvasWorkbenchChip
                {
                    Text = $"{stepRun.AvailableBranchOutcomes.Count} outputs",
                    Tone = "accent"
                },
                new CanvasWorkbenchChip
                {
                    Text = $"{routedDependents} downstream",
                    Tone = routedDependents == 0 ? "neutral" : "info"
                }
            ],
            InputPorts = DecorateProcessPorts(
            [
                new CanvasWorkbenchPort
                {
                    Id = ProcessCanvasCatalog.RuntimePorts.BranchStepInput,
                    Label = "From step",
                    Side = "left",
                    Tone = "neutral",
                    Kind = "source"
                }
            ]),
            OutputPorts = DecorateProcessPorts(stepRun.AvailableBranchOutcomes
                .Select(BuildRunBranchOutputPort))
        };
    }
}
