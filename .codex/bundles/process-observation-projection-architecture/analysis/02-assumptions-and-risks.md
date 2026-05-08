# Assumptions And Risks

## Assumptions

- The first implementation target remains a single app instance. `IMemoryCache` can be used for local projection caching, but the design must not rely on local cache correctness.
- The UI remains Blazor-based and continues to use BaseLib/CanvasLib patterns.
- Existing process runtime write paths stay authoritative and generic.
- The first live-observation implementation can use throttled polling or a circuit-scoped observation state service before adding a dedicated SignalR hub.
- Future conversational dashboard control can be server-mediated through typed intents, not direct client-side prompt execution.

## Critical Path Risks

- Split source of truth: caching derived process state without a clear invalidation/staleness contract could make UI disagree with process core.
- Refresh amplification: a dashboard showing many active processes can trigger many run-list, detail, AgentFramework, outbox, and analytics queries if the service boundary is not windowed and coalesced.
- Cache stampede: many circuits or dialogs opening at the same time can recompute the same expensive snapshot unless per-key concurrency is controlled.
- Authorization leakage: project or user scoping omitted from cache keys can show one user's process observations to another context.
- UI rerender flood: large snapshots pushed into a single state object can rerender most of the page on every update.
- AI overreach: allowing an AI assistant to emit free-form UI commands or core actions would undermine the read-only observation contract.

## Validation Risks

- Existing tests may not cover high-volume live observation scenarios. Subbundle 06 must add scale-oriented integration tests before declaring success.
- Browser smoothness requires actual browser proof because query optimization alone does not prove Blazor rendering is cheap.
- Mock-agent tests must remain generic. They should prove process observation works for arbitrary definitions, not only one hand-crafted process.
- Simple .NET app build smoke tests must remain independent from CanDoItAll process definitions so validation proves generic agent/process behavior.

## Reopen Triggers

- The Processes UI starts showing stale state without visible freshness metadata.
- New observation services bypass existing process access/project scoping.
- A dashboard implementation reloads full run details for every active run.
- Cache keys accept unbounded user input directly.
- Any implementation subbundle changes process execution behavior rather than read-model/projection behavior.
- Future Blazor implementation introduces a single large notifying global state object for all process dashboard data.
