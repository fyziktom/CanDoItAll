# C# Current State Inventory

## CodeAnalytics Evidence

- Snapshot id: `snap-20260708104406-98263759`
- Created UTC: `2026-07-08T10:44:06Z`
- Scoped source projects: 16
- Scoped documents: 427
- Dependency cycles: `[]`
- Dashboard caveats: class diagrams for large projects were truncated; unrelated `Microsoft.OpenApi` advisory warnings appeared in app/test/tool projects.

## Source Files Inspected

| File | Responsibility observed | Risk |
| --- | --- | --- |
| `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs` | Projection, operator actions, diagnostics, rework text, action composition. | Large class; adding blocked packet logic inline would worsen coupling. |
| `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorActionDiagnostics.cs` | Parses execution observation summaries. | Only observation-based; no runtime receipt fallback contract. |
| `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs` | Dispatch, manager recovery, rework instructions. | Generic rework message lacks typed blocked packet. |
| `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs` | Observation query/record contracts. | No step-level selector. |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs` | Reads AgentFramework runs for process projections. | Applies run-level `TakePerRun` before exact step matching. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | AgentFramework run listing and result summary persistence. | Has `ProcessStepId` filter but process reader does not use it; result summary is not guaranteed structured for process runs. |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs` | Mapped subprocess launch/defer/completion path. | Runtime and prompt both own subprocess launch. |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.SubprocessState.cs` | Child state lookup and synthesized completion. | Resolves child evidence from generic step artifacts, not accepted/no-go contract. |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` | Converts process step outcome to strategy result and artifacts. | Synthesizes produced artifacts with non-content-grounded ids/hashes. |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs` | Managed artifact read/write/validation helpers. | Existing logic can be reused but should not absorb all bridge responsibility. |
| `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs` | Applies strategy results. | Uses applied result but ledger call still passes original command. |
| `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs` | Finalization and ledger helpers. | Ledger helper reads `command.Result.ProducedArtifacts`. |
| `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs` | Template document model and loader. | Needs typed contract model and validation without growing into a validator monolith. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/AgentProcessReadinessEvaluator.cs` | Agent capability/readiness evaluator. | Checks metadata, not composed provider availability. |
| `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` | Runtime provider for project-structure tools including subprocess launch. | Actual availability depends on governed context and `ExecuteExternalAction`. |

## Large Classes And Partial Clusters

GPTPro and CodeAnalytics identify several high-risk large surfaces:

| Surface | Approximate issue | Bundle policy |
| --- | --- | --- |
| `ProcessRuntimeProjectionQueryService` | Projection plus diagnostics plus operator action composition. | Extract `IProcessBlockedStepPacketBuilder` or equivalent; do not add more inline diagnostic branches. |
| `AgentFrameworkProcessExecutionAdapter.*` | Adapter behavior split across partial files but still one responsibility-heavy type. | Bridge can be invoked from adapter, but bridge logic must live in focused service(s). |
| `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | Execution persistence and query/persistence logic. | Add focused result-summary projection/persistence helper if needed. |
| `ProcessTemplatePackLoader` | Template document loading, summaries, and compatibility. | Add typed document records and validator helpers without embedding all template policy in one method. |
| `AgentToolInvocationPolicy` | Large runtime policy surface. | Preflight should reuse existing tool catalog/registry, not grow policy class. |

## Constructor Dependency Counts

Preparation did not perform constructor-count extraction for every class. Implementation SB01 must capture exact constructor dependencies for the files it changes and add them to proof. This is a planned proof requirement, not an allowed omission.

## Direct Instantiation Points

Implementation must inventory direct instantiation or service registration points for new services:

- blocked packet builder
- structured result-summary projector/persister
- subprocess contract resolver/validator
- parent subprocess bridge
- artifact descriptor resolver/materializer
- runtime tool preflight service

## Current Tests

Known likely test homes:

- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- process/module tests discovered by implementation-time search

SB01 must locate exact current test coverage before behavior changes.

## Missing Tests

- exact step observation lookup with many runs under one process run
- runtime receipt fallback when AgentFramework observation is missing
- structured result summary persistence for process-bound runs
- accepted/repaired/no-go parent subprocess bridge states
- content-grounded produced artifact refs
- applied-result ledger after finalization downgrade
- exact composed runtime tool preflight
- typed subprocess template validation across all nine parent steps
- fake-proof resistance for child folder-only evidence
