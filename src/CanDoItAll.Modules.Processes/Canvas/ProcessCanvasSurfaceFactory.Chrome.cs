using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessCanvasSurfaceFactory
{
    private CanvasWorkbenchChrome BuildDefinitionChrome()
    {
        var chromeCatalog = chromeCatalogService.GetDefinitionChrome();
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
            QuickCreateActions = chromeCatalog.DefinitionQuickCreateActions.ToList(),
            GroupContextActions = chromeCatalog.DefinitionGroupContextActions.ToList()
        };
    }

    private static string ResolveDefinitionLeadText(ProcessStepEditorModel step, IReadOnlyList<string> dependencyOutcomeTitles)
    {
        if (step.StepKind == ProcessStepKind.Subprocess &&
            string.IsNullOrWhiteSpace(step.OutputContractSummary) &&
            !string.IsNullOrWhiteSpace(step.SubprocessDefinitionSnapshotName))
        {
            return $"Runs and observes {step.SubprocessDefinitionSnapshotName}.";
        }

        if (dependencyOutcomeTitles.Count == 0)
        {
            return step.OutputContractSummary;
        }

        var dependencyOutcomeSummary = dependencyOutcomeTitles.Count == 1
            ? dependencyOutcomeTitles[0]
            : string.Join(", ", dependencyOutcomeTitles);
        return string.IsNullOrWhiteSpace(step.OutputContractSummary)
            ? $"Continues when one of these paths is selected: {dependencyOutcomeSummary}."
            : $"{step.OutputContractSummary} Paths: {dependencyOutcomeSummary}.";
    }

    private static string ResolveDefinitionSubtitle(ProcessStepEditorModel step)
    {
        if (step.StepKind == ProcessStepKind.Subprocess &&
            !string.IsNullOrWhiteSpace(step.SubprocessDefinitionSnapshotName))
        {
            return $"Subprocess: {step.SubprocessDefinitionSnapshotName}";
        }

        return string.IsNullOrWhiteSpace(step.Subtitle) ? step.StepKind.ToString() : step.Subtitle;
    }

    private static List<CanvasWorkbenchChip> BuildDefinitionNodeChips(ProcessStepEditorModel step)
    {
        var chips = new List<CanvasWorkbenchChip>();
        if (step.StepKind == ProcessStepKind.Subprocess)
        {
            chips.Add(new CanvasWorkbenchChip
            {
                Text = step.SubprocessDefinitionId.HasValue ? "target linked" : "target missing",
                Tone = step.SubprocessDefinitionId.HasValue ? "info" : "danger"
            });
        }

        chips.Add(new CanvasWorkbenchChip
        {
            Text = $"{step.RoleAssignments.Count} roles",
            Tone = step.RoleAssignments.Count == 0 ? "danger" : "neutral"
        });
        chips.Add(new CanvasWorkbenchChip
        {
            Text = $"{step.ArtifactExpectations.Count} artifacts",
            Tone = step.ArtifactExpectations.Count == 0 ? "neutral" : "info"
        });
        chips.Add(new CanvasWorkbenchChip
        {
            Text = $"{step.BranchOutcomes.Count} branches",
            Tone = step.BranchOutcomes.Count == 0 ? "neutral" : "accent"
        });

        return chips;
    }

    private static List<CanvasWorkbenchAction> BuildDefinitionStepContextActions(ProcessStepEditorModel step)
    {
        var actions = new List<CanvasWorkbenchAction>();
        if (step.StepKind == ProcessStepKind.Subprocess && step.SubprocessDefinitionId.HasValue)
        {
            actions.Add(new CanvasWorkbenchAction
            {
                ActionId = ProcessCanvasActionIds.OpenSubprocessDefinition,
                Label = "Open subprocess",
                MenuLabel = "Open subprocess definition",
                Icon = "open_in_new",
                Tone = "info"
            });
        }

        actions.AddRange(
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
                ActionId = ProcessCanvasActionIds.AddSubprocessStep,
                Label = "Add subprocess",
                MenuLabel = "Add subprocess step",
                Icon = "account_tree",
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
        ]);

        return actions;
    }

    private static List<CanvasWorkbenchAction> BuildDefinitionArtifactContextActions()
    {
        return
        [
            new CanvasWorkbenchAction
            {
                ActionId = ProcessCanvasActionIds.CreateArtifactClone,
                Label = "Create clone",
                MenuLabel = "Create clone",
                Icon = "content_copy",
                Tone = "info"
            },
            new CanvasWorkbenchAction
            {
                ActionId = ProcessCanvasActionIds.HighlightArtifactClones,
                Label = "Highlight clones",
                MenuLabel = "Highlight all clones",
                Icon = "hub",
                Tone = "accent"
            }
        ];
    }

    private static List<CanvasWorkbenchAction> BuildDefinitionRoleContextActions()
    {
        return
        [
            new CanvasWorkbenchAction
            {
                ActionId = ProcessCanvasActionIds.EditDefinitionRole,
                Label = "Edit role",
                MenuLabel = "Edit role",
                Icon = "draw",
                Tone = "accent"
            },
            new CanvasWorkbenchAction
            {
                ActionId = ProcessCanvasActionIds.HighlightRoleClones,
                Label = "Highlight clones",
                MenuLabel = "Highlight all clones",
                Icon = "hub",
                Tone = "accent"
            }
        ];
    }

    private static List<CanvasWorkbenchChip> BuildDefinitionFooterChips(string status, IReadOnlyList<string> dependencyOutcomeTitles)
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

        foreach (var dependencyOutcomeTitle in dependencyOutcomeTitles.Where(title => !string.IsNullOrWhiteSpace(title)))
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
            ProcessStepKind.Subprocess => "account_tree",
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
            ProcessStepKind.Subprocess => "#0f766e",
            ProcessStepKind.End => "#059669",
            _ => "#475569"
        };
    }

    private static string ResolveRunSubtitle(ProcessStepRunViewModel stepRun)
    {
        if (stepRun.StepKind == ProcessStepKind.Subprocess)
        {
            return stepRun.SubprocessRun is null
                ? "Subprocess pending"
                : $"Subprocess run: {stepRun.SubprocessRun.Status}";
        }

        return string.IsNullOrWhiteSpace(stepRun.CurrentExecutorName)
            ? "Unassigned"
            : stepRun.CurrentExecutorName;
    }

    private static string ResolveRunLeadText(ProcessStepRunViewModel stepRun)
    {
        if (stepRun.SubprocessRun is not null)
        {
            return $"{stepRun.SubprocessRun.RunName}: {stepRun.SubprocessRun.CompletedStepCount} of {stepRun.SubprocessRun.TotalStepCount} steps complete.";
        }

        return !string.IsNullOrWhiteSpace(stepRun.BlockedReason)
            ? stepRun.BlockedReason
            : !string.IsNullOrWhiteSpace(stepRun.RefusalReason)
                ? stepRun.RefusalReason
                : !string.IsNullOrWhiteSpace(stepRun.SelectedBranchOutcomeTitle)
                    ? $"Selected branch: {stepRun.SelectedBranchOutcomeTitle}"
                    : stepRun.DecisionSummary;
    }

    private static List<CanvasWorkbenchChip> BuildRunNodeChips(ProcessStepRunViewModel stepRun)
    {
        var chips = new List<CanvasWorkbenchChip>();
        if (stepRun.SubprocessRun is not null)
        {
            chips.Add(new CanvasWorkbenchChip
            {
                Text = $"{stepRun.SubprocessRun.CompletedStepCount}/{stepRun.SubprocessRun.TotalStepCount} child steps",
                Tone = stepRun.SubprocessRun.BlockedStepCount > 0 ? "danger" : "info"
            });
        }

        chips.Add(new CanvasWorkbenchChip
        {
            Text = $"{stepRun.WaitMinutes} m wait",
            Tone = "neutral"
        });
        chips.Add(new CanvasWorkbenchChip
        {
            Text = $"{stepRun.ReworkCount} rework",
            Tone = stepRun.ReworkCount > 0 ? "warn" : "neutral"
        });

        return chips;
    }

    private static List<CanvasWorkbenchAction> BuildRuntimeStepContextActions(ProcessStepRunViewModel stepRun)
    {
        var actions = new List<CanvasWorkbenchAction>();
        if (stepRun.SubprocessRun is not null)
        {
            actions.Add(new CanvasWorkbenchAction
            {
                ActionId = ProcessCanvasActionIds.RuntimeOpenSubprocessRun,
                Label = "Open subprocess",
                MenuLabel = "Open subprocess run",
                Icon = "open_in_new",
                Tone = "info"
            });
        }

        actions.AddRange(
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
        ]);

        return actions;
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

        if (ProcessCanvasCatalog.DefinitionPorts.IsRoleMessagingInputPortId(port.Id) ||
            ProcessCanvasCatalog.DefinitionPorts.IsRoleMessagingOutputPortId(port.Id))
        {
            return ProcessCanvasCatalog.GetConnectionVisual(ProcessCanvasCatalog.PortFamily.RoleMessagingOutput);
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
            string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.ArtifactSourceInput, StringComparison.Ordinal) ||
            string.Equals(port.Id, ProcessCanvasCatalog.DefinitionPorts.ArtifactUsageOutput, StringComparison.Ordinal) ||
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
