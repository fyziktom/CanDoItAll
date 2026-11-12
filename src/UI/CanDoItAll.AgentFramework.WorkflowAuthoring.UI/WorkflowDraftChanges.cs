using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

internal enum WorkflowDraftField {
    Name, Description, Status, RuntimePolicy, StartNodeId, Nodes, Edges,
    Id, Kind, ComponentId, ProviderProfileId, Model, AgentId, SubworkflowId,
    ExternalRequestKind, ExecutorId, ExecutorSettings, ExecutionPolicy,
    Instructions, InputShape, ResultShape, CanvasX, CanvasY,
    SourceNodeId, TargetNodeId, SourcePortId, TargetPortId, ConditionExpression, Routing
}

internal sealed class WorkflowDraftChanges {
    private readonly Dictionary<WorkflowDraftField, long> fields = [];
    public long Revision { get; private set; }
    public event Action? Changed;

    public void Set<T>(WorkflowDraftField property, ref T storage, T value) {
        if (EqualityComparer<T>.Default.Equals(storage, value)) {
            return;
        }
        storage = value;
        Touch(property);
    }

    public void Touch(WorkflowDraftField property) {
        Revision = checked(Revision + 1);
        fields[property] = Revision;
        Changed?.Invoke();
    }

    public IReadOnlyDictionary<WorkflowDraftField, long> Capture() => new Dictionary<WorkflowDraftField, long>(fields);

    public bool IsUnchanged(WorkflowDraftField property, IReadOnlyDictionary<WorkflowDraftField, long> submitted)
        => fields.GetValueOrDefault(property) == submitted.GetValueOrDefault(property);

    public T Reconcile<T>(WorkflowDraftField property, IReadOnlyDictionary<WorkflowDraftField, long> submittedFields,
        T current, T submitted, T accepted)
        => IsUnchanged(property, submittedFields) && EqualityComparer<T>.Default.Equals(current, submitted) ? accepted : current;
}

internal sealed record WorkflowNodeSubmission(WorkflowCanvasNodeDraft Occurrence, WorkflowNode Definition,
    IReadOnlyDictionary<WorkflowDraftField, long> Fields);

internal sealed record WorkflowEdgeSubmission(WorkflowCanvasEdgeDraft Occurrence, WorkflowEdge Definition,
    IReadOnlyDictionary<WorkflowDraftField, long> Fields);

internal sealed record WorkflowCanvasSubmission(WorkflowDefinition Definition, long Revision,
    IReadOnlyDictionary<WorkflowDraftField, long> Fields,
    IReadOnlyDictionary<WorkflowNodeId, WorkflowNodeSubmission> Nodes,
    IReadOnlyDictionary<WorkflowEdgeId, WorkflowEdgeSubmission> Edges) {
    public static WorkflowCanvasSubmission Capture(WorkflowCanvasDocument document) {
        var definition = WorkflowCanvasDefinitionMapper.ToDefinition(document);
        var nodes = document.Nodes.Zip(definition.Graph.Nodes,
            (draft, value) => new WorkflowNodeSubmission(draft, value, draft.Changes.Capture()))
            .ToDictionary(item => item.Definition.Id);
        var edges = document.Edges.Zip(definition.Graph.Edges,
            (draft, value) => new WorkflowEdgeSubmission(draft, value, draft.Changes.Capture()))
            .ToDictionary(item => item.Definition.Id);
        return new(definition, document.Changes.Revision, document.Changes.Capture(), nodes, edges);
    }
}
