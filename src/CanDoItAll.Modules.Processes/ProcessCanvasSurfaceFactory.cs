using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessCanvasSurfaceFactory
{
    public CanvasWorkbenchSurface BuildDefinitionSurface(ProcessDefinitionEditorModel editor)
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
                SelectedNodeIds = nodes.Count > 0 ? [nodes[0].Id] : [],
                ActiveInspectorTab = "definition"
            },
            Chrome = BuildDefinitionChrome()
        };
    }

    public CanvasWorkbenchSurface BuildRunSurface(
        ProcessRunListItem run,
        IReadOnlyList<ProcessStepRunViewModel> stepRuns)
    {
        ArgumentNullException.ThrowIfNull(run);

        var nodes = stepRuns
            .OrderBy(stepRun => stepRun.Sequence)
            .Select(BuildRunNode)
            .ToList();
        var links = stepRuns
            .OrderBy(stepRun => stepRun.Sequence)
            .Skip(1)
            .Select(stepRun => new CanvasWorkbenchLink
            {
                SourceId = $"run-step:{stepRun.Sequence - 1}",
                TargetId = $"run-step:{stepRun.Sequence}",
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
                SelectedNodeIds = nodes.FirstOrDefault(node => string.Equals(node.Status, "active", StringComparison.Ordinal)) is { } activeNode
                    ? [activeNode.Id]
                    : nodes.Count > 0
                        ? [nodes[0].Id]
                        : [],
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
                    ActionId = $"select-step:{BuildDefinitionNodeId(step)}",
                    Label = "Select step",
                    MenuLabel = "Select step",
                    Icon = "cursor",
                    Tone = "neutral"
                }
            ]
        };
    }

    private static CanvasWorkbenchNode BuildRunNode(ProcessStepRunViewModel stepRun)
    {
        var tone = ResolveRunTone(stepRun.Status);
        return new CanvasWorkbenchNode
        {
            Id = $"run-step:{stepRun.Sequence}",
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
                    ActionId = $"open-step-run:{stepRun.Id:D}",
                    Label = "Open step run",
                    MenuLabel = "Open step run",
                    Icon = "open",
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

    private static CanvasWorkbenchChrome BuildDefinitionChrome()
    {
        return new CanvasWorkbenchChrome
        {
            HintText = "Select a step to edit, drag nodes to improve the process map, and keep role bindings explicit.",
            EmptyStateKicker = "Process design",
            EmptyStateTitle = "Add or select a process step",
            EmptyStateDescription = "The canvas mirrors the current step sequence, role bindings, and contract shape.",
            QuickCreateActions =
            [
                new CanvasWorkbenchAction
                {
                    ActionId = "create-work-step",
                    Label = "Work step",
                    MenuLabel = "Add work step",
                    Description = "Add a standard typed work step.",
                    Icon = "plus",
                    Tone = "accent"
                },
                new CanvasWorkbenchAction
                {
                    ActionId = "create-approval-step",
                    Label = "Approval step",
                    MenuLabel = "Add approval step",
                    Description = "Add an explicit approval gate.",
                    Icon = "check",
                    Tone = "warn"
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
            ShowQuickCreateRail = false
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
}
