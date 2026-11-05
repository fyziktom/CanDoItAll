using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

public sealed class WorkflowDefinitionReview {
    private readonly Dictionary<WorkflowGraph, string> graphSummaries = new(ReferenceEqualityComparer.Instance);
    public WorkflowDefinitionReview(WorkflowDefinition original, WorkflowDefinition local, WorkflowDefinition current,
        long revision, bool hasRawGraphInput) {
        if (original.Id != local.Id || original.Id != current.Id) {
            throw new ArgumentException("A workflow comparison requires the same document identity.");
        }
        Original = Freeze(original);
        Local = Freeze(local);
        Current = Freeze(current);
        Revision = revision;
        HasRawGraphInput = hasRawGraphInput;
        Name = new(original.Name, local.Name, current.Name, StringComparer.Ordinal);
        Description = TextMerge.Compare(original.Description, local.Description, current.Description).Field;
        Status = new(original.Status, local.Status, current.Status);
        RuntimePolicy = new(original.RuntimePolicy, local.RuntimePolicy, current.RuntimePolicy);
        Graph = new(Original.Graph, Local.Graph, Current.Graph, new GraphEquality(Original.Graph, Local.Graph, Current.Graph));
        foreach (var graph in new[] { Original.Graph, Local.Graph, Current.Graph }) {
            graphSummaries[graph] = $"{graph.Nodes.Count} nodes · {graph.Edges.Count} routes · start {graph.StartNodeId}\n" +
                string.Join("\n", graph.Nodes.Take(30).Select(node => $"{node.Name} · {node.Kind} · {node.Id}")) +
                (graph.Nodes.Count > 30 ? "\nAdditional nodes are included in the whole graph choice." : string.Empty);
        }
        if (hasRawGraphInput && Graph.CurrentChanged) {
            Graph.RequireChoice();
        }
    }

    public WorkflowDefinition Original { get; }
    public WorkflowDefinition Local { get; }
    public WorkflowDefinition Current { get; }
    public long Revision { get; }
    public bool HasRawGraphInput { get; }
    public MergeField<string> Name { get; }
    public MergeField<string> Description { get; }
    public MergeField<WorkflowLifecycleStatus> Status { get; }
    public MergeField<WorkflowRuntimePolicy> RuntimePolicy { get; }
    public MergeField<WorkflowGraph> Graph { get; }
    public bool IsResolved => Name.IsResolved && Description.IsResolved && Status.IsResolved && RuntimePolicy.IsResolved && Graph.IsResolved;
    public bool KeepsLocalGraph => Graph.IsResolved && ReferenceEquals(Graph.Result, Local.Graph);
    public string FormatGraph(WorkflowGraph graph) => graphSummaries[graph];

    public WorkflowDefinition Result() {
        if (!IsResolved) {
            throw new InvalidOperationException("Resolve the workflow comparison before applying it.");
        }
        return Current with {
            Name = Name.Result,
            Description = Description.Result,
            Status = Status.Result,
            RuntimePolicy = RuntimePolicy.Result,
            Graph = Graph.Result
        };
    }

    private static WorkflowDefinition Freeze(WorkflowDefinition definition) => definition with {
        Graph = definition.Graph with {
            Nodes = definition.Graph.Nodes.Select(node => node with { Ports = node.Ports.ToImmutableArray() }).ToImmutableArray(),
            Edges = definition.Graph.Edges.ToImmutableArray()
        },
        InputParameters = definition.InputParameters.ToImmutableArray()
    };

    private sealed class GraphEquality : IEqualityComparer<WorkflowGraph> {
        private readonly Dictionary<WorkflowGraph, string> snapshots = new(ReferenceEqualityComparer.Instance);

        public GraphEquality(params WorkflowGraph[] graphs) {
            foreach (var graph in graphs) {
                snapshots.TryAdd(graph, JsonSerializer.Serialize(graph));
            }
        }

        public bool Equals(WorkflowGraph? left, WorkflowGraph? right)
            => ReferenceEquals(left, right) || left is not null && right is not null &&
                StringComparer.Ordinal.Equals(snapshots[left], snapshots[right]);

        public int GetHashCode(WorkflowGraph graph) => StringComparer.Ordinal.GetHashCode(snapshots[graph]);
    }
}
