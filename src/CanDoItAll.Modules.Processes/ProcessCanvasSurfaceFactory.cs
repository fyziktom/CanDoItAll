using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessCanvasSurfaceFactory
{
    private readonly ProcessCanvasChromeCatalogService chromeCatalogService;

    public ProcessCanvasSurfaceFactory(ProcessCanvasChromeCatalogService chromeCatalogService)
    {
        this.chromeCatalogService = chromeCatalogService;
    }

    public CanvasWorkbenchSurface BuildDefinitionSurface(
        ProcessDefinitionEditorModel editor,
        string? selectedNodeId = null,
        string mode = "authoring")
    {
        ArgumentNullException.ThrowIfNull(editor);

        ProcessCanvasBranching.NormalizeDefinitionEditor(editor);

        var branchOutcomeTitles = editor.Steps
            .SelectMany(ProcessCanvasBranching.GetOrderedBranchOutcomes)
            .Where(outcome => outcome.Id.HasValue)
            .ToDictionary(
                outcome => outcome.Id!.Value,
                outcome => string.IsNullOrWhiteSpace(outcome.Title) ? "Untitled branch" : outcome.Title);
        var rolesById = editor.Roles
            .Where(role => role.Id.HasValue)
            .ToDictionary(role => role.Id!.Value);
        var stepNodes = editor.Steps
            .Select((step, index) =>
            {
                var primaryDependency = ProcessCanvasBranching.GetOrderedDependencies(step).FirstOrDefault();
                return BuildDefinitionNode(
                    step,
                    index,
                    rolesById,
                    primaryDependency?.DependsOnBranchOutcomeId.HasValue == true &&
                    branchOutcomeTitles.TryGetValue(primaryDependency.DependsOnBranchOutcomeId.Value, out var dependencyOutcomeTitle)
                        ? dependencyOutcomeTitle
                        : null);
            })
            .ToList();
        var branchNodes = editor.Steps
            .Where(ProcessCanvasBranching.ShouldRenderBranchRouter)
            .Select(step => BuildDefinitionBranchNode(step, editor.Steps, rolesById))
            .ToList();
        var roleNodes = editor.Roles
            .Select((role, index) => BuildDefinitionRoleNode(role, index, editor))
            .ToList();
        var nodes = new List<CanvasWorkbenchNode>(stepNodes.Count + branchNodes.Count + roleNodes.Count);
        nodes.AddRange(stepNodes);
        nodes.AddRange(branchNodes);
        nodes.AddRange(roleNodes);
        var links = BuildDefinitionLinks(editor.Steps, rolesById);

        return new CanvasWorkbenchSurface
        {
            SurfaceId = editor.Id?.ToString("N") ?? "process-definition-draft",
            Mode = string.Equals(mode, "delete", StringComparison.Ordinal)
                ? "delete"
                : "authoring",
            DependencySourceId = editor.WorkingVersionId?.ToString("N") ?? "draft",
            Nodes = nodes,
            Links = links,
            UiState = new CanvasWorkbenchUiState
            {
                SelectedNodeIds = ResolveSelectedNodeIds(nodes, selectedNodeId),
                ActiveInspectorTab = "definition"
            },
            Chrome = BuildDefinitionChrome()
        };
    }

    public CanvasWorkbenchSurface BuildRunSurface(
        ProcessRunListItem run,
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        string? selectedNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(run);

        var orderedSteps = stepRuns
            .OrderBy(stepRun => stepRun.Sequence)
            .ToList();
        var stepNodes = orderedSteps
            .Select(BuildRunNode)
            .ToList();
        var branchNodes = orderedSteps
            .Where(ProcessCanvasBranching.ShouldRenderBranchRouter)
            .Select(stepRun => BuildRunBranchNode(stepRun, orderedSteps))
            .ToList();
        var nodes = new List<CanvasWorkbenchNode>(stepNodes.Count + branchNodes.Count);
        nodes.AddRange(stepNodes);
        nodes.AddRange(branchNodes);
        var links = BuildRunLinks(orderedSteps);

        return new CanvasWorkbenchSurface
        {
            SurfaceId = run.Id.ToString("N"),
            Mode = "runtime",
            DependencySourceId = run.ProcessDefinitionVersionId.ToString("N"),
            Nodes = nodes,
            Links = links,
            UiState = new CanvasWorkbenchUiState
            {
                SelectedNodeIds = ResolveSelectedNodeIds(
                    nodes,
                    selectedNodeId,
                    nodes.FirstOrDefault(node => string.Equals(node.Status, ProcessStepRunStatus.InProgress.ToString().ToLowerInvariant(), StringComparison.Ordinal))?.Id),
                ActiveInspectorTab = "runtime"
            },
            Chrome = BuildRunChrome()
        };
    }

    private static CanvasWorkbenchNode BuildDefinitionNode(
        ProcessStepEditorModel step,
        int index,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById,
        string? dependencyOutcomeTitle)
    {
        var status = step.RequiresApproval
            ? "approval"
            : step.RequiresDecisionRecord
                ? "review"
                : "draft";
        var profile = ProcessCanvasCatalog.GetStepKindProfile(step.StepKind);
        var paletteKey = step.StepKind switch
        {
            ProcessStepKind.Start => "sky",
            ProcessStepKind.Approval => "amber",
            ProcessStepKind.Decision => "violet",
            ProcessStepKind.End => "mint",
            _ => "neutral"
        };

        return new CanvasWorkbenchNode
        {
            Id = BuildDefinitionNodeId(step),
            Kind = ProcessCanvasCatalog.NodeKinds.DefinitionStep,
            Family = "process-definition",
            Icon = ResolveStepIcon(step.StepKind),
            Title = step.Title,
            Subtitle = string.IsNullOrWhiteSpace(step.Subtitle) ? step.StepKind.ToString() : step.Subtitle,
            LeadText = ResolveDefinitionLeadText(step, dependencyOutcomeTitle),
            Status = status,
            StatusPill = step.StepKind.ToString(),
            PaletteKey = paletteKey,
            AccentColor = ResolveAccentColor(step.StepKind),
            DurationLabel = step.TargetLeadHours > 0 ? $"{step.TargetLeadHours} h" : "Unbounded",
            X = step.CanvasX != 0 ? step.CanvasX : 140 + (index * 280),
            Y = step.CanvasY != 0 ? step.CanvasY : 180,
            Chips =
            [
                new CanvasWorkbenchChip
                {
                    Text = $"{step.RoleAssignments.Count} roles",
                    Tone = step.RoleAssignments.Count == 0 ? "danger" : "neutral"
                },
                new CanvasWorkbenchChip
                {
                    Text = $"{step.ArtifactExpectations.Count} artifacts",
                    Tone = step.ArtifactExpectations.Count == 0 ? "neutral" : "info"
                },
                new CanvasWorkbenchChip
                {
                    Text = $"{step.BranchOutcomes.Count} branches",
                    Tone = step.BranchOutcomes.Count == 0 ? "neutral" : "accent"
                }
            ],
            FooterChips = BuildDefinitionFooterChips(status, dependencyOutcomeTitle),
            ContextActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.EditDefinitionStep,
                    Label = "Edit step",
                    MenuLabel = "Edit step",
                    Icon = "draw",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.AddDependentStep,
                    Label = "Add dependent step",
                    MenuLabel = "Add dependent step",
                    Icon = "add_circle",
                    Tone = "info"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.AddBranchOutcome,
                    Label = "Add branch outcome",
                    MenuLabel = "Add branch outcome",
                    Icon = "call_split",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.AddRoleBinding,
                    Label = "Add role binding",
                    MenuLabel = "Add role binding",
                    Icon = "people",
                    Tone = "neutral"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.AddArtifactExpectation,
                    Label = "Add artifact expectation",
                    MenuLabel = "Add artifact expectation",
                    Icon = "description",
                    Tone = "neutral"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.RemoveDefinitionStep,
                    Label = "Remove step",
                    MenuLabel = "Remove step",
                    Icon = "delete",
                    Tone = "danger"
                }
            ],
            InputPorts = DecorateProcessPorts(BuildDefinitionStepInputPorts(step, rolesById, profile)),
            OutputPorts = DecorateProcessPorts(BuildDefinitionStepOutputPorts(step, profile))
        };
    }

    private static CanvasWorkbenchNode BuildDefinitionBranchNode(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepEditorModel> allSteps,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById)
    {
        var outcomes = ProcessCanvasBranching.GetOrderedBranchOutcomes(step);
        var decisionRoleTitle = step.DecisionRoleRequirementId.HasValue &&
            rolesById.TryGetValue(step.DecisionRoleRequirementId.Value, out var decisionRole)
                ? decisionRole.DisplayName
                : string.Empty;
        var routedDependents = allSteps.Count(candidate =>
            ProcessCanvasBranching.GetOrderedDependencies(candidate)
                .Any(dependency => dependency.DependsOnStepId == step.Id));

        return new CanvasWorkbenchNode
        {
            Id = BuildDefinitionBranchNodeId(step),
            Kind = ProcessCanvasCatalog.NodeKinds.DefinitionBranchRouter,
            Family = "special",
            Icon = "branch",
            Title = string.IsNullOrWhiteSpace(step.Title)
                ? "Route process outcomes"
                : $"{step.Title} routing",
            Subtitle = string.IsNullOrWhiteSpace(decisionRoleTitle)
                ? "Explicit branch router"
                : $"Decision maker: {decisionRoleTitle}",
            LeadText = string.IsNullOrWhiteSpace(step.DecisionRightsSummary)
                ? step.OutputContractSummary
                : step.DecisionRightsSummary,
            Status = "routing",
            StatusPill = "Routing",
            PaletteKey = "accent",
            AccentColor = ResolveAccentColor(ProcessStepKind.Decision),
            DurationLabel = $"{outcomes.Count} routes",
            X = ResolveDefinitionBranchNodeX(step, allSteps),
            Y = ResolveDefinitionBranchNodeY(step, allSteps),
            Chips =
            [
                new CanvasWorkbenchChip
                {
                    Text = $"{outcomes.Count} outputs",
                    Tone = "accent"
                },
                new CanvasWorkbenchChip
                {
                    Text = $"{routedDependents} downstream",
                    Tone = routedDependents == 0 ? "neutral" : "info"
                }
            ],
            FooterChips = string.IsNullOrWhiteSpace(decisionRoleTitle)
                ? []
                : [new CanvasWorkbenchChip { Text = decisionRoleTitle, Tone = "info" }],
            ContextActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.EditDefinitionStep,
                    Label = "Edit route",
                    MenuLabel = "Edit route",
                    Icon = "draw",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.AddDependentStep,
                    Label = "Add default path",
                    MenuLabel = "Add default path",
                    Icon = "add_circle",
                    Tone = "info"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.AddBranchOutcome,
                    Label = "Add branch outcome",
                    MenuLabel = "Add branch outcome",
                    Icon = "call_split",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.RemoveDefinitionStep,
                    Label = "Remove route",
                    MenuLabel = "Remove route",
                    Icon = "delete",
                    Tone = "danger"
                }
            ],
            InputPorts = DecorateProcessPorts(BuildDefinitionBranchInputPorts(decisionRoleTitle)),
            OutputPorts = DecorateProcessPorts(outcomes.Select(BuildDefinitionBranchOutputPort))
        };
    }

    private static CanvasWorkbenchNode BuildDefinitionRoleNode(
        ProcessRoleEditorModel role,
        int index,
        ProcessDefinitionEditorModel editor)
    {
        var assignmentCount = editor.Steps.Sum(step => step.RoleAssignments.Count(assignment => assignment.RoleRequirementId == role.Id));
        var decisionCount = editor.Steps.Count(step => step.DecisionRoleRequirementId == role.Id);

        return new CanvasWorkbenchNode
        {
            Id = BuildDefinitionRoleNodeId(role),
            Kind = ProcessCanvasCatalog.NodeKinds.DefinitionRole,
            Family = "group",
            Icon = "people",
            Title = string.IsNullOrWhiteSpace(role.DisplayName) ? $"Role {index + 1}" : role.DisplayName,
            Subtitle = string.IsNullOrWhiteSpace(role.PreferredExecutorKind)
                ? "Role contract"
                : role.PreferredExecutorKind,
            LeadText = string.IsNullOrWhiteSpace(role.Purpose)
                ? role.StaffingIntent
                : role.Purpose,
            Status = role.IsRequired ? "required" : "optional",
            StatusPill = role.IsRequired ? "Required" : "Optional",
            PaletteKey = "neutral",
            AccentColor = "#0f766e",
            DurationLabel = $"{role.DefaultAllocationPercent}%",
            X = ResolveDefinitionRoleNodeX(role, editor),
            Y = ResolveDefinitionRoleNodeY(role, index),
            Chips =
            [
                new CanvasWorkbenchChip
                {
                    Text = $"{assignmentCount} bindings",
                    Tone = assignmentCount == 0 ? "neutral" : "info"
                },
                new CanvasWorkbenchChip
                {
                    Text = $"{decisionCount} decisions",
                    Tone = decisionCount == 0 ? "neutral" : "accent"
                }
            ],
            FooterChips =
            [
                new CanvasWorkbenchChip
                {
                    Text = string.IsNullOrWhiteSpace(role.PreferredExecutorKind) ? "person" : role.PreferredExecutorKind,
                    Tone = "neutral"
                }
            ],
            ContextActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.EditDefinitionRole,
                    Label = "Edit role",
                    MenuLabel = "Edit role",
                    Icon = "draw",
                    Tone = "accent"
                }
            ],
            OutputPorts = DecorateProcessPorts(BuildDefinitionRoleOutputPorts(role, editor))
        };
    }

    private static CanvasWorkbenchNode BuildRunNode(ProcessStepRunViewModel stepRun)
    {
        var tone = ResolveRunTone(stepRun.Status);
        var profile = ProcessCanvasCatalog.GetStepKindProfile(stepRun.StepKind);
        return new CanvasWorkbenchNode
        {
            Id = BuildRunNodeId(stepRun.Id),
            Kind = ProcessCanvasCatalog.NodeKinds.RuntimeStep,
            Family = "process-runtime",
            Icon = ResolveStepIcon(stepRun.StepKind),
            Title = stepRun.Title,
            Subtitle = string.IsNullOrWhiteSpace(stepRun.CurrentExecutorName)
                ? "Unassigned"
                : stepRun.CurrentExecutorName,
            LeadText = !string.IsNullOrWhiteSpace(stepRun.BlockedReason)
                ? stepRun.BlockedReason
                : !string.IsNullOrWhiteSpace(stepRun.RefusalReason)
                    ? stepRun.RefusalReason
                    : !string.IsNullOrWhiteSpace(stepRun.SelectedBranchOutcomeTitle)
                        ? $"Selected branch: {stepRun.SelectedBranchOutcomeTitle}"
                    : stepRun.DecisionSummary,
            Status = stepRun.Status.ToString().ToLowerInvariant(),
            StatusPill = stepRun.Status.ToString(),
            PaletteKey = tone,
            AccentColor = ResolveRunAccentColor(stepRun.Status),
            DurationLabel = stepRun.TouchMinutes > 0
                ? $"{stepRun.TouchMinutes} m touch"
                : $"{stepRun.WaitMinutes} m wait",
            X = 140 + ((stepRun.Sequence - 1) * 280),
            Y = stepRun.Status == ProcessStepRunStatus.Blocked ? 260 : 180,
            Chips =
            [
                new CanvasWorkbenchChip
                {
                    Text = $"{stepRun.WaitMinutes} m wait",
                    Tone = "neutral"
                },
                new CanvasWorkbenchChip
                {
                    Text = $"{stepRun.ReworkCount} rework",
                    Tone = stepRun.ReworkCount > 0 ? "warn" : "neutral"
                }
            ],
            FooterChips =
            [
                new CanvasWorkbenchChip
                {
                    Text = stepRun.CapabilityGapSeverity.ToString(),
                    Tone = stepRun.CapabilityGapSeverity == ProcessCapabilityGapSeverity.None ? "neutral" : "danger"
                }
            ],
            ContextActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.RuntimeStart,
                    Label = "Start",
                    MenuLabel = "Start",
                    Icon = "play",
                    Tone = "info"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.RuntimeComplete,
                    Label = "Complete",
                    MenuLabel = "Complete",
                    Icon = "check",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.RuntimeBlock,
                    Label = "Block",
                    MenuLabel = "Block",
                    Icon = "block",
                    Tone = "danger"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.RuntimeRecordArtifact,
                    Label = "Record artifact",
                    MenuLabel = "Record artifact",
                    Icon = "note_add",
                    Tone = "neutral"
                }
            ],
            InputPorts = DecorateProcessPorts(BuildRunStepInputPorts(stepRun, profile)),
            OutputPorts = DecorateProcessPorts(BuildRunStepOutputPorts(stepRun, profile))
        };
    }

}
