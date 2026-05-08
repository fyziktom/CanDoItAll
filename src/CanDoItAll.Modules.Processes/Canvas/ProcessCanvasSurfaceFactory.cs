using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessCanvasSurfaceFactory
{
    private const double DefinitionRoleInstanceOffsetX = 430d;
    private const double DefinitionRoleInstanceRowGap = 260d;
    private const double DefinitionArtifactNodeOffsetX = 360d;
    private const double DefinitionArtifactCloneOffsetX = 260d;
    private const double DefinitionArtifactNodeRowGap = 230d;

    private readonly ProcessCanvasChromeCatalogService chromeCatalogService;

    public ProcessCanvasSurfaceFactory(ProcessCanvasChromeCatalogService chromeCatalogService)
    {
        this.chromeCatalogService = chromeCatalogService;
    }

    public CanvasWorkbenchSurface BuildDefinitionSurface(
        ProcessDefinitionEditorModel editor,
        string? selectedNodeId = null,
        string mode = "authoring",
        IReadOnlyList<ProcessCanvasArtifactCloneDraft>? artifactCloneDrafts = null)
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
                var dependencyOutcomeTitles = ProcessCanvasBranching.GetOrderedDependencies(step)
                    .Where(dependency => dependency.DependsOnBranchOutcomeId.HasValue)
                    .Select(dependency => dependency.DependsOnBranchOutcomeId!.Value)
                    .Distinct()
                    .Where(branchOutcomeTitles.ContainsKey)
                    .Select(branchOutcomeId => branchOutcomeTitles[branchOutcomeId])
                    .ToList();
                return BuildDefinitionNode(
                    step,
                    index,
                    rolesById,
                    dependencyOutcomeTitles);
            })
            .ToList();
        var branchNodes = editor.Steps
            .Where(ProcessCanvasBranching.ShouldRenderBranchRouter)
            .Select(step => BuildDefinitionBranchNode(step, editor.Steps, rolesById))
            .ToList();
        var roleNodePlans = BuildDefinitionRoleNodePlans(editor, rolesById);
        var roleNodeIds = BuildDefinitionRoleNodeIdMap(roleNodePlans);
        var roleNodes = roleNodePlans
            .Select(plan => BuildDefinitionRoleNode(plan, editor))
            .ToList();
        var artifactNodePlans = BuildDefinitionArtifactNodePlans(editor, roleNodePlans, artifactCloneDrafts ?? []);
        var artifactNodes = artifactNodePlans
            .Select(BuildDefinitionArtifactNode)
            .ToList();
        var nodes = new List<CanvasWorkbenchNode>(stepNodes.Count + branchNodes.Count + roleNodes.Count + artifactNodes.Count);
        nodes.AddRange(stepNodes);
        nodes.AddRange(branchNodes);
        nodes.AddRange(roleNodes);
        nodes.AddRange(artifactNodes);
        var links = BuildDefinitionLinks(editor.Steps, rolesById, editor.MessagingPolicies, roleNodeIds);

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
                SelectedNodeIds = ResolveSelectedNodeIds(nodes, selectedNodeId, ResolveSelectedRoleInstanceNodeId(nodes, selectedNodeId)),
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
        IReadOnlyList<string> dependencyOutcomeTitles)
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
            ProcessStepKind.Subprocess => "info",
            ProcessStepKind.End => "mint",
            _ => "neutral"
        };

        return new CanvasWorkbenchNode
        {
            Id = BuildDefinitionNodeId(step),
            Kind = ProcessCanvasCatalog.NodeKinds.DefinitionStep,
            Family = step.StepKind == ProcessStepKind.Subprocess ? "process-subprocess" : "process-definition",
            Icon = ResolveStepIcon(step.StepKind),
            Title = step.Title,
            Subtitle = ResolveDefinitionSubtitle(step),
            LeadText = ResolveDefinitionLeadText(step, dependencyOutcomeTitles),
            Status = status,
            StatusPill = step.StepKind.ToString(),
            PaletteKey = paletteKey,
            AccentColor = ResolveAccentColor(step.StepKind),
            DurationLabel = step.TargetLeadHours > 0 ? $"{step.TargetLeadHours} h" : "Unbounded",
            X = step.CanvasX != 0 ? step.CanvasX : 140 + (index * 280),
            Y = step.CanvasY != 0 ? step.CanvasY : 180,
            Chips = BuildDefinitionNodeChips(step),
            FooterChips = BuildDefinitionFooterChips(status, dependencyOutcomeTitles),
            ContextActions = BuildDefinitionStepContextActions(step),
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
        DefinitionRoleNodePlan plan,
        ProcessDefinitionEditorModel editor)
    {
        var role = plan.Role;
        var assignmentCount = plan.RelatedStep is null
            ? editor.Steps.Sum(step => step.RoleAssignments.Count(assignment => assignment.RoleRequirementId == role.Id))
            : plan.Responsibilities.Count;
        var decisionCount = plan.RelatedStep is null
            ? editor.Steps.Count(step => step.DecisionRoleRequirementId == role.Id)
            : plan.IsDecisionAuthority ? 1 : 0;
        var outboundMessagingCount = editor.MessagingPolicies.Count(item => item.SourceRoleRequirementId == role.Id);
        var inboundMessagingCount = editor.MessagingPolicies.Count(item => item.TargetRoleRequirementId == role.Id);

        return new CanvasWorkbenchNode
        {
            Id = plan.NodeId,
            Kind = ProcessCanvasCatalog.NodeKinds.DefinitionRole,
            Family = "group",
            Icon = "people",
            Title = string.IsNullOrWhiteSpace(role.DisplayName) ? $"Role {plan.RoleIndex + 1}" : role.DisplayName,
            Subtitle = ResolveDefinitionRoleSubtitle(plan),
            LeadText = string.IsNullOrWhiteSpace(role.Purpose)
                ? role.StaffingIntent
                : role.Purpose,
            Status = role.IsRequired ? "required" : "optional",
            StatusPill = plan.RelatedStep is null
                ? role.IsRequired ? "Required" : "Optional"
                : "Role instance",
            PaletteKey = "neutral",
            AccentColor = "#0f766e",
            DurationLabel = $"{role.DefaultAllocationPercent}%",
            X = ResolveDefinitionRoleNodeX(plan, editor),
            Y = ResolveDefinitionRoleNodeY(plan),
            Chips = BuildDefinitionRoleNodeChips(plan, assignmentCount, decisionCount, outboundMessagingCount, inboundMessagingCount),
            FooterChips = BuildDefinitionRoleNodeFooterChips(plan),
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
            InputPorts = DecorateProcessPorts(plan.RelatedStep is null
                ? BuildDefinitionRoleInputPorts(role, editor)
                : []),
            OutputPorts = DecorateProcessPorts(plan.RelatedStep is null
                ? BuildDefinitionRoleOutputPorts(role, editor)
                : BuildDefinitionRoleInstanceOutputPorts(plan))
        };
    }

    private static CanvasWorkbenchNode BuildDefinitionArtifactNode(DefinitionArtifactNodePlan plan)
    {
        var artifactTitle = ResolveArtifactTitle(plan.Artifact);
        return new CanvasWorkbenchNode
        {
            Id = plan.NodeId,
            Kind = ProcessCanvasCatalog.NodeKinds.DefinitionArtifact,
            Family = "special",
            Icon = "description",
            Title = artifactTitle,
            Subtitle = ResolveDefinitionArtifactSubtitle(plan),
            LeadText = ResolveDefinitionArtifactLeadText(plan),
            Status = plan.IsDraft
                ? "draft"
                : plan.Artifact.IsRequired ? "required" : "optional",
            StatusPill = plan.IsClone ? "Clone" : "Artifact",
            PaletteKey = "neutral",
            AccentColor = "#db2777",
            DurationLabel = plan.Artifact.ArtifactKind.ToString(),
            X = ResolveDefinitionArtifactNodeX(plan),
            Y = ResolveDefinitionArtifactNodeY(plan),
            Chips = BuildDefinitionArtifactNodeChips(plan),
            FooterChips = BuildDefinitionArtifactFooterChips(plan),
            ContextActions = BuildDefinitionArtifactContextActions(),
            InputPorts = DecorateProcessPorts(plan.IsClone ? [] : BuildDefinitionArtifactInputPorts()),
            OutputPorts = DecorateProcessPorts(BuildDefinitionArtifactOutputPorts())
        };
    }

    private static List<DefinitionArtifactNodePlan> BuildDefinitionArtifactNodePlans(
        ProcessDefinitionEditorModel editor,
        IReadOnlyList<DefinitionRoleNodePlan> roleNodePlans,
        IReadOnlyList<ProcessCanvasArtifactCloneDraft> artifactCloneDrafts)
    {
        var roleInstanceCountByStepId = roleNodePlans
            .Where(plan => plan.RelatedStep?.Id.HasValue == true)
            .GroupBy(plan => plan.RelatedStep!.Id!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var ownerCandidates = editor.Steps
            .SelectMany((step, stepIndex) => step.ArtifactExpectations
                .Select((artifact, artifactIndex) => new DefinitionArtifactOwner(step, artifact, artifactIndex))
                .Select(owner => owner with { StepIndex = stepIndex })
                .Where(owner => owner.Artifact.Id is { } artifactId && artifactId != Guid.Empty))
            .ToList();
        var artifactOwnersById = ownerCandidates
            .GroupBy(owner => owner.Artifact.Id!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var plans = new List<DefinitionArtifactNodePlan>();

        foreach (var owner in ownerCandidates)
        {
            plans.Add(DefinitionArtifactNodePlan.CreateSource(owner));
        }

        for (var targetStepIndex = 0; targetStepIndex < editor.Steps.Count; targetStepIndex++)
        {
            var targetStep = editor.Steps[targetStepIndex];
            var artifactInputs = targetStep.ArtifactInputs
                .Where(input => input.ArtifactExpectationId.HasValue &&
                    artifactOwnersById.ContainsKey(input.ArtifactExpectationId.Value))
                .ToList();
            var relatedRoleInstanceCount = targetStep.Id.HasValue
                ? roleInstanceCountByStepId.GetValueOrDefault(targetStep.Id.Value)
                : 0;

            for (var artifactInputIndex = 0; artifactInputIndex < artifactInputs.Count; artifactInputIndex++)
            {
                var artifactInput = artifactInputs[artifactInputIndex];
                var owner = artifactOwnersById[artifactInput.ArtifactExpectationId!.Value];
                plans.Add(DefinitionArtifactNodePlan.CreateInputClone(
                    owner,
                    targetStep,
                    artifactInput,
                    targetStepIndex,
                    artifactInputIndex,
                    relatedRoleInstanceCount));
            }
        }

        foreach (var draft in artifactCloneDrafts)
        {
            if (!artifactOwnersById.TryGetValue(draft.ArtifactExpectationId, out var owner))
            {
                continue;
            }

            plans.Add(DefinitionArtifactNodePlan.CreateDraftClone(owner, draft));
        }

        return plans;
    }


    private static List<DefinitionRoleNodePlan> BuildDefinitionRoleNodePlans(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById)
    {
        var participations = BuildDefinitionRoleParticipations(editor, rolesById);
        var participationsByRoleId = participations
            .GroupBy(participation => participation.Role.Id!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var messagingRoleIds = editor.MessagingPolicies
            .SelectMany(policy => new[] { policy.SourceRoleRequirementId, policy.TargetRoleRequirementId })
            .Where(roleId => roleId.HasValue)
            .Select(roleId => roleId!.Value)
            .ToHashSet();
        var plans = new List<DefinitionRoleNodePlan>();

        for (var roleIndex = 0; roleIndex < editor.Roles.Count; roleIndex++)
        {
            var role = editor.Roles[roleIndex];
            if (!role.Id.HasValue ||
                !participationsByRoleId.TryGetValue(role.Id.Value, out var roleParticipations) ||
                roleParticipations.Count <= 1)
            {
                plans.Add(DefinitionRoleNodePlan.CreateContract(role, roleIndex));
                continue;
            }

            foreach (var participation in roleParticipations)
            {
                plans.Add(DefinitionRoleNodePlan.CreateInstance(role, roleIndex, participation));
            }

            if (messagingRoleIds.Contains(role.Id.Value))
            {
                plans.Add(DefinitionRoleNodePlan.CreateContract(role, roleIndex));
            }
        }

        return plans;
    }

    private static Dictionary<DefinitionRoleNodeKey, string> BuildDefinitionRoleNodeIdMap(
        IReadOnlyList<DefinitionRoleNodePlan> plans)
    {
        var map = new Dictionary<DefinitionRoleNodeKey, string>();
        foreach (var plan in plans)
        {
            if (!plan.Role.Id.HasValue)
            {
                continue;
            }

            map[new DefinitionRoleNodeKey(plan.Role.Id.Value, plan.RelatedStep?.Id)] = plan.NodeId;
        }

        return map;
    }

    private static List<DefinitionRoleParticipation> BuildDefinitionRoleParticipations(
        ProcessDefinitionEditorModel editor,
        IReadOnlyDictionary<Guid, ProcessRoleEditorModel> rolesById)
    {
        var roleIndexById = editor.Roles
            .Select((role, index) => new { role, index })
            .Where(item => item.role.Id.HasValue)
            .ToDictionary(item => item.role.Id!.Value, item => item.index);
        var builders = new Dictionary<DefinitionRoleParticipationKey, DefinitionRoleParticipationBuilder>();

        for (var stepIndex = 0; stepIndex < editor.Steps.Count; stepIndex++)
        {
            var step = editor.Steps[stepIndex];
            if (!step.Id.HasValue)
            {
                continue;
            }

            foreach (var assignment in step.RoleAssignments.Where(assignment => assignment.RoleRequirementId.HasValue))
            {
                var roleId = assignment.RoleRequirementId!.Value;
                if (!rolesById.TryGetValue(roleId, out var role))
                {
                    continue;
                }

                var builder = ResolveRoleParticipationBuilder(
                    builders,
                    role,
                    step,
                    stepIndex,
                    roleIndexById.GetValueOrDefault(roleId, int.MaxValue));
                if (!builder.Responsibilities.Contains(assignment.ResponsibilityKind))
                {
                    builder.Responsibilities.Add(assignment.ResponsibilityKind);
                }
            }

            if (!step.DecisionRoleRequirementId.HasValue ||
                !rolesById.TryGetValue(step.DecisionRoleRequirementId.Value, out var decisionRole))
            {
                continue;
            }

            ResolveRoleParticipationBuilder(
                    builders,
                    decisionRole,
                    step,
                    stepIndex,
                    roleIndexById.GetValueOrDefault(step.DecisionRoleRequirementId.Value, int.MaxValue))
                .IsDecisionAuthority = true;
        }

        var ordered = builders.Values
            .OrderBy(builder => builder.StepIndex)
            .ThenBy(builder => builder.RoleIndex)
            .ToList();
        var stepInstanceCounts = ordered
            .GroupBy(builder => builder.Step.Id!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var stepInstanceCursors = new Dictionary<Guid, int>();
        var result = new List<DefinitionRoleParticipation>(ordered.Count);

        foreach (var builder in ordered)
        {
            var stepId = builder.Step.Id!.Value;
            var stepRoleIndex = stepInstanceCursors.GetValueOrDefault(stepId);
            stepInstanceCursors[stepId] = stepRoleIndex + 1;
            result.Add(new DefinitionRoleParticipation(
                builder.Role,
                builder.Step,
                [.. builder.Responsibilities],
                builder.IsDecisionAuthority,
                builder.StepIndex,
                stepRoleIndex,
                stepInstanceCounts[stepId]));
        }

        return result;
    }

    private static DefinitionRoleParticipationBuilder ResolveRoleParticipationBuilder(
        IDictionary<DefinitionRoleParticipationKey, DefinitionRoleParticipationBuilder> builders,
        ProcessRoleEditorModel role,
        ProcessStepEditorModel step,
        int stepIndex,
        int roleIndex)
    {
        var key = new DefinitionRoleParticipationKey(role.Id!.Value, step.Id!.Value);
        if (builders.TryGetValue(key, out var builder))
        {
            return builder;
        }

        builder = new DefinitionRoleParticipationBuilder(role, step, stepIndex, roleIndex);
        builders[key] = builder;
        return builder;
    }

    private static string? ResolveSelectedRoleInstanceNodeId(
        IReadOnlyList<CanvasWorkbenchNode> nodes,
        string? selectedNodeId)
    {
        if (!ProcessCanvasBranching.TryResolveDefinitionRoleToken(selectedNodeId, out var selectedRoleToken))
        {
            return null;
        }

        return nodes.FirstOrDefault(node =>
            ProcessCanvasBranching.TryResolveDefinitionRoleInstanceTokens(node.Id, out var roleToken, out _) &&
            string.Equals(roleToken, selectedRoleToken, StringComparison.Ordinal))?.Id;
    }

    private static string ResolveDefinitionRoleSubtitle(DefinitionRoleNodePlan plan)
    {
        if (plan.RelatedStep is null)
        {
            return string.IsNullOrWhiteSpace(plan.Role.PreferredExecutorKind)
                ? "Role contract"
                : plan.Role.PreferredExecutorKind;
        }

        return string.IsNullOrWhiteSpace(plan.RelatedStep.Title)
            ? "Step role"
            : plan.RelatedStep.Title;
    }

    private static List<CanvasWorkbenchChip> BuildDefinitionRoleNodeChips(
        DefinitionRoleNodePlan plan,
        int assignmentCount,
        int decisionCount,
        int outboundMessagingCount,
        int inboundMessagingCount)
    {
        if (plan.RelatedStep is null)
        {
            return
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
                },
                new CanvasWorkbenchChip
                {
                    Text = $"{outboundMessagingCount} out / {inboundMessagingCount} in",
                    Tone = outboundMessagingCount == 0 && inboundMessagingCount == 0 ? "neutral" : "accent"
                }
            ];
        }

        var chips = plan.Responsibilities
            .Select(responsibility => new CanvasWorkbenchChip
            {
                Text = ProcessCanvasCatalog.DefinitionPorts.GetResponsibilityLabel(responsibility),
                Tone = "info"
            })
            .ToList();
        if (plan.IsDecisionAuthority)
        {
            chips.Add(new CanvasWorkbenchChip
            {
                Text = "Decision",
                Tone = "accent"
            });
        }

        return chips.Count == 0
            ? [new CanvasWorkbenchChip { Text = "Role binding", Tone = "info" }]
            : chips;
    }

    private static List<CanvasWorkbenchChip> BuildDefinitionRoleNodeFooterChips(DefinitionRoleNodePlan plan)
    {
        var executorKind = string.IsNullOrWhiteSpace(plan.Role.PreferredExecutorKind)
            ? "person"
            : plan.Role.PreferredExecutorKind;
        if (plan.RelatedStep is null)
        {
            return
            [
                new CanvasWorkbenchChip
                {
                    Text = executorKind,
                    Tone = "neutral"
                }
            ];
        }

        return
        [
            new CanvasWorkbenchChip
            {
                Text = "Same role contract",
                Tone = "neutral"
            },
            new CanvasWorkbenchChip
            {
                Text = executorKind,
                Tone = "neutral"
            }
        ];
    }

    private static double ResolveDefinitionRoleNodeX(
        DefinitionRoleNodePlan plan,
        ProcessDefinitionEditorModel editor)
    {
        if (plan.RelatedStep is null)
        {
            return ResolveDefinitionRoleNodeX(plan.Role, editor);
        }

        return ResolveDefinitionStepX(plan.RelatedStep, editor.Steps) - DefinitionRoleInstanceOffsetX;
    }

    private static double ResolveDefinitionRoleNodeY(DefinitionRoleNodePlan plan)
    {
        if (plan.RelatedStep is null)
        {
            return ResolveDefinitionRoleNodeY(plan.Role, plan.RoleIndex);
        }

        return ResolveDefinitionStepY(plan.RelatedStep) + ResolveRoleInstanceOffsetY(
            plan.StepRoleIndex,
            plan.StepRoleCount);
    }

    private static double ResolveRoleInstanceOffsetY(int stepRoleIndex, int stepRoleCount)
    {
        if (stepRoleCount <= 1)
        {
            return 0d;
        }

        var row = (stepRoleIndex / 2) + 1;
        var direction = stepRoleIndex % 2 == 0
            ? -1d
            : 1d;
        return direction * row * DefinitionRoleInstanceRowGap;
    }

    private static double ResolveDefinitionArtifactNodeX(DefinitionArtifactNodePlan plan)
    {
        if (plan.Draft is not null)
        {
            return plan.Draft.X;
        }

        var anchorStep = plan.TargetStep ?? plan.OwnerStep;
        var anchorStepIndex = plan.TargetStep is null
            ? plan.OwnerStepIndex
            : plan.TargetStepIndex;
        var anchorX = ResolveDefinitionArtifactStepX(anchorStep, anchorStepIndex);
        return plan.IsClone
            ? anchorX - DefinitionArtifactCloneOffsetX
            : anchorX + DefinitionArtifactNodeOffsetX;
    }

    private static double ResolveDefinitionArtifactNodeY(DefinitionArtifactNodePlan plan)
    {
        if (plan.Draft is not null)
        {
            return plan.Draft.Y;
        }

        if (!plan.IsClone)
        {
            return ResolveDefinitionStepY(plan.OwnerStep) + ResolveArtifactStackOffsetY(plan.ArtifactIndex, plan.SourceArtifactCount);
        }

        var targetStep = plan.TargetStep ?? plan.OwnerStep;
        var roleRowsReserved = (plan.TargetRoleInstanceCount + 1) / 2;
        var baseOffset = Math.Max(1, roleRowsReserved + 1) * DefinitionArtifactNodeRowGap;
        return ResolveDefinitionStepY(targetStep) + baseOffset + (plan.InputIndex * DefinitionArtifactNodeRowGap);
    }

    private static double ResolveArtifactStackOffsetY(int index, int count)
    {
        if (count <= 1)
        {
            return DefinitionArtifactNodeRowGap;
        }

        return DefinitionArtifactNodeRowGap + (index * DefinitionArtifactNodeRowGap);
    }

    private static string ResolveDefinitionArtifactSubtitle(DefinitionArtifactNodePlan plan)
    {
        if (plan.IsDraft)
        {
            return "Manual artifact clone";
        }

        if (plan.TargetStep is not null)
        {
            return $"Input for {ResolveStepTitle(plan.TargetStep)}";
        }

        return $"Produced by {ResolveStepTitle(plan.OwnerStep)}";
    }

    private static string ResolveDefinitionArtifactLeadText(DefinitionArtifactNodePlan plan)
    {
        if (plan.IsClone)
        {
            return $"Represents the same artifact produced by {ResolveStepTitle(plan.OwnerStep)} without drawing a long cross-canvas artifact line.";
        }

        if (!string.IsNullOrWhiteSpace(plan.Artifact.ValidationRequirementSummary))
        {
            return plan.Artifact.ValidationRequirementSummary;
        }

        return plan.Artifact.AllowedFutureUsageSummary;
    }

    private static List<CanvasWorkbenchChip> BuildDefinitionArtifactNodeChips(DefinitionArtifactNodePlan plan)
    {
        var chips = new List<CanvasWorkbenchChip>
        {
            new()
            {
                Text = plan.Artifact.ArtifactKind.ToString(),
                Tone = "accent"
            },
            new()
            {
                Text = plan.Artifact.IsRequired ? "Required" : "Optional",
                Tone = plan.Artifact.IsRequired ? "info" : "neutral"
            }
        };

        if (plan.IsClone)
        {
            chips.Add(new CanvasWorkbenchChip
            {
                Text = plan.IsDraft ? "Draft clone" : "Input clone",
                Tone = "neutral"
            });
        }

        return chips;
    }

    private static List<CanvasWorkbenchChip> BuildDefinitionArtifactFooterChips(DefinitionArtifactNodePlan plan)
    {
        if (!plan.IsClone)
        {
            return
            [
                new CanvasWorkbenchChip
                {
                    Text = ResolveStepTitle(plan.OwnerStep),
                    Tone = "neutral"
                }
            ];
        }

        return
        [
            new CanvasWorkbenchChip
            {
                Text = "Same artifact",
                Tone = "accent"
            },
            new CanvasWorkbenchChip
            {
                Text = ResolveStepTitle(plan.OwnerStep),
                Tone = "neutral"
            }
        ];
    }

    private static string ResolveArtifactTitle(ProcessArtifactExpectationEditorModel artifact)
        => string.IsNullOrWhiteSpace(artifact.Title)
            ? "Untitled artifact"
            : artifact.Title;

    private static double ResolveDefinitionArtifactStepX(ProcessStepEditorModel step, int stepIndex)
    {
        return step.CanvasX != 0
            ? step.CanvasX
            : 140 + (Math.Max(0, stepIndex) * 280);
    }

    private static string ResolveStepTitle(ProcessStepEditorModel step)
        => string.IsNullOrWhiteSpace(step.Title)
            ? "Untitled step"
            : step.Title;

    private static CanvasWorkbenchNode BuildRunNode(ProcessStepRunViewModel stepRun)
    {
        var tone = ResolveRunTone(stepRun.Status);
        var profile = ProcessCanvasCatalog.GetStepKindProfile(stepRun.StepKind);
        return new CanvasWorkbenchNode
        {
            Id = BuildRunNodeId(stepRun.Id),
            Kind = ProcessCanvasCatalog.NodeKinds.RuntimeStep,
            Family = stepRun.StepKind == ProcessStepKind.Subprocess ? "process-subprocess-runtime" : "process-runtime",
            Icon = ResolveStepIcon(stepRun.StepKind),
            Title = stepRun.Title,
            Subtitle = ResolveRunSubtitle(stepRun),
            LeadText = ResolveRunLeadText(stepRun),
            Status = stepRun.Status.ToString().ToLowerInvariant(),
            StatusPill = stepRun.Status.ToString(),
            PaletteKey = tone,
            AccentColor = ResolveRunAccentColor(stepRun.Status),
            DurationLabel = stepRun.TouchMinutes > 0
                ? $"{stepRun.TouchMinutes} m touch"
                : $"{stepRun.WaitMinutes} m wait",
            X = 140 + ((stepRun.Sequence - 1) * 280),
            Y = stepRun.Status == ProcessStepRunStatus.Blocked ? 260 : 180,
            Chips = BuildRunNodeChips(stepRun),
            FooterChips =
            [
                new CanvasWorkbenchChip
                {
                    Text = stepRun.CapabilityGapSeverity.ToString(),
                    Tone = stepRun.CapabilityGapSeverity == ProcessCapabilityGapSeverity.None ? "neutral" : "danger"
                }
            ],
            ContextActions = BuildRuntimeStepContextActions(stepRun),
            InputPorts = DecorateProcessPorts(BuildRunStepInputPorts(stepRun, profile)),
            OutputPorts = DecorateProcessPorts(BuildRunStepOutputPorts(stepRun, profile))
        };
    }

    private readonly record struct DefinitionRoleNodeKey(Guid RoleId, Guid? StepId);

    private readonly record struct DefinitionRoleParticipationKey(Guid RoleId, Guid StepId);

    private sealed record DefinitionArtifactOwner(
        ProcessStepEditorModel Step,
        ProcessArtifactExpectationEditorModel Artifact,
        int ArtifactIndex)
    {
        public int StepIndex { get; init; }
    }

    private sealed record DefinitionRoleParticipation(
        ProcessRoleEditorModel Role,
        ProcessStepEditorModel Step,
        IReadOnlyList<ProcessResponsibilityKind> Responsibilities,
        bool IsDecisionAuthority,
        int StepIndex,
        int StepRoleIndex,
        int StepRoleCount);

    private sealed class DefinitionRoleParticipationBuilder(
        ProcessRoleEditorModel role,
        ProcessStepEditorModel step,
        int stepIndex,
        int roleIndex)
    {
        public ProcessRoleEditorModel Role { get; } = role;

        public ProcessStepEditorModel Step { get; } = step;

        public int StepIndex { get; } = stepIndex;

        public int RoleIndex { get; } = roleIndex;

        public List<ProcessResponsibilityKind> Responsibilities { get; } = [];

        public bool IsDecisionAuthority { get; set; }
    }

    private sealed record DefinitionRoleNodePlan(
        string NodeId,
        ProcessRoleEditorModel Role,
        ProcessStepEditorModel? RelatedStep,
        int RoleIndex,
        IReadOnlyList<ProcessResponsibilityKind> Responsibilities,
        bool IsDecisionAuthority,
        int StepRoleIndex,
        int StepRoleCount)
    {
        public static DefinitionRoleNodePlan CreateContract(
            ProcessRoleEditorModel role,
            int roleIndex)
        {
            return new DefinitionRoleNodePlan(
                ProcessCanvasBranching.BuildDefinitionRoleNodeId(role),
                role,
                null,
                roleIndex,
                [],
                false,
                0,
                1);
        }

        public static DefinitionRoleNodePlan CreateInstance(
            ProcessRoleEditorModel role,
            int roleIndex,
            DefinitionRoleParticipation participation)
        {
            return new DefinitionRoleNodePlan(
                ProcessCanvasBranching.BuildDefinitionRoleInstanceNodeId(role, participation.Step),
                role,
                participation.Step,
                roleIndex,
                participation.Responsibilities,
                participation.IsDecisionAuthority,
                participation.StepRoleIndex,
                participation.StepRoleCount);
        }
    }

    private sealed record DefinitionArtifactNodePlan(
        string NodeId,
        ProcessStepEditorModel OwnerStep,
        ProcessArtifactExpectationEditorModel Artifact,
        ProcessStepEditorModel? TargetStep,
        ProcessStepArtifactInputEditorModel? ArtifactInput,
        ProcessCanvasArtifactCloneDraft? Draft,
        int ArtifactIndex,
        int OwnerStepIndex,
        int TargetStepIndex,
        int SourceArtifactCount,
        int InputIndex,
        int TargetRoleInstanceCount)
    {
        public bool IsClone => TargetStep is not null || Draft is not null;

        public bool IsDraft => Draft is not null;

        public static DefinitionArtifactNodePlan CreateSource(DefinitionArtifactOwner owner)
        {
            return new DefinitionArtifactNodePlan(
                ProcessCanvasBranching.BuildDefinitionArtifactNodeId(owner.Artifact),
                owner.Step,
                owner.Artifact,
                null,
                null,
                null,
                owner.ArtifactIndex,
                owner.StepIndex,
                0,
                owner.Step.ArtifactExpectations.Count(HasStableArtifactIdentity),
                0,
                0);
        }

        public static DefinitionArtifactNodePlan CreateInputClone(
            DefinitionArtifactOwner owner,
            ProcessStepEditorModel targetStep,
            ProcessStepArtifactInputEditorModel artifactInput,
            int targetStepIndex,
            int inputIndex,
            int targetRoleInstanceCount)
        {
            return new DefinitionArtifactNodePlan(
                ProcessCanvasBranching.BuildDefinitionArtifactCloneNodeId(owner.Artifact, artifactInput, targetStep),
                owner.Step,
                owner.Artifact,
                targetStep,
                artifactInput,
                null,
                owner.ArtifactIndex,
                owner.StepIndex,
                targetStepIndex,
                owner.Step.ArtifactExpectations.Count(HasStableArtifactIdentity),
                inputIndex,
                targetRoleInstanceCount);
        }

        public static DefinitionArtifactNodePlan CreateDraftClone(
            DefinitionArtifactOwner owner,
            ProcessCanvasArtifactCloneDraft draft)
        {
            return new DefinitionArtifactNodePlan(
                draft.NodeId,
                owner.Step,
                owner.Artifact,
                null,
                null,
                draft,
                owner.ArtifactIndex,
                owner.StepIndex,
                0,
                owner.Step.ArtifactExpectations.Count(HasStableArtifactIdentity),
                0,
                0);
        }
    }

    private static bool HasStableArtifactIdentity(ProcessArtifactExpectationEditorModel artifact)
        => artifact.Id is { } artifactId && artifactId != Guid.Empty;

}
