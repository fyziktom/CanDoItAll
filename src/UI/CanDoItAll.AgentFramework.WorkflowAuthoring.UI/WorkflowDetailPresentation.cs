using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.UI;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

public sealed record WorkflowTemplatePreviewHeader(string Name, string Description);

public sealed record WorkflowEventDetailPresentation(WorkflowEventView Event, WorkflowRunId RunId,
    WorkflowNodeId? NodeId, string Summary, string Payload, string TechnicalDetail);

public sealed record WorkflowArtifactDetailPresentation(WorkflowArtifactView Artifact,
    WorkflowNodeId? NodeId, string? RelativePath);

public sealed record WorkflowRunDetailPresentation(WorkflowRunSnapshot Run, string Summary, string Result,
    IReadOnlyList<WorkflowEventDetailPresentation> Events, IReadOnlyList<WorkflowArtifactDetailPresentation> Artifacts);
