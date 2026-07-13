# Scope Inventory

## Bundle Scope

This bundle covers generic process runtime hardening around process-step edges:

- connected artifact availability and lineage;
- context-safe step contract retrieval;
- step finalization before advancement;
- manager-confirmed handoff when required;
- retry/recovery taxonomy;
- upstream repair routing;
- process driver isolation;
- bounded context packaging;
- regression and architecture proof.

The bundle does not implement code during preparation.

## Source Areas

| Area | Files | Why in scope |
|---|---|---|
| Runtime engine | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs`, `ProcessRuntimeEngine.ResultHelpers.cs`, `ProcessRuntimeEngine.Rework.cs`, `ProcessRuntimeEngine.Claims.cs`, `ProcessRuntimeEngine.CommitHelpers.cs` | Current state transitions, recovery decisions, retry classification, artifact ledger, and rework behavior. |
| Runtime scheduler/state | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs`, `ProcessRuntimeState.cs`, `ProcessRuntimeStepAssignments.cs`, `ProcessRuntimePorts.cs` | Readiness checks, available slots, step assignment metadata, runtime ports. |
| Launch and plan compilation | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`, `repo://src/Processes/CanDoItAll.Processes.Application/ProcessTemplateKernelBuilder.cs`, `repo://src/Processes/CanDoItAll.Processes.Builder/ProcessInstancePlan.cs`, `ProcessInstancePlanCompiler.Builders.cs` | Artifact connections must be preserved from template/plan into runtime state. |
| Dispatch and manager orchestration | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`, manager-control-loop files under Process Application/Module tests | Dispatch invokes strategy results and currently suppresses repeated automatic retry after the fact. |
| Core artifact model | `repo://src/Processes/CanDoItAll.Processes.Core/ProcessArtifactModels.cs`, `ProcessGraphKernel.cs` | Existing strongly typed artifact definitions and graph semantics. |
| Driver abstractions | `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`, `ProcessStrategyContracts.cs` | Extension points for domain-specific execution, finalization, and evidence policy. |
| AgentFramework module integration | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter*.cs`, `AgentFrameworkProcessStepBriefBuilder.cs`, `ProcessRequiredToolReceiptGate.cs`, `ProcessRuntimeEvidenceSourceProvider.cs` | Current adapter/finalizer/materialization/tool-receipt/context-packaging behavior. |
| Tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs`, `ProcessRuntimeDispatchApplicationServiceTests.cs`, `ProcessRuntimeIntegrationAdapterTests.cs`, `ProcessManagerControlLoopTests.cs` | Primary characterization and regression surface. |

## Existing Related Bundles

| Bundle | Relationship |
|---|---|
| `repo://codex/bundles/process-escalation-root-cause-architecture` | Source context for escalation issues; not sufficient for full runtime artifact/finalization hardening. |
| `repo://codex/bundles/process-runtime-dispatch-flexibility-hardening` | Prior dispatch hardening context; this bundle adds artifact lineage, finalization, and recovery router scope. |
| `repo://codex/bundles/process-tool-proof-readiness-refactor` | Prior proof/tool-readiness context; this bundle turns missing receipts and finalization into generic runtime gates. |
| `repo://codex/bundles/multiteam-development-escalation-repair` | Domain-specific regression context; generic runtime must not become multi-team-dev-specific. |

## Out Of Scope

- Direct implementation.
- Blazor UI redesign unless required by SB08 proof/projection visibility.
- Replacing all templates or all drivers in one pass.
- Introducing generic runtime references to software-development, AgentFramework, MAF, browser, project-structure, GitHub, or .NET-specific concepts.
