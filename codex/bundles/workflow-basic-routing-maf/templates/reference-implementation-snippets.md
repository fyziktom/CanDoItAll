# Reference Implementation Snippets

These snippets are guidance only. Adapt naming and namespaces to the repo.

## Predicate Evaluation Shape

```csharp
private static bool Evaluate(WorkflowEdgeRouting routing, WorkflowNodeInput? input)
{
    if (routing.RoutingLanguage != WorkflowRoutingLanguages.BuiltInJsonV1)
    {
        throw new InvalidOperationException($"Unsupported routing language '{routing.RoutingLanguage}'.");
    }

    using var document = JsonDocument.Parse(input?.PayloadJson ?? "{}");
    var found = TryResolvePath(document.RootElement, routing.JsonPath, out var value);
    return EvaluateOperator(found, value, routing);
}
```

## Compiler Call Shape

```csharp
builder.AddEdge<WorkflowNodeInput>(
    source,
    target,
    compiled.Predicate,
    compiled.Label,
    idempotent: true);
```

## ARTL Handoff Shape

```csharp
public sealed class ArtlWorkflowRoutingCompiler : IWorkflowRoutingCompiler
{
    // Implement in the later ARTL bundle.
}
```
