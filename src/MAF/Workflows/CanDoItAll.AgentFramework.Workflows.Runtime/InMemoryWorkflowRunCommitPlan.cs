using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed record InMemoryWorkflowRunCommitPlan(
    WorkflowRunSnapshot ExpectedRun,
    WorkflowExternalRequestRecord ExpectedRequest,
    WorkflowExternalRequestRecord RespondedRequest,
    WorkflowRunSnapshot UpdatedRun,
    WorkflowEventRecord? TransitionEvent,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowExternalRequestRecord> NextRequests,
    IReadOnlyList<WorkflowCheckpointRecord> Checkpoints,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts);
