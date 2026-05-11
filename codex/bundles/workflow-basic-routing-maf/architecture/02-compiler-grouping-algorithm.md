# Compiler Grouping Algorithm

## Input

- `WorkflowDefinition definition`
- `IReadOnlyDictionary<WorkflowNodeId, ExecutorBinding> bindings`
- `IWorkflowRoutingCompiler routingCompiler`

## Edge Groups

1. Build `edgesBySource` from `definition.Graph.Edges`.
2. For each source group, split edges into fan-out selector edges, switch case/default edges, normal direct or predicate edges, and fan-in edges if future aggregation support is added.
3. Validate that an edge belongs to only one executable routing group.
4. Add each executable group once.

## Pseudocode

```csharp
foreach (var group in edgesBySource)
{
    var source = bindings[group.Key];
    var fanOutEdges = group.Where(IsFanOutSelector).OrderBy(ResolveFanOutOrder).ToArray();
    if (fanOutEdges.Length > 0)
    {
        var compiled = routingCompiler.CompileFanOut(definition, group.Key, fanOutEdges);
        builder.AddFanOutEdge<WorkflowNodeInput>(
            source,
            compiled.OrderedTargetNodeIds.Select(id => bindings[id]),
            compiled.TargetSelector,
            label: ResolveGroupLabel(fanOutEdges));
    }

    var switchEdges = group.Where(IsSwitchRoute).OrderBy(ResolveSwitchOrder).ToArray();
    if (switchEdges.Length > 0)
    {
        builder.AddSwitch(source, switchBuilder =>
        {
            foreach (var edge in switchEdges.Where(edge => edge.Routing.Kind == WorkflowRouteKind.SwitchCase))
            {
                var compiled = routingCompiler.CompilePredicate(definition, edge);
                switchBuilder.AddCase(compiled.Predicate, bindings[edge.TargetNodeId]);
            }

            var defaultEdge = switchEdges.SingleOrDefault(edge => edge.Routing.Kind == WorkflowRouteKind.SwitchDefault);
            if (defaultEdge is not null)
            {
                switchBuilder.WithDefault(bindings[defaultEdge.TargetNodeId]);
            }
        });
    }

    foreach (var edge in group.Where(IsNormalEdge))
    {
        if (edge.Routing.Kind == WorkflowRouteKind.Predicate || edge.Kind == WorkflowEdgeKind.Conditional)
        {
            var compiled = routingCompiler.CompilePredicate(definition, edge);
            builder.AddEdge<WorkflowNodeInput>(source, bindings[edge.TargetNodeId], compiled.Predicate, compiled.Label, idempotent: true);
        }
        else
        {
            builder.AddEdge(source, bindings[edge.TargetNodeId], ResolveLabel(edge), idempotent: true);
        }
    }
}
```

## Validation Rules Required Before Compile

- A source node cannot mix switch routes and normal conditional routes unless explicitly documented as separate outgoing groups.
- A source node cannot have more than one switch default.
- Fan-out target indices must be unique when supplied.
- Fan-out selector output indices must be within the target count.
- Switch cases must have a predicate path/value unless they are default.
- Direct edges must not carry predicate-only required fields.

## Runtime Event Expectations

- A false predicate edge does not invoke the target executor.
- A switch group invokes the first matching case or the default target.
- A fan-out selector may invoke zero, one, or multiple targets depending on selected indices; zero-target behavior must be explicit and tested.
