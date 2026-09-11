using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed record WorkflowStructureReadContext(WorkflowExecutionOccurrence Occurrence,
    WorkflowVersionId VersionId, WorkflowNodeId StepId) {
    public static WorkflowDisclosureOwnerId DisclosureOwner { get; } = new("workbench.structure");

    [JsonIgnore]
    public Action<WorkflowProviderReadEvidence>? CaptureReadEvidence { get; init; }
}
