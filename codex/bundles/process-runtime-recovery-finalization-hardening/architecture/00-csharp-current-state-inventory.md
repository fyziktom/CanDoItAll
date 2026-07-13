# C# Current State Inventory

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260707213600-f58ac646`
- Solution: `repo://CanDoItAll.slnx`
- Scope: Processes Core, Builder, Runtime, Application, Persistence, Projections, Templates, Driver abstractions and standard drivers, `CanDoItAll.Modules.Processes`, unit tests, and integration tests.
- Dependency result: no project cycles reported for the scoped graph.
- Known snapshot limitations: integration-test factory DI registrations were partially interpreted; class diagrams truncated after 80 types for large projects; unrelated `Microsoft.OpenApi` vulnerability warnings appeared during workspace processing.

## High-Risk Responsibility Clusters

| Surface | Current responsibility pressure | Bundle action |
|---|---|---|
| `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs` and partials | Activation, claims, cancellation, result submission, artifact ledger, recovery decision, result helpers, and rework behavior are split across partial files. | SB01 maps behavior; SB05 extracts recovery routing; SB06 prevents new partial expansion. |
| `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs` | Dispatch loop, retry suppression, strategy invocation, manager/background orchestration, and persistence coordination are concentrated in one large service. | SB01 characterizes flow; SB05 moves retry taxonomy out of dispatch heuristics. |
| `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` | Launch, plan compilation, assignment creation, artifact expectations, and runtime persistence orchestration are concentrated. | SB02 adds connected artifact contracts and validation at launch/plan boundaries. |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter*.cs` | Agent execution, finalizer parsing, managed artifact materialization, grounding, child processes, completion issue conversion, and retry policy are partial-heavy. | SB04 and SB06 extract finalization and driver policy seams. |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs` | Evidence construction and process-context packaging are large and likely over-broad. | SB03 and SB07 introduce fresh contract retrieval and bounded packages. |

## Current Generic Contracts

- `repo://src/Processes/CanDoItAll.Processes.Core/ProcessArtifactModels.cs` already defines artifact slots, artifact references, requirement mode, scope, sensitivity, and artifact definitions.
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs` stores runtime state, step state, result receipts, produced artifacts, recovery decisions, and available artifact slots.
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs` stores assignment metadata, including required and produced slot ids.
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs` and `ProcessStrategyContracts.cs` define adapter/strategy result boundaries.

## Current Gap Summary

- The runtime knows that an artifact slot is available, but not enough concrete lineage to reliably say which artifact instance satisfies a downstream connected input.
- Step assignments persist slot ids, but not a durable step input package suitable for re-fetch after context compression.
- Retry classification is currently influenced by adapter diagnostic retry flags, which allows `NeedsManager` to become a same-step automatic retry.
- Prompt instructions carry too much of the finalization/handoff contract. They must become enforceable runtime and driver contracts.
- The partial-class split is organizational, not architectural isolation. Unit tests cannot target several responsibilities without constructing broad runtime/adapter state.
