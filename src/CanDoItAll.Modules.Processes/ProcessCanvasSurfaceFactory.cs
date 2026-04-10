using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessCanvasSurfaceFactory
{
    public CanvasWorkbenchSurface BuildDefinitionSurface(ProcessDefinitionEditorModel editor, string? selectedNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var nodes = editor.Steps
            .Select((step, index) => BuildDefinitionNode(step, index))
            .ToList();
        var links = editor.Steps
            .Where(step => step.DependsOnStepId.HasValue)
            .Select(step => BuildLink(step))
            .Where(link => link is not null)
            .Cast<CanvasWorkbenchLink>()
            .ToList();

        return new CanvasWorkbenchSurface
        {
            SurfaceId = editor.Id?.ToString("N") ?? "process-definition-draft",
            Mode = "authoring",
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
        var nodes = orderedSteps
            .Select(BuildRunNode)
            .ToList();
        var runNodesByDefinitionId = orderedSteps.ToDictionary(stepRun => stepRun.StepDefinitionId);
        var links = orderedSteps
            .Where(stepRun => stepRun.DependsOnStepDefinitionId.HasValue &&
                runNodesByDefinitionId.ContainsKey(stepRun.DependsOnStepDefinitionId.Value))
            .Select(stepRun => new CanvasWorkbenchLink
            {
                SourceId = BuildRunNodeId(runNodesByDefinitionId[stepRun.DependsOnStepDefinitionId!.Value].Id),
                TargetId = BuildRunNodeId(stepRun.Id),
                Kind = "flow",
                IsUserAuthored = false
            })
            .ToList();

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

    private static CanvasWorkbenchNode BuildDefinitionNode(ProcessStepEditorModel step, int index)
    {
        var status = step.RequiresApproval
            ? "approval"
            : step.RequiresDecisionRecord
                ? "review"
                : "draft";
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
            Kind = "process-step",
            Family = "process-definition",
            Icon = ResolveStepIcon(step.StepKind),
            Title = step.Title,
            Subtitle = string.IsNullOrWhiteSpace(step.Subtitle) ? step.StepKind.ToString() : step.Subtitle,
            LeadText = step.OutputContractSummary,
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
            FooterChips =
            [
                new CanvasWorkbenchChip
                {
                    Text = status,
                    Tone = status switch
                    {
                        "approval" => "warn",
                        "review" => "accent",
                        _ => "neutral"
                    }
                }
            ],
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
            ]
        };
    }

    private static CanvasWorkbenchNode BuildRunNode(ProcessStepRunViewModel stepRun)
    {
        var tone = ResolveRunTone(stepRun.Status);
        return new CanvasWorkbenchNode
        {
            Id = BuildRunNodeId(stepRun.Id),
            Kind = "process-run-step",
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
            ]
        };
    }

    private static CanvasWorkbenchLink? BuildLink(ProcessStepEditorModel step)
    {
        if (!step.DependsOnStepId.HasValue)
        {
            return null;
        }

        return new CanvasWorkbenchLink
        {
            SourceId = BuildDefinitionNodeId(step.DependsOnStepId.Value),
            TargetId = BuildDefinitionNodeId(step),
            Kind = "flow",
            IsUserAuthored = true
        };
    }

    private static string BuildDefinitionNodeId(ProcessStepEditorModel step)
    {
        return BuildDefinitionNodeId(step.Id ?? Guid.Empty, step.Key, step.Title);
    }

    private static string BuildDefinitionNodeId(Guid stepId)
    {
        return $"step:{stepId:D}";
    }

    private static string BuildDefinitionNodeId(Guid stepId, string key, string title)
    {
        if (stepId != Guid.Empty)
        {
            return BuildDefinitionNodeId(stepId);
        }

        var normalized = string.IsNullOrWhiteSpace(key) ? title : key;
        normalized = normalized.Trim().ToLowerInvariant().Replace(' ', '-');
        return $"step:{normalized}";
    }

    private static string BuildRunNodeId(Guid stepRunId)
    {
        return $"run-step:{stepRunId:D}";
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
