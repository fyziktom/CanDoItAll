# C# Pattern Selection Matrix

Patterns are tools, not decorations. Select them only when the forces match the problem.

| Problem force | Preferred pattern | Use when | Avoid when |
|---|---|---|---|
| Complex object construction with validation | Builder | Process definitions, workflow definitions, runtime options, capability catalogs | The object has only a few required constructor parameters |
| Runtime chooses one implementation | Factory Method | Provider, tool, strategy, or driver selection | The caller already knows the exact type and no extension is expected |
| Runtime creates families of related implementations | Abstract Factory | Memory provider family, tool family, plugin family | There is only one product type |
| External SDK must not leak into core | Adapter | MCP clients, OpenAI/Azure/Ollama providers, memory APIs | The SDK type is already safely isolated in Infrastructure |
| Large class mixes abstraction and implementation hierarchy | Bridge | Runtime capability facade with multiple implementation families | A simple interface plus implementation is enough |
| Multiple algorithms or policies are interchangeable | Strategy | execution policy, retry policy, memory ranking, dispatch decision, provider routing | A single stable algorithm exists |
| Ordered handling pipeline | Chain of Responsibility | tool authorization, validators, middleware-like handlers | Handler order is not meaningful |
| Cross-cutting behavior around a service | Decorator | logging, telemetry, caching, retry, authorization envelope | The wrapper starts owning business behavior |
| User/tool action as data | Command | tool invocation, process step execution, queued work | The command becomes a god object with every dependency |
| Tree-like structure | Composite | process steps, sub-processes, workflow graphs | The model is linear and does not need tree operations |
| State-specific behavior | State | process run lifecycle, approval lifecycle, memory ingestion status | A simple enum plus switch is clearer and stable |
| Event notifications | Observer | runtime events, snapshots, projections | Consumers need request/response semantics |
| Simplified legacy entry point | Facade | keep UI/runtime callers stable while internals are extracted | The facade stores all real logic |
| Replace conditional by polymorphism | Strategy or State | switches grow as new providers/tools/drivers appear | The switch is tiny and closed by design |

## Rule for Codex

When applying a pattern, write a Pattern Selection Record:

- observed problem force
- rejected simpler option
- selected pattern
- new types/projects
- dependency direction
- unit-test seam
- migration plan
- proof that the old large class does not still own the behavior
