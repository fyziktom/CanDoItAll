using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Execution.UI.Workflows;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureWorkflowPresentation {
    internal static WorkflowAddView Add(ProjectStructureWorkflowAddDialogState state)
        => new(state.OpeningId, state.ParentNodeTitle,
            state.Options.Select(option => new WorkflowChoice(option.WorkflowId, option.VersionId, option.DisplayName,
                option.Description, option.Status, option.PreferredBackend, option.IsSelectable, option.DisabledReason)).ToArray(),
            state.SelectedWorkflowId, state.SelectedVersionId,
            new(state.InputSettings.IncludeParentSubtree, state.InputSettings.IncludeAssets, state.InputSettings.ManualInputJson,
                state.InputSettings.AdditionalSources.Select(source => new WorkflowInputSourceView(SourceKind(source.Kind), source.Key,
                    source.Label, source.Value)).ToArray()),
            new(state.Preview.Sections.Select(section => new WorkflowPreviewSection(section.Title, section.Summary,
                section.Rows.Select(row => new WorkflowPreviewRow(row.Label, row.Value)).ToArray())).ToArray()),
            state.IsBusy, state.RequiresObservation, state.Error);

    internal static WorkflowStartView Start(ProjectStructureWorkflowStartDialogState state)
        => new(state.OpeningId, state.NodeTitle,
            state.Status is { } status ? new(status.Status, status.CurrentStepIndex, status.StepCount, status.Message) : null,
            state.SimulationOptions.Select(option => new WorkflowSimulationOption(new(option.NodeId), option.NodeName, option.Description,
                state.SimulatedNodeIds.Contains(option.NodeId, StringComparer.OrdinalIgnoreCase))).ToArray(),
            state.PreferredBackend, state.RequestedBackend,
            state.BackendOptions.Select(option => new WorkflowBackendOption(option.Backend, option.Label, option.IsSelected)).ToArray(),
            state.BackendWarning,
            state.IsBusy, state.RequiresObservation, state.Error);

    private static WorkflowInputSourceKind SourceKind(ProjectStructureWorkflowInputSourceKind kind) => kind switch {
        ProjectStructureWorkflowInputSourceKind.FilePath => WorkflowInputSourceKind.FilePath,
        ProjectStructureWorkflowInputSourceKind.FolderPath => WorkflowInputSourceKind.FolderPath,
        ProjectStructureWorkflowInputSourceKind.SelectedNode => WorkflowInputSourceKind.SelectedNode,
        ProjectStructureWorkflowInputSourceKind.ManualJson => WorkflowInputSourceKind.ManualJson,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Workflow input source.")
    };

    internal static ProjectStructureWorkflowInputSourceKind SourceKind(WorkflowInputSourceKind kind) => kind switch {
        WorkflowInputSourceKind.FilePath => ProjectStructureWorkflowInputSourceKind.FilePath,
        WorkflowInputSourceKind.FolderPath => ProjectStructureWorkflowInputSourceKind.FolderPath,
        WorkflowInputSourceKind.SelectedNode => ProjectStructureWorkflowInputSourceKind.SelectedNode,
        WorkflowInputSourceKind.ManualJson => ProjectStructureWorkflowInputSourceKind.ManualJson,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Workflow input source.")
    };
}
