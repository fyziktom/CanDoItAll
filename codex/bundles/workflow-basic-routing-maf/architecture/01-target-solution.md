# Target Solution

## Architecture Summary

The current phase adds a built-in, safe route description to each workflow edge and compiles that route description into Microsoft Agent Framework workflow routing primitives. The workflow model remains the source of truth; the canvas becomes an authoring surface for that model; the MAF compiler becomes the runtime bridge.

## Routing Contract

Recommended model additions:

```csharp
public enum WorkflowRouteKind
{
    Always,
    Predicate,
    SwitchCase,
    SwitchDefault,
    FanOutSelector
}

public enum WorkflowRouteOperator
{
    Exists,
    DoesNotExist,
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    EndsWith,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    IsTruthy,
    IsFalsy
}

public enum WorkflowRouteValueKind
{
    String,
    Number,
    Boolean,
    Null,
    Json
}

public sealed record WorkflowEdgeRouting(
    WorkflowRouteKind Kind,
    string Label,
    string JsonPath,
    WorkflowRouteOperator Operator,
    string ExpectedValueJson,
    WorkflowRouteValueKind ExpectedValueKind,
    bool CaseSensitive,
    int? FanOutTargetIndex,
    string RoutingLanguage)
{
    public static WorkflowEdgeRouting Always { get; } = new(
        WorkflowRouteKind.Always,
        string.Empty,
        string.Empty,
        WorkflowRouteOperator.Exists,
        string.Empty,
        WorkflowRouteValueKind.Json,
        CaseSensitive: false,
        FanOutTargetIndex: null,
        RoutingLanguage: WorkflowRoutingLanguages.BuiltInJsonV1);
}

public static class WorkflowRoutingLanguages
{
    public const string BuiltInJsonV1 = "built-in-json-v1";
    public const string LegacyConditionExpression = "legacy-condition-expression";
    public const string ArtlV1 = "artl-v1";
}
```

Recommended `WorkflowEdge` evolution:

```csharp
public sealed record WorkflowEdge(
    WorkflowEdgeId Id,
    WorkflowNodeId SourceNodeId,
    WorkflowPortId? SourcePortId,
    WorkflowNodeId TargetNodeId,
    WorkflowPortId? TargetPortId,
    WorkflowEdgeKind Kind,
    string ConditionExpression)
{
    public WorkflowEdgeRouting Routing { get; init; } = WorkflowEdgeRouting.Always;
}
```

## Core Routing Compiler Seam

Add Core-level interfaces that are not MAF-specific:

```csharp
public interface IWorkflowRoutingCompiler
{
    WorkflowCompiledRoute CompilePredicate(WorkflowDefinition definition, WorkflowEdge edge);
    WorkflowCompiledFanOutRoute CompileFanOut(WorkflowDefinition definition, WorkflowNodeId sourceNodeId, IReadOnlyList<WorkflowEdge> fanOutEdges);
}

public sealed record WorkflowCompiledRoute(
    WorkflowEdgeId EdgeId,
    string Label,
    Func<WorkflowNodeInput?, bool> Predicate);

public sealed record WorkflowCompiledFanOutRoute(
    WorkflowNodeId SourceNodeId,
    IReadOnlyList<WorkflowEdgeId> OrderedEdgeIds,
    IReadOnlyList<WorkflowNodeId> OrderedTargetNodeIds,
    Func<WorkflowNodeInput?, int, IEnumerable<int>> TargetSelector);
```

The initial implementation should be `BuiltInJsonWorkflowRoutingCompiler`. ARTL later supplies an alternate compiler that implements the same interface or translates ARTL AST into `WorkflowEdgeRouting`.

## Built-In JSON Evaluator

- JSON path syntax: `$`, `$.property`, `$.nested.property`, and optional `[index]` support if implementation remains small and tested.
- Operators: existence, equality/inequality, string contains/prefix/suffix, numeric comparisons, truthiness.
- Expected values are stored as JSON strings and parsed with `System.Text.Json`.
- Missing path evaluates according to the operator: `DoesNotExist` returns true; comparison operators return false.
- Malformed JSON path, invalid expected JSON, unsupported type comparison, or invalid route kind fails validation/compile.

## MAF Compiler Mapping

- `Always` or direct route: `builder.AddEdge(source, target, label, idempotent: true)`.
- `Predicate`: `builder.AddEdge<WorkflowNodeInput>(source, target, compiled.Predicate, label, idempotent: true)`.
- `SwitchCase` and `SwitchDefault`: group outgoing switch edges by source and call `builder.AddSwitch(source, switchBuilder => ...)`.
- `FanOutSelector`: group outgoing fan-out edges by source, order by `FanOutTargetIndex` or stable edge order, and call `builder.AddFanOutEdge<WorkflowNodeInput>(source, targets, selector, label)`.
- `FanIn`: preserve current behavior unless the project already uses or needs MAF `AddFanInEdge`; this bundle does not require new aggregation semantics.

## UI Authoring Model

- Route mode: Direct, If predicate, Switch case, Switch default, Fan-out selector.
- Predicate fields: JSON path, operator, value kind, expected value, case sensitivity, label.
- Switch fields: switch path, case value, default toggle, label.
- Fan-out fields: target index/order, JSON path, operator, expected value, label.
- Edge list: source -> target, route mode badge, condition summary, label, remove/edit action.
- Canvas link projection: add `Label`, `Summary`, and `Tone` to `CanvasWorkbenchLink` if shared canvas rendering can consume them safely; otherwise use route summaries in edge rows and connector primitive preview first.

## Persistence And API

- Route metadata must be serialized with workflow definitions.
- No migration is required if workflow graph JSON is stored as a JSON blob and new properties default safely; add migrations only if relational columns are required.
- API DTOs should expose routing metadata in the workflow definition graph rather than a separate endpoint.
- Existing clients that ignore `Routing` continue to read `ConditionExpression` and edge kind.

## ARTL Handoff

- Do not implement ARTL now.
- Add `RoutingLanguage` and a compiler interface so ARTL can later become another route compiler.
- Keep built-in JSON route expressions stable as `built-in-json-v1`.
- The UI may later add an ARTL advanced editor tab, but this bundle should ship only the safe built-in route builder.
