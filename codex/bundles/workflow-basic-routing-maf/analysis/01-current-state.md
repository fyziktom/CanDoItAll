# Current State

## Domain Model

- `WorkflowEdgeKind` currently includes `Direct`, `Conditional`, `FanOut`, and `FanIn`.
- `WorkflowEdge` currently carries `ConditionExpression` as a string.
- There is no typed route contract for route kind, predicate path, operator, expected value, switch default, or fan-out target index.

## Compiler

- `MafWorkflowCompiler` builds executor bindings for every node and loops through all graph edges.
- The current edge loop calls the non-generic `AddEdge` overload with `ConditionExpression` as the third argument.
- Based on MAF API documentation, this string is label-like visualization metadata, while executable predicates require the generic `AddEdge<T>` overload with `Func<T?, bool>`.
- Therefore the current `ConditionExpression` field should be treated as legacy metadata, not as confirmed executable routing.

## Runtime Payload

- Workflow nodes currently execute through `WorkflowNodeInput`.
- LLM and executor nodes return new `WorkflowNodeInput(result.PayloadJson)`.
- Built-in route evaluation should therefore inspect `WorkflowNodeInput.PayloadJson`.

## Canvas

- `WorkflowCanvasEdgeDraft` has `Kind` and `ConditionExpression` only.
- `WorkflowCanvasDefinitionMapper.FromDefinition` and `ToDefinition` round-trip that legacy edge shape.
- `BuildSurface` projects links with `Kind = edge.Kind.ToString()` and no route label or summary.
- The current edge editor exposes a free-text condition field rather than a route builder.

## Persistence/API

- Workflow catalog, persistent stores, and API endpoints already exist.
- Route metadata must be verified through save/load and API round-trip once the model is extended.
