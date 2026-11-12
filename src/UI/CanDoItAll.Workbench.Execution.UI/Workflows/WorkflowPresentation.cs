using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Workbench.Execution.UI.Workflows;

public enum WorkflowInputSourceKind { FilePath, FolderPath, SelectedNode, ManualJson }

public sealed record WorkflowInputSourceView(WorkflowInputSourceKind Kind, string Key, string Label, string Value);
public sealed record WorkflowInputView(bool IncludeParentSubtree, bool IncludeAssets, string ManualInputJson,
    IReadOnlyList<WorkflowInputSourceView> AdditionalSources);
public sealed record WorkflowPreviewRow(string Label, string Value);
public sealed record WorkflowPreviewSection(string Title, string Summary, IReadOnlyList<WorkflowPreviewRow> Rows);
public sealed record WorkflowInputPreviewView(IReadOnlyList<WorkflowPreviewSection> Sections);
public sealed record WorkflowChoice(WorkflowId WorkflowId, WorkflowVersionId VersionId, string DisplayName,
    string Description, WorkflowLifecycleStatus Status, WorkflowRuntimeBackendKind PreferredBackend,
    bool IsSelectable, string DisabledReason);

public sealed record WorkflowAddView(Guid OpeningId, string ParentNodeTitle, IReadOnlyList<WorkflowChoice> Options,
    WorkflowId? SelectedWorkflowId, WorkflowVersionId? SelectedVersionId, WorkflowInputView InputSettings,
    WorkflowInputPreviewView Preview, bool IsBusy, bool RequiresObservation, string Error) {
    public string Title => $"Add workflow for {ParentNodeTitle}";
    public string Copy => "Choose an active workflow and review the project, parent node, and optional sources that will be sent as input.";
    public string SubmitLabel => "Add workflow";
}

public sealed record WorkflowAddActions(Func<WorkflowId, Task> Select, Func<bool, Task> IncludeSubtree,
    Func<bool, Task> IncludeAssets, Func<string, Task> ManualInput, Func<WorkflowInputSourceKind, Task> SourceKind,
    Func<string, Task> SourceKey, Func<string, Task> SourceLabel, Func<string, Task> SourceValue,
    Func<Task> Submit, Func<Task> Close);

public sealed record WorkflowRunView(string Status, int CurrentStepIndex, int StepCount, string Message);
public sealed record WorkflowSimulationOption(WorkflowNodeId NodeId, string NodeName, string Description, bool IsEnabled);
public sealed record WorkflowBackendOption(WorkflowRuntimeBackendKind Backend, string Label, bool IsSelected);
public sealed record WorkflowSimulationChange(WorkflowNodeId NodeId, bool IsEnabled);
public sealed record WorkflowStartView(Guid OpeningId, string NodeTitle, WorkflowRunView? Status,
    IReadOnlyList<WorkflowSimulationOption> SimulationOptions, WorkflowRuntimeBackendKind PreferredBackend,
    WorkflowRuntimeBackendKind RequestedBackend, IReadOnlyList<WorkflowBackendOption> BackendOptions,
    string BackendWarning, bool IsBusy, bool RequiresObservation, string Error) {
    public string Title => $"Start {NodeTitle}";
    public string Copy => "Confirm the workflow start. The workflow definition owns its execution settings, so this starts directly without resource matching.";
    public string SubmitLabel => RequiresObservation ? "Observe original start" : "Start workflow";
}

public sealed record WorkflowStartActions(Func<WorkflowSimulationChange, Task> Simulation, Func<Task> Submit, Func<Task> Close);
