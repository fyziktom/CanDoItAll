# C# Current-State Inventory

## Projects And Responsibilities

| Project | Current responsibility | Change |
| --- | --- | --- |
| `CanDoItAll.Processes.Contracts` | External process commands/contracts | No run-record persistence dependency. |
| `CanDoItAll.Processes.Abstractions` | Runtime-facing abstractions | No projection dependency added. |
| `CanDoItAll.Processes.Runtime` | Canonical state machine and terminal rules | Preserve resumable escalation semantics. |
| `CanDoItAll.Processes.Projections` | Read models and store/query contracts | Own run-record value types and store/query interfaces. |
| `CanDoItAll.Processes.Application` | Process orchestration and read use cases | Own deterministic assembler/finalization/query orchestration. |
| `CanDoItAll.Processes.Persistence` | EF entities and stores | Own dedicated entity, configuration, and store implementation. |
| `CanDoItAll.Modules.Processes` | Agent Framework/provider integration and composition | Own execution-evidence adapter, manager selection, narrative generator/worker. |
| `CanDoItAll.Modules.Workbench` | Project-structure projection | Consume terminal run records. |
| `CanDoItAll.Web` | HTTP boundary and composition root | Map typed list/summary/analytics endpoints. |

## Hot Classes

- `ProcessRuntimeProjectionQueryService` is approximately 1,900 lines and already combines list/detail/workspace enrichment. It must not acquire run-record responsibilities.
- `ProcessWorkspaceShellProjectionService` aggregates graph/analytics data from raw history and usage.
- `ProcessRuntimeProjectionProjector` writes several generic snapshots per event.
- `EfProcessProjectionStore` saves each mutation independently.
- `AgentFrameworkProcessExecutionObservationReader` and `AgentFrameworkProcessRuntimeUsageTelemetryReader` sequentially enumerate and hydrate execution history.
- `ProjectStructureProcessProjectionContributor` reassembles terminal summary data from canonical stores.

## Canonical Versus Derived

- Canonical: runtime state, plans, assignments, runtime event envelopes, Agent Framework execution evidence.
- Derived: live/detail/history projections, dashboard aggregates, project-structure nodes, new compact run record.
- Agent catalog/pricing metadata is cached reference data, not copied as relational entities.

## Invariants

- Runtime owns state transitions.
- Existing `Escalated` state is not terminal and can resume.
- Projection records are rebuildable/replaceable derivations and must carry schema/completeness metadata.
- The hard record is the source of truth only for historical read-model consumers, never for runtime command decisions.
