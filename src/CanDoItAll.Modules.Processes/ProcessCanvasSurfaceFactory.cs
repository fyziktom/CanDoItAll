using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessCanvasSurfaceFactory
{
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

    private static List<CanvasWorkbenchLink> BuildDefinitionLinks(
        IReadOnlyList<ProcessStepEditorModel> steps,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById)
    {
        var stepsById = steps
            .Where(step => step.Id.HasValue)
            .ToDictionary(step => step.Id!.Value);
        var artifactOwnersById = steps
            .SelectMany(step => step.ArtifactExpectations
                .Where(artifact => artifact.Id.HasValue)
                .Select(artifact => new KeyValuePair<Guid, (ProcessStepEditorModel Step, ProcessArtifactExpectationEditorModel Artifact)>(
                    artifact.Id!.Value,
                    (step, artifact))))
            .ToDictionary(item => item.Key, item => item.Value);
        var links = new List<CanvasWorkbenchLink>();

        foreach (var step in steps.Where(ProcessCanvasBranching.ShouldRenderBranchRouter))
        {
            links.Add(new CanvasWorkbenchLink
            {
                SourceId = BuildDefinitionNodeId(step),
                SourcePortId = ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput,
                TargetId = BuildDefinitionBranchNodeId(step),
                TargetPortId = ProcessCanvasCatalog.DefinitionPorts.BranchStepInput,
                Kind = "flow",
                IsUserAuthored = true
            });

            if (step.DecisionRoleRequirementId.HasValue &&
                rolesById.ContainsKey(step.DecisionRoleRequirementId.Value))
            {
                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = BuildDefinitionRoleNodeId(rolesById[step.DecisionRoleRequirementId.Value]),
                    SourcePortId = ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput,
                    TargetId = BuildDefinitionBranchNodeId(step),
                    TargetPortId = ProcessCanvasCatalog.DefinitionPorts.BranchDecisionRoleInput,
                    Kind = "decision-role",
                    IsUserAuthored = true
                });
            }
        }

        foreach (var step in steps.Where(step => !ProcessCanvasBranching.ShouldRenderBranchRouter(step) && step.DecisionRoleRequirementId.HasValue))
        {
            if (!rolesById.TryGetValue(step.DecisionRoleRequirementId!.Value, out var decisionRole))
            {
                continue;
            }

            links.Add(new CanvasWorkbenchLink
            {
                SourceId = BuildDefinitionRoleNodeId(decisionRole),
                SourcePortId = ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput,
                TargetId = BuildDefinitionNodeId(step),
                TargetPortId = ProcessCanvasCatalog.DefinitionPorts.StepDecisionAuthorityInput,
                Kind = "decision-role",
                IsUserAuthored = true
            });
        }

        foreach (var step in steps)
        {
            foreach (var assignment in step.RoleAssignments
                         .Where(assignment => assignment.RoleRequirementId.HasValue &&
                             rolesById.ContainsKey(assignment.RoleRequirementId.Value)))
            {
                var role = rolesById[assignment.RoleRequirementId!.Value];
                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = BuildDefinitionRoleNodeId(role),
                    SourcePortId = ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(assignment.ResponsibilityKind),
                    TargetId = BuildDefinitionNodeId(step),
                    TargetPortId = ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(assignment.ResponsibilityKind),
                    Kind = "role-binding",
                    IsUserAuthored = true
                });
            }
        }

        foreach (var step in steps)
        {
            foreach (var dependency in ProcessCanvasBranching.GetOrderedDependencies(step)
                         .Where(dependency => dependency.DependsOnStepId.HasValue &&
                             stepsById.ContainsKey(dependency.DependsOnStepId.Value)))
            {
                var sourceStep = stepsById[dependency.DependsOnStepId!.Value];
                if (ProcessCanvasBranching.ShouldRenderBranchRouter(sourceStep))
                {
                    links.Add(new CanvasWorkbenchLink
                    {
                        SourceId = BuildDefinitionBranchNodeId(sourceStep),
                        SourcePortId = ProcessCanvasBranching.ResolveOutcomePortId(sourceStep, dependency.DependsOnBranchOutcomeId),
                        TargetId = BuildDefinitionNodeId(step),
                        TargetPortId = ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput,
                        Kind = "flow",
                        IsUserAuthored = true
                    });
                    continue;
                }

                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = BuildDefinitionNodeId(sourceStep),
                    SourcePortId = ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput,
                    TargetId = BuildDefinitionNodeId(step),
                    TargetPortId = ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput,
                    Kind = "flow",
                    IsUserAuthored = true
                });
            }

            foreach (var artifactInput in step.ArtifactInputs.Where(item => item.ArtifactExpectationId.HasValue))
            {
                if (!artifactOwnersById.TryGetValue(artifactInput.ArtifactExpectationId!.Value, out var artifactOwner))
                {
                    continue;
                }

                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = BuildDefinitionNodeId(artifactOwner.Step),
                    SourcePortId = ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(artifactOwner.Artifact),
                    TargetId = BuildDefinitionNodeId(step),
                    TargetPortId = ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs,
                    Kind = "artifact",
                    IsUserAuthored = true
                });
            }
        }

        return links;
    }

    private static List<CanvasWorkbenchLink> BuildRunLinks(IReadOnlyList<ProcessStepRunViewModel> stepRuns)
    {
        var runsByDefinitionId = stepRuns.ToDictionary(stepRun => stepRun.StepDefinitionId);
        var links = new List<CanvasWorkbenchLink>();

        foreach (var stepRun in stepRuns.Where(ProcessCanvasBranching.ShouldRenderBranchRouter))
        {
            links.Add(new CanvasWorkbenchLink
            {
                SourceId = BuildRunNodeId(stepRun.Id),
                SourcePortId = ProcessCanvasCatalog.RuntimePorts.StepStructuralOutput,
                TargetId = BuildRunBranchNodeId(stepRun.Id),
                TargetPortId = ProcessCanvasCatalog.RuntimePorts.BranchStepInput,
                Kind = "flow",
                IsUserAuthored = false
            });
        }

        foreach (var stepRun in stepRuns)
        {
            foreach (var dependency in stepRun.Dependencies
                         .Where(dependency => runsByDefinitionId.ContainsKey(dependency.DependsOnStepDefinitionId)))
            {
                var sourceStepRun = runsByDefinitionId[dependency.DependsOnStepDefinitionId];
                if (ProcessCanvasBranching.ShouldRenderBranchRouter(sourceStepRun))
                {
                    links.Add(new CanvasWorkbenchLink
                    {
                        SourceId = BuildRunBranchNodeId(sourceStepRun.Id),
                        SourcePortId = ProcessCanvasBranching.ResolveOutcomePortId(sourceStepRun, dependency.DependsOnBranchOutcomeId),
                        TargetId = BuildRunNodeId(stepRun.Id),
                        TargetPortId = ProcessCanvasCatalog.RuntimePorts.StepStructuralInput,
                        Kind = "flow",
                        IsUserAuthored = false
                    });
                    continue;
                }

                links.Add(new CanvasWorkbenchLink
                {
                    SourceId = BuildRunNodeId(sourceStepRun.Id),
                    SourcePortId = ProcessCanvasCatalog.RuntimePorts.StepStructuralOutput,
                    TargetId = BuildRunNodeId(stepRun.Id),
                    TargetPortId = ProcessCanvasCatalog.RuntimePorts.StepStructuralInput,
                    Kind = "flow",
                    IsUserAuthored = false
                });
            }
        }

        return links;
    }

    private static string BuildDefinitionNodeId(ProcessStepEditorModel step)
    {
        return ProcessCanvasBranching.BuildDefinitionStepNodeId(step);
    }

    private static string BuildDefinitionNodeId(Guid stepId)
    {
        return $"{ProcessCanvasBranching.DefinitionStepNodePrefix}{stepId:D}";
    }

    private static string BuildDefinitionBranchNodeId(ProcessStepEditorModel step)
    {
        return ProcessCanvasBranching.BuildDefinitionBranchNodeId(step);
    }

    private static string BuildDefinitionRoleNodeId(ProcessRoleEditorModel role)
    {
        return ProcessCanvasBranching.BuildDefinitionRoleNodeId(role);
    }

    private static string BuildRunNodeId(Guid stepRunId)
    {
        return ProcessCanvasBranching.BuildRuntimeStepNodeId(stepRunId);
    }

    private static string BuildRunBranchNodeId(Guid stepRunId)
    {
        return ProcessCanvasBranching.BuildRuntimeBranchNodeId(stepRunId);
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionBranchInputPorts(string decisionRoleTitle)
    {
        var ports = new List<CanvasWorkbenchPort>
        {
            new()
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.BranchStepInput,
                Label = "From step",
                Side = "left",
                Tone = "neutral",
                Kind = "source"
            }
        };
        ports.Add(new CanvasWorkbenchPort
        {
            Id = ProcessCanvasCatalog.DefinitionPorts.BranchDecisionRoleInput,
            Label = string.IsNullOrWhiteSpace(decisionRoleTitle)
                ? "Decision authority"
                : decisionRoleTitle,
            Side = "left",
            Tone = "info",
            Kind = "decision-role",
            IsRequired = !string.IsNullOrWhiteSpace(decisionRoleTitle)
        });

        return ports;
    }

    private static CanvasWorkbenchPort BuildDefinitionBranchOutputPort(ProcessStepBranchOutcomeEditorModel outcome)
    {
        return new CanvasWorkbenchPort
        {
            Id = ProcessCanvasBranching.BuildOutcomePortId(outcome),
            Label = string.IsNullOrWhiteSpace(outcome.Title) ? "Untitled route" : outcome.Title,
            Side = "right",
            Tone = ProcessCanvasBranching.IsErrorOutcome(outcome)
                ? "danger"
                : ProcessCanvasBranching.IsDefaultOutcome(outcome)
                    ? "neutral"
                    : "accent",
            Kind = outcome.Key
        };
    }

    private static CanvasWorkbenchPort BuildRunBranchOutputPort(ProcessStepBranchOutcomeOptionViewModel outcome)
    {
        var tone = string.Equals(outcome.Title, ProcessCanvasBranching.ErrorRouteTitle, StringComparison.OrdinalIgnoreCase)
            ? "danger"
            : string.Equals(outcome.Title, ProcessCanvasBranching.DefaultRouteTitle, StringComparison.OrdinalIgnoreCase)
                ? "neutral"
                : "accent";

        return new CanvasWorkbenchPort
        {
            Id = ProcessCanvasBranching.BuildOutcomePortId(outcome),
            Label = string.IsNullOrWhiteSpace(outcome.Title) ? "Untitled route" : outcome.Title,
            Side = "right",
            Tone = tone,
            Kind = "route"
        };
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionStepInputPorts(
        ProcessStepEditorModel step,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById,
        ProcessCanvasCatalog.StepKindProfile profile)
    {
        var ports = new List<CanvasWorkbenchPort>();
        if (profile.AllowsStructuralInput)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput,
                Label = "Inputs",
                Side = "left",
                Tone = ProcessCanvasBranching.GetOrderedDependencies(step).Count == 0 ? "neutral" : "info",
                Kind = "structural-input",
                IsRequired = step.StepKind != ProcessStepKind.Start
            });
        }

        if (profile.AllowsDecisionAuthorityInput && !ProcessCanvasBranching.ShouldRenderBranchRouter(step))
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.StepDecisionAuthorityInput,
                Label = ResolveDecisionAuthorityLabel(step.DecisionRoleRequirementId, rolesById),
                Side = "left",
                Tone = "info",
                Kind = "decision-role",
                IsRequired = step.DecisionRoleRequirementId.HasValue
            });
        }

        if (profile.AllowsParticipantInputs)
        {
            foreach (var responsibilityKind in ProcessCanvasCatalog.OrderedResponsibilities)
            {
                var hasBinding = step.RoleAssignments.Any(assignment => assignment.ResponsibilityKind == responsibilityKind);
                ports.Add(new CanvasWorkbenchPort
                {
                    Id = ProcessCanvasCatalog.DefinitionPorts.GetStepResponsibilityInputPortId(responsibilityKind),
                    Label = ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityKind),
                    Side = "left",
                    Tone = hasBinding ? "info" : "neutral",
                    Kind = responsibilityKind.ToString().ToLowerInvariant(),
                    IsRequired = step.RoleAssignments.Any(assignment => assignment.ResponsibilityKind == responsibilityKind && assignment.IsRequired)
                });
            }
        }

        if (profile.AllowsArtifactInputs)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs,
                Label = "Artifacts",
                Side = "left",
                Tone = step.ArtifactInputs.Count == 0 ? "neutral" : "accent",
                Kind = "artifact-input",
                IsRequired = step.ArtifactInputs.Count > 0
            });
        }

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionStepOutputPorts(
        ProcessStepEditorModel step,
        ProcessCanvasCatalog.StepKindProfile profile)
    {
        var ports = new List<CanvasWorkbenchPort>();
        if (profile.AllowsStructuralOutput)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput,
                Label = ProcessCanvasBranching.ShouldRenderBranchRouter(step) ? "Route" : "Next",
                Side = "right",
                Tone = ProcessCanvasBranching.ShouldRenderBranchRouter(step) ? "accent" : "neutral",
                Kind = "structural-output"
            });
        }

        if (profile.AllowsArtifactOutputs)
        {
            ports.AddRange(step.ArtifactExpectations
                .Where(artifact => !string.IsNullOrWhiteSpace(artifact.Title))
                .Select(artifact => new CanvasWorkbenchPort
                {
                    Id = ProcessCanvasCatalog.DefinitionPorts.BuildStepArtifactOutputPortId(artifact),
                    Label = artifact.Title,
                    Side = "right",
                    Tone = artifact.IsRequired ? "info" : "neutral",
                    Kind = "artifact-output",
                    IsRequired = artifact.IsRequired
                }));
        }

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildDefinitionRoleOutputPorts(
        ProcessRoleEditorModel role,
        ProcessDefinitionEditorModel editor)
    {
        var ports = new List<CanvasWorkbenchPort>();
        foreach (var responsibilityKind in ProcessCanvasCatalog.OrderedResponsibilities)
        {
            var bindingCount = editor.Steps.Sum(step => step.RoleAssignments.Count(assignment =>
                assignment.RoleRequirementId == role.Id &&
                assignment.ResponsibilityKind == responsibilityKind));
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.DefinitionPorts.GetRoleResponsibilityOutputPortId(responsibilityKind),
                Label = ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityKind),
                Side = "right",
                Tone = bindingCount == 0 ? "neutral" : "info",
                Kind = responsibilityKind.ToString().ToLowerInvariant()
            });
        }

        ports.Add(new CanvasWorkbenchPort
        {
            Id = ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput,
            Label = "Decision authority",
            Side = "right",
            Tone = "info",
            Kind = "decision"
        });

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildRunStepInputPorts(
        ProcessStepRunViewModel stepRun,
        ProcessCanvasCatalog.StepKindProfile profile)
    {
        var ports = new List<CanvasWorkbenchPort>();
        if (profile.AllowsStructuralInput)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.RuntimePorts.StepStructuralInput,
                Label = "Inputs",
                Side = "left",
                Tone = stepRun.Dependencies.Count == 0 ? "neutral" : "info",
                Kind = "structural-input",
                IsRequired = stepRun.StepKind != ProcessStepKind.Start
            });
        }

        if (profile.AllowsDecisionAuthorityInput &&
            !ProcessCanvasBranching.ShouldRenderBranchRouter(stepRun) &&
            (stepRun.DecisionRoleRequirementId.HasValue || !string.IsNullOrWhiteSpace(stepRun.DecisionRoleTitle)))
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.RuntimePorts.StepDecisionAuthorityInput,
                Label = string.IsNullOrWhiteSpace(stepRun.DecisionRoleTitle)
                    ? "Decision authority"
                    : stepRun.DecisionRoleTitle,
                Side = "left",
                Tone = "info",
                Kind = "decision-role",
                IsRequired = stepRun.DecisionRoleRequirementId.HasValue
            });
        }

        if (profile.AllowsParticipantInputs)
        {
            foreach (var responsibilityPort in stepRun.ResponsibilityPorts)
            {
                ports.Add(new CanvasWorkbenchPort
                {
                    Id = ProcessCanvasCatalog.RuntimePorts.GetStepResponsibilityInputPortId(responsibilityPort.ResponsibilityKind),
                    Label = responsibilityPort.AssignmentCount > 1
                        ? $"{ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityPort.ResponsibilityKind)} ({responsibilityPort.AssignmentCount})"
                        : ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibilityPort.ResponsibilityKind),
                    Side = "left",
                    Tone = "info",
                    Kind = responsibilityPort.ResponsibilityKind.ToString().ToLowerInvariant(),
                    IsRequired = responsibilityPort.IsRequired
                });
            }
        }

        if (profile.AllowsArtifactInputs && stepRun.ArtifactInputCount > 0)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.RuntimePorts.StepArtifactInputs,
                Label = stepRun.ArtifactInputCount == 1
                    ? "Artifact"
                    : $"Artifacts ({stepRun.ArtifactInputCount})",
                Side = "left",
                Tone = "accent",
                Kind = "artifact-input",
                IsRequired = true
            });
        }

        return ports;
    }

    private static List<CanvasWorkbenchPort> BuildRunStepOutputPorts(
        ProcessStepRunViewModel stepRun,
        ProcessCanvasCatalog.StepKindProfile profile)
    {
        var ports = new List<CanvasWorkbenchPort>();
        if (profile.AllowsStructuralOutput)
        {
            ports.Add(new CanvasWorkbenchPort
            {
                Id = ProcessCanvasCatalog.RuntimePorts.StepStructuralOutput,
                Label = ProcessCanvasBranching.ShouldRenderBranchRouter(stepRun) ? "Route" : "Next",
                Side = "right",
                Tone = ProcessCanvasBranching.ShouldRenderBranchRouter(stepRun) ? "accent" : "neutral",
                Kind = "structural-output"
                });
        }

        if (profile.AllowsArtifactOutputs)
        {
            ports.AddRange(stepRun.ArtifactOutputs
                .Where(artifact => !string.IsNullOrWhiteSpace(artifact.Title))
                .Select(artifact => new CanvasWorkbenchPort
                {
                    Id = $"{ProcessCanvasCatalog.RuntimePorts.StepArtifactOutputPrefix}{artifact.ArtifactExpectationId:D}",
                    Label = artifact.Title,
                    Side = "right",
                    Tone = artifact.IsRequired ? "info" : "neutral",
                    Kind = "artifact-output",
                    IsRequired = artifact.IsRequired
                }));
        }

        return ports;
    }

    private static string ResolveDecisionAuthorityLabel(
        Guid? roleId,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById)
    {
        if (!roleId.HasValue || !rolesById.TryGetValue(roleId.Value, out var role))
        {
            return "Decision authority";
        }

        return string.IsNullOrWhiteSpace(role.DisplayName)
            ? "Decision authority"
            : role.DisplayName;
    }

    private static double ResolveDefinitionBranchNodeX(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepEditorModel> allSteps)
    {
        if (step.BranchCanvasX != 0)
        {
            return step.BranchCanvasX;
        }

        var stepX = ResolveDefinitionStepX(step, allSteps);
        var directDependents = allSteps
            .Where(candidate => ProcessCanvasBranching.GetOrderedDependencies(candidate)
                .Any(dependency => dependency.DependsOnStepId == step.Id))
            .Select(candidate => ResolveDefinitionStepX(candidate, allSteps))
            .ToList();
        if (directDependents.Count == 0)
        {
            return stepX + 320d;
        }

        var closestDependentX = directDependents.Min();
        return closestDependentX - stepX < 420d
            ? stepX + 320d
            : stepX + ((closestDependentX - stepX) / 2d);
    }

    private static double ResolveDefinitionBranchNodeY(
        ProcessStepEditorModel step,
        IReadOnlyList<ProcessStepEditorModel> allSteps)
    {
        if (step.BranchCanvasY != 0)
        {
            return step.BranchCanvasY;
        }

        var stepY = ResolveDefinitionStepY(step);
        var directDependents = allSteps
            .Where(candidate => ProcessCanvasBranching.GetOrderedDependencies(candidate)
                .Any(dependency => dependency.DependsOnStepId == step.Id))
            .ToList();
        if (directDependents.Count == 0)
        {
            return stepY;
        }

        return directDependents.All(candidate => Math.Abs(ResolveDefinitionStepY(candidate) - stepY) < 90d)
            ? stepY + 220d
            : directDependents.Average(ResolveDefinitionStepY);
    }

    private static double ResolveDefinitionRoleNodeX(ProcessRoleEditorModel role, ProcessDefinitionEditorModel editor)
    {
        if (role.CanvasX != 0)
        {
            return role.CanvasX;
        }

        if (editor.Steps.Count == 0)
        {
            return -180d;
        }

        var leftMostStepX = editor.Steps
            .Select(step => step.CanvasX != 0 ? step.CanvasX : 140d)
            .Min();
        return leftMostStepX - 360d;
    }

    private static double ResolveDefinitionRoleNodeY(ProcessRoleEditorModel role, int index)
    {
        if (role.CanvasY != 0)
        {
            return role.CanvasY;
        }

        return 120d + (index * 210d);
    }

    private static double ResolveDefinitionStepX(ProcessStepEditorModel step, IReadOnlyList<ProcessStepEditorModel> allSteps)
    {
        var index = 0;
        for (var candidateIndex = 0; candidateIndex < allSteps.Count; candidateIndex++)
        {
            if (ReferenceEquals(allSteps[candidateIndex], step))
            {
                index = candidateIndex;
                break;
            }
        }

        return step.CanvasX != 0
            ? step.CanvasX
            : 140d + (index * 280d);
    }

    private static double ResolveDefinitionStepY(ProcessStepEditorModel step)
    {
        return step.CanvasY != 0
            ? step.CanvasY
            : 180d;
    }

    private static double ResolveRunBranchNodeX(
        ProcessStepRunViewModel stepRun,
        IReadOnlyList<ProcessStepRunViewModel> allSteps)
    {
        var directDependents = allSteps
            .Where(candidate => candidate.Dependencies.Any(dependency => dependency.DependsOnStepDefinitionId == stepRun.StepDefinitionId))
            .Select(candidate => 140d + ((candidate.Sequence - 1) * 280d))
            .ToList();
        var stepX = 140d + ((stepRun.Sequence - 1) * 280d);
        if (directDependents.Count == 0)
        {
            return stepX + 320d;
        }

        var closestDependentX = directDependents.Min();
        return closestDependentX - stepX < 420d
            ? stepX + 320d
            : stepX + ((closestDependentX - stepX) / 2d);
    }

    private static double ResolveRunBranchNodeY(
        ProcessStepRunViewModel stepRun,
        IReadOnlyList<ProcessStepRunViewModel> allSteps)
    {
        var directDependents = allSteps
            .Where(candidate => candidate.Dependencies.Any(dependency => dependency.DependsOnStepDefinitionId == stepRun.StepDefinitionId))
            .ToList();
        var stepY = stepRun.Status == ProcessStepRunStatus.Blocked ? 260d : 180d;
        if (directDependents.Count == 0)
        {
            return stepY;
        }

        return directDependents.All(candidate => candidate.Status != ProcessStepRunStatus.Blocked)
            ? stepY + 220d
            : 260d;
    }

    private static CanvasWorkbenchChrome BuildDefinitionChrome()
    {
        return new CanvasWorkbenchChrome
        {
            HintText = "Select a step to edit, drag nodes to improve the process map, and keep role bindings explicit.",
            EmptyStateKicker = "Process design",
            EmptyStateTitle = "Add or select a process step",
            EmptyStateDescription = "The canvas mirrors the current step sequence, role bindings, and contract shape.",
            CollapseOnDoubleClick = false,
            ConnectorAnchors = new CanvasWorkbenchConnectorAnchorOptions
            {
                PlacementMode = "Horizontal"
            },
            QuickCreateActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateRoleBlank,
                    Label = "Blank role",
                    MenuLabel = "Add blank role",
                    Description = "Add a new role requirement and decide later who can fulfill it.",
                    Icon = "person_add",
                    Tone = "neutral"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateStepImplementation,
                    Label = "Implementation",
                    MenuLabel = "Add implementation step",
                    Description = "Add a standard execution step with proof-oriented defaults.",
                    Icon = "plus",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateStepDecision,
                    Label = "Decision step",
                    MenuLabel = "Add decision step",
                    Description = "Add a switch-style decision node with explicit branch outcomes.",
                    Icon = "call_split",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateStepReleaseApproval,
                    Label = "Approval step",
                    MenuLabel = "Add approval step",
                    Description = "Add an explicit approval gate.",
                    Icon = "check",
                    Tone = "warn"
                }
            ],
            GroupContextActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.OpenDefinitionToolbox,
                    Label = "Open toolbox",
                    MenuLabel = "Open toolbox",
                    Description = "Open the floating process templates toolbox.",
                    Icon = "inventory_2",
                    Tone = "neutral"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateRoleBlank,
                    Label = "Add role",
                    MenuLabel = "Add role",
                    Description = "Open the role-first editor and decide whether to start blank or from a template.",
                    Icon = "person_add",
                    Tone = "neutral"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateStepImplementation,
                    Label = "Add step",
                    MenuLabel = "Add step",
                    Description = "Open the step editor and choose whether to start from a template or a blank implementation step.",
                    Icon = "add_circle",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateStepDecision,
                    Label = "Decision router",
                    MenuLabel = "Add decision router",
                    Description = "Add a branching node and define typed switch outcomes on the canvas.",
                    Icon = "call_split",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateRoleSolutionArchitect,
                    Label = "Architect role",
                    MenuLabel = "Add architect role",
                    Description = "Add an architecture authority role template.",
                    Icon = "architecture",
                    Tone = "neutral"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateStepArchitecture,
                    Label = "Architecture step",
                    MenuLabel = "Add architecture step",
                    Description = "Add a design review step with decision-record defaults.",
                    Icon = "hub",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.CreateStepQa,
                    Label = "QA gate",
                    MenuLabel = "Add QA gate",
                    Description = "Add a regression and release-confidence step.",
                    Icon = "fact_check",
                    Tone = "accent"
                }
            ]
        };
    }

    private static string ResolveDefinitionLeadText(ProcessStepEditorModel step, string? dependencyOutcomeTitle)
    {
        if (!string.IsNullOrWhiteSpace(dependencyOutcomeTitle))
        {
            return string.IsNullOrWhiteSpace(step.OutputContractSummary)
                ? $"Continues when '{dependencyOutcomeTitle}' is selected."
                : $"{step.OutputContractSummary} Path: {dependencyOutcomeTitle}.";
        }

        return step.OutputContractSummary;
    }

    private static List<CanvasWorkbenchChip> BuildDefinitionFooterChips(string status, string? dependencyOutcomeTitle)
    {
        var chips = new List<CanvasWorkbenchChip>
        {
            new()
            {
                Text = status,
                Tone = status switch
                {
                    "approval" => "warn",
                    "review" => "accent",
                    _ => "neutral"
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(dependencyOutcomeTitle))
        {
            chips.Add(new CanvasWorkbenchChip
            {
                Text = dependencyOutcomeTitle,
                Tone = "accent"
            });
        }

        return chips;
    }

    private static CanvasWorkbenchChrome BuildRunChrome()
    {
        return new CanvasWorkbenchChrome
        {
            HintText = "Follow the active run, inspect blocked steps, and keep executor changes explicit.",
            EmptyStateKicker = "Process runtime",
            EmptyStateTitle = "Choose a run to inspect",
            EmptyStateDescription = "The runtime canvas shows the current step states, capability gaps, and executor assignments.",
            CollapseOnDoubleClick = false,
            ShowQuickCreateRail = false,
            GroupContextActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = ProcessCanvasActionIds.RuntimeRecordArtifact,
                    Label = "Record artifact",
                    MenuLabel = "Record artifact",
                    Description = "Open runtime evidence capture for the selected step or run.",
                    Icon = "note_add",
                    Tone = "neutral"
                }
            ]
        };
    }

    private static string ResolveStepIcon(ProcessStepKind stepKind)
    {
        return stepKind switch
        {
            ProcessStepKind.Start => "play",
            ProcessStepKind.Decision => "branch",
            ProcessStepKind.Approval => "check",
            ProcessStepKind.Review => "search",
            ProcessStepKind.Delivery => "send",
            ProcessStepKind.End => "stop",
            _ => "task"
        };
    }

    private static string ResolveAccentColor(ProcessStepKind stepKind)
    {
        return stepKind switch
        {
            ProcessStepKind.Start => "#0284c7",
            ProcessStepKind.Approval => "#d97706",
            ProcessStepKind.Decision => "#7c3aed",
            ProcessStepKind.End => "#059669",
            _ => "#475569"
        };
    }

    private static string ResolveRunTone(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Completed => "mint",
            ProcessStepRunStatus.InProgress => "sky",
            ProcessStepRunStatus.Blocked => "danger",
            ProcessStepRunStatus.Refused => "warn",
            ProcessStepRunStatus.WaitingApproval => "accent",
            _ => "neutral"
        };
    }

    private static string ResolveRunAccentColor(ProcessStepRunStatus status)
    {
        return status switch
        {
            ProcessStepRunStatus.Completed => "#059669",
            ProcessStepRunStatus.InProgress => "#0284c7",
            ProcessStepRunStatus.Blocked => "#dc2626",
            ProcessStepRunStatus.Refused => "#ea580c",
            ProcessStepRunStatus.WaitingApproval => "#7c3aed",
            _ => "#475569"
        };
    }

    private static List<CanvasWorkbenchPort> DecorateProcessPorts(IEnumerable<CanvasWorkbenchPort> ports)
    {
        return ports
            .Select(DecorateProcessPort)
            .ToList();
    }

    private static CanvasWorkbenchPort DecorateProcessPort(CanvasWorkbenchPort port)
    {
        ArgumentNullException.ThrowIfNull(port);

        var visual = ResolveProcessPortVisual(port);
        port.CategoryKey = visual.CategoryKey;
        port.AccentColor = visual.AccentColor;
        return port;
    }

    private static ProcessCanvasCatalog.ConnectionVisual ResolveProcessPortVisual(CanvasWorkbenchPort port)
    {
        if (ProcessCanvasCatalog.DefinitionPorts.TryGetRoleResponsibilityKind(port.Id, out var responsibilityKind) ||
            ProcessCanvasCatalog.DefinitionPorts.TryGetStepResponsibilityKind(port.Id, out responsibilityKind) ||
            TryResolveRuntimeResponsibilityKind(port.Id, out responsibilityKind))
        {
            return ProcessCanvasCatalog.GetResponsibilityVisual(responsibilityKind);
        }

        if (string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.RoleDecisionAuthorityOutput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.StepDecisionAuthorityInput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.BranchDecisionRoleInput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.RuntimePorts.StepDecisionAuthorityInput, StringComparison.Ordinal))
        {
            return ProcessCanvasCatalog.GetConnectionVisual(ProcessCanvasCatalog.PortFamily.StepDecisionAuthorityInput);
        }

        if (string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.StepStructuralInput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.StepStructuralOutput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.BranchStepInput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.RuntimePorts.StepStructuralInput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.RuntimePorts.StepStructuralOutput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.RuntimePorts.BranchStepInput, StringComparison.Ordinal))
        {
            return ProcessCanvasCatalog.GetConnectionVisual(ProcessCanvasCatalog.PortFamily.StepStructuralInput);
        }

        if (string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputs, StringComparison.Ordinal) ||
            port.Id.StartsWith(ProcessCanvasCatalog.DefinitionPorts.StepArtifactInputPrefix, StringComparison.Ordinal) ||
            port.Id.StartsWith(ProcessCanvasCatalog.DefinitionPorts.StepArtifactOutputPrefix, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.RuntimePorts.StepArtifactInputs, StringComparison.Ordinal) ||
            port.Id.StartsWith(ProcessCanvasCatalog.RuntimePorts.StepArtifactOutputPrefix, StringComparison.Ordinal))
        {
            return ProcessCanvasCatalog.GetConnectionVisual(ProcessCanvasCatalog.PortFamily.StepArtifactInput);
        }

        if (port.Id.StartsWith(ProcessCanvasCatalog.DefinitionPorts.BranchOutcomeOutputPrefix, StringComparison.Ordinal) ||
            port.Id.StartsWith(ProcessCanvasCatalog.RuntimePorts.BranchOutcomeOutputPrefix, StringComparison.Ordinal))
        {
            return ResolveBranchVisual(port.Label);
        }

        return ProcessCanvasCatalog.GetConnectionVisual(ProcessCanvasCatalog.PortFamily.StepStructuralInput);
    }

    private static bool TryResolveRuntimeResponsibilityKind(string? portId, out ProcessResponsibilityKind responsibilityKind)
    {
        responsibilityKind = default;
        if (string.IsNullOrWhiteSpace(portId))
        {
            return false;
        }

        foreach (var candidate in ProcessCanvasCatalog.OrderedResponsibilities)
        {
            if (string.Equals(portId, ProcessCanvasCatalog.RuntimePorts.GetStepResponsibilityInputPortId(candidate), StringComparison.Ordinal))
            {
                responsibilityKind = candidate;
                return true;
            }
        }

        return false;
    }

    private static ProcessCanvasCatalog.ConnectionVisual ResolveBranchVisual(string? label)
    {
        if (string.Equals(label, ProcessCanvasBranching.DefaultRouteTitle, StringComparison.OrdinalIgnoreCase))
        {
            return ProcessCanvasCatalog.GetConnectionVisual(ProcessCanvasCatalog.PortFamily.BranchDefaultOutput);
        }

        if (string.Equals(label, ProcessCanvasBranching.ErrorRouteTitle, StringComparison.OrdinalIgnoreCase))
        {
            return ProcessCanvasCatalog.GetConnectionVisual(ProcessCanvasCatalog.PortFamily.BranchErrorOutput);
        }

        return ProcessCanvasCatalog.GetConnectionVisual(ProcessCanvasCatalog.PortFamily.BranchOutcomeOutput);
    }

    private static List<string> ResolveSelectedNodeIds(
        IReadOnlyList<CanvasWorkbenchNode> nodes,
        string? selectedNodeId,
        string? fallbackSelectedNodeId = null)
    {
        if (string.Equals(selectedNodeId, "__processes:none__", StringComparison.Ordinal))
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(selectedNodeId) &&
            nodes.Any(node => string.Equals(node.Id, selectedNodeId, StringComparison.Ordinal)))
        {
            return [selectedNodeId];
        }

        if (!string.IsNullOrWhiteSpace(fallbackSelectedNodeId) &&
            nodes.Any(node => string.Equals(node.Id, fallbackSelectedNodeId, StringComparison.Ordinal)))
        {
            return [fallbackSelectedNodeId];
        }

        return nodes.Count > 0 ? [nodes[0].Id] : [];
    }
}
