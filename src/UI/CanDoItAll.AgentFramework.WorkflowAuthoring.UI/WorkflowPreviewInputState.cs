using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

public sealed record WorkflowPreviewProjectRequirement(
    WorkflowNodeId NodeId,
    string NodeName,
    WorkflowProjectStructureOperation Operation,
    bool IsWriteOperation);

public sealed record WorkflowPreviewSimulationRequirement(
    WorkflowNodeId NodeId,
    string NodeName,
    WorkflowExecutorId SourceExecutorId,
    string Description,
    string OutputTemplateJson);

public sealed record WorkflowPreviewRequirements(
    IReadOnlyList<WorkflowPreviewProjectRequirement> ProjectRequirements,
    IReadOnlyList<WorkflowPreviewSimulationRequirement> SimulationRequirements)
{
    public static WorkflowPreviewRequirements Empty { get; } = new([], []);

    public bool NeedsProjectContext => ProjectRequirements.Any(requirement =>
        requirement.Operation != WorkflowProjectStructureOperation.ListProjects);

    public bool HasProjectStructureWrites => ProjectRequirements.Any(requirement => requirement.IsWriteOperation);

    public bool NeedsPreviewDialog => NeedsProjectContext || SimulationRequirements.Count > 0;
}

public sealed class WorkflowPreviewInputState
{
    public string InputJson { get; set; } = WorkflowPreviewInputSupport.DefaultInputJson;

    public string ProjectId { get; set; } = string.Empty;

    public string ParentNodeId { get; set; } = string.Empty;

    public HashSet<string> SimulatedNodeIds { get; } = new(StringComparer.Ordinal);

    public WorkflowPreviewRequirements Requirements { get; set; } = WorkflowPreviewRequirements.Empty;

    public string ProjectLoadError { get; set; } = string.Empty;
}
