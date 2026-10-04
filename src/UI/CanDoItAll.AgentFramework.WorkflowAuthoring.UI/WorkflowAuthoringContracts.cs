using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

public sealed record WorkflowSecretOption(Guid Id, string Name, string Kind);

public sealed record WorkflowCanvasNodeSelection(WorkflowId WorkflowId, WorkflowNodeId NodeId, string Name, WorkflowNodeKind Kind);

public enum WorkflowSaveRejection { Validation, Conflict, Retired }

public abstract record WorkflowDefinitionSaveOutcome {
    public sealed record Accepted(WorkflowDefinition Definition) : WorkflowDefinitionSaveOutcome;
    public sealed record Rejected(WorkflowSaveRejection Reason) : WorkflowDefinitionSaveOutcome;
    public sealed record Unknown : WorkflowDefinitionSaveOutcome;
}

public sealed record WorkflowDocumentOperations(
    Func<WorkflowDefinitionSaveRequest, CancellationToken, Task<WorkflowDefinitionSaveOutcome>> Save,
    Func<WorkflowDefinition, CancellationToken, Task<WorkflowValidationResult>> Validate);

public sealed record WorkflowPromptBindingRequest(PromptGallerySelection Selection,
    WorkflowProviderOption Provider, string Model, LlmCallComponent? Current);

public abstract record WorkflowPromptBindingOutcome {
    public sealed record Accepted(LlmCallComponent Component) : WorkflowPromptBindingOutcome;
    public sealed record Rejected(string Message) : WorkflowPromptBindingOutcome;
    public sealed record Unknown : WorkflowPromptBindingOutcome;
}

public sealed record WorkflowPromptPickerContext(string Text, string? Provider, string? Model,
    object TargetKey, bool Disabled, EventCallback<PromptGallerySelection> Selected);

public sealed record WorkflowExecutorSettingsContext(WorkflowExecutorDescriptor Descriptor,
    ConfigurationState State, EventCallback<ConfigurationState> StateChanged, string TestIdPrefix);

public sealed record WorkflowPreviewProject(Guid Id, string Name);

public sealed record WorkflowPreviewSubmission(WorkflowDefinition Definition, string InputJson,
    WorkflowPreviewSimulationPlan SimulationPlan);

public abstract record WorkflowPreviewOutcome {
    public sealed record Completed(WorkflowValidationResult Validation, WorkflowRunSnapshot? Run,
        bool Succeeded, bool DetailsComplete, string Summary) : WorkflowPreviewOutcome;
    public sealed record Rejected(string Message) : WorkflowPreviewOutcome;
    public sealed record Unknown(WorkflowRunId? ReservedRunId = null) : WorkflowPreviewOutcome;
}

public sealed record WorkflowPreviewOperations(
    Func<WorkflowDefinition, WorkflowPreviewRequirements> Analyze,
    Func<CancellationToken, Task<IReadOnlyList<WorkflowPreviewProject>>> Projects,
    Func<WorkflowPreviewSubmission, Func<WorkflowNodeId, Task>, CancellationToken, Task<WorkflowPreviewOutcome>> Run);

public sealed record WorkflowPolicyInput(WorkflowNodeInputField Field, string Value);
