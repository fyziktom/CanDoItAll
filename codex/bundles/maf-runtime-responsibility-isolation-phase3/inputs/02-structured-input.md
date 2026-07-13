# Structured Input

## Objective

Prepare a follow-up implementation bundle that repairs remaining MAF runtime architecture problems by isolating responsibilities around `MafAgentRuntime`, `MafRuntimeAgentFactory`, `RuntimeCapabilityComposer`, and `WorkspaceRuntimePlugin`.

## Must Solve

- `MafAgentRuntime` must stop owning turn execution details, finalizer repair, session serialization, approval continuation, usage diagnostics, and collaborator construction.
- `RuntimeCapabilityComposer` must stop being a partial-class cluster and stop owning unrelated capability responsibilities.
- `MafRuntimeAgentFactory` must be decomposed into explicit construction, instrumentation, script-policy, handoff, and finalizer-tool responsibilities.
- `WorkspaceRuntimePlugin` must be split into cohesive workspace tool families with shared access-policy services instead of one massive plugin type.
- Tests must be able to instantiate extracted owners directly with fakes.
- Architecture guards must prevent a future regression back to partial-class runtime growth.

## Must Not Solve

- Financial Strategist behavior.
- Quotation/PDF parsing or margin calculation.
- MarkItDown availability.
- Broad tool-catalog domain expansion.
- Full solution modernization unrelated to MAF runtime architecture.

## Architectural Assumption

Start with extraction inside `CanDoItAll.AgentFramework.Maf` because that preserves dependency direction and limits blast radius. Move contracts or implementations to a new project only when a subbundle proves distinct SDK/package lifecycle, reusable contracts, or cycle prevention requires it.
