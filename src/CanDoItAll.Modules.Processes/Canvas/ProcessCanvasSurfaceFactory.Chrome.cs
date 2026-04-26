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
