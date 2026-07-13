# C# Boundary Map

## Target Projects And Ownership

| Project | Target ownership | Must not own |
| --- | --- | --- |
| `CanDoItAll.Processes.Contracts` | Stable public record shapes when needed across application/runtime/module boundaries. | Runtime behavior, AgentFramework types, provider SDKs. |
| `CanDoItAll.Processes.Abstractions` | Process-level interfaces/options consumed across runtime/application. | Concrete infrastructure or template file loading. |
| `CanDoItAll.Processes.Drivers.Abstractions` | Strategy execution request/result records, artifact descriptors, process adapter contracts. | Module-specific file I/O or project-structure provider details. |
| `CanDoItAll.Processes.Runtime` | Domain-neutral process state transitions, finalization, applied-result ledger, produced-slot lifecycle. | Project-structure tool composition, AgentFramework execution calls, .NET-specific template rules. |
| `CanDoItAll.Processes.Application` | Launch enrichment, dispatch/rework orchestration, projection packet composition. | Low-level AgentFramework persistence or file-system managed artifact I/O. |
| `CanDoItAll.Processes.Templates` | Template document records, compatibility loading, typed contract validation, summaries. | Runtime state mutation or subprocess launch. |
| `CanDoItAll.Modules.Processes` | AgentFramework process adapter, observation reader, module-level bridge wiring, managed artifact I/O integration. | Generic runtime state semantics. |
| `CanDoItAll.Modules.Workbench` | Project-structure runtime tool provider and process-scoped tool authorization. | Process template branch semantics. |
| `CanDoItAll.AgentFramework.Core/Models` | Execution run query/persistence, result summary storage, tool capability catalog, agent readiness models. | Process-template-specific bridge rules. |

## New Top-Level Types To Plan

| Type | Likely project | Responsibility |
| --- | --- | --- |
| `ProcessExecutionObservationSelector` or `ProcessExecutionObservationStepSelector` | `Processes.Projections` or `Processes.Contracts` | Strongly typed run/step selector for observation queries. |
| `ProcessBlockedStepPacket` | `Processes.Application` or `Processes.Projections` | Projection-safe structured blocked step data for operator action and rework. |
| `IProcessBlockedStepPacketBuilder` | `Processes.Application` | Build packets from step state, assignment, receipt, optional observation, and descriptor data. |
| `ProcessResultSummaryProjection` | `Processes.Drivers.Abstractions` or `AgentFramework.Models` | Compact JSON shape persisted for process-bound AgentFramework runs. |
| `SubprocessContractDocument` | `Processes.Templates` | Typed template metadata loaded from definition JSON. |
| `SubprocessContract` | `Processes.Contracts` or `Processes.Abstractions` | Runtime-consumable typed contract after template validation. |
| `ISubprocessContractResolver` | `Processes.Application` or `Processes.Templates` | Resolve validated typed contract from template/assignment. |
| `IParentSubprocessArtifactBridge` | `Processes.Drivers.Abstractions` or module integration boundary | Bridge request/result contract for runtime-owned subprocess handling. |
| `ParentSubprocessArtifactBridge` | `Modules.Processes` | Infrastructure-aware implementation that inspects child run/artifacts and writes parent managed evidence. |
| `ProcessArtifactSlotDescriptor` | `Processes.Drivers.Abstractions` | Semantic artifact descriptor rendered into prompt/diagnostics. |
| `IProcessArtifactDescriptorResolver` | `Processes.Application` or `Processes.Runtime` depending on inputs | Resolve expectation key/title/ref/gates for assignments. |
| `IManagedArtifactMaterializer` | `Modules.Processes` | Read/write materialized managed artifacts and compute content hashes. |
| `IProcessRuntimeToolPreflightService` | `Processes.Application` abstraction with module implementation | Check exact composed runtime tool availability before dispatch. |

## Old Responsibilities To Remove Or Delegate

- `ProcessRuntimeProjectionQueryService` should delegate blocked packet assembly instead of growing more diagnostic conditionals.
- `AgentFrameworkProcessExecutionAdapter.SubprocessState` should stop treating all child step artifacts as accepted evidence.
- `AgentFrameworkProcessExecutionAdapter.ResultConversion` should stop synthesizing produced artifact identity from raw output hash alone.
- `ProcessRuntimeEngine.ResultHelpers.BuildArtifactLedgerEvents` should stop reading original command result.
- Template markdown should stop being the only source for accepted repair handoff, no-go, manual skip, and required receipt gates.

## Temporary Bridges

- Legacy `SubprocessChildStepKey` and `SubprocessChildArtifactTitle` fields may remain as compatibility inputs during migration.
- Agent-owned subprocess launch may remain as a documented fallback only when no typed runtime-owned contract exists or a template explicitly opts into `AgentOwned`.
- Any temporary compatibility bridge must have SB08 or SB09 proof showing the new typed contract path is used for current controlled templates.

## Partial Class Policy

No new partial class may be the final architecture boundary for this work. A partial file is allowed only as a small adapter entry point that delegates immediately to a focused top-level service and includes a removal or shrink proof. Critical closure must include source assertion that core behavior lives outside the old large partial cluster.
