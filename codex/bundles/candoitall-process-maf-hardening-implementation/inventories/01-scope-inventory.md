# Scope Inventory

## Source Code Inventory

| Area | Source | Why it matters | Owning subbundle |
| --- | --- | --- | --- |
| Observation query contract | `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs` | Adds exact step selector or run/step selector contract. | SB02 |
| Observation reader | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs` | Currently applies `TakePerRun` before step grouping; should use `ExecutionRunQuery.ProcessStepId`. | SB02 |
| AgentFramework execution query | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | Already supports `ProcessStepId` filtering and owns ResultSummary persistence. | SB02, SB03 |
| Operator diagnostics | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorActionDiagnostics.cs` | Parses AgentFramework observation summaries. Needs runtime-receipt fallback input. | SB02 |
| Projection/operator action | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs` | Builds operator problem summary, capability hints, and rework instructions. | SB02 |
| Dispatch/rework | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs` | Rework instructions need blocked packet and no blind retry. | SB02 |
| Strategy contracts | `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs` | Defines runtime step contract and produced artifacts. | SB04, SB06 |
| Runtime artifact contracts | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeArtifactContracts.cs` | Builds required/expected artifact contract and applies produced artifacts. | SB06 |
| Runtime result finalization | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs` | Computes `appliedResult`; ledger currently uses original command result. | SB06 |
| Artifact ledger helper | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs` | Must accept applied result and avoid ledgering invalid artifacts. | SB06 |
| Prompt builder | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessStepContractPromptBuilder.cs` | Must render semantic descriptors, primary refs, child mappings, and gates. | SB06 |
| Adapter result conversion | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` | Synthesizes produced artifact refs and subprocess retry issues. | SB05, SB06 |
| Adapter managed artifacts | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs` | Reads/writes/validates managed artifacts and primary refs. | SB05, SB06 |
| Adapter subprocess launch | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs` | Coordinates mapped subprocesses before agent execution. | SB05 |
| Adapter subprocess state | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.SubprocessState.cs` | Resolves completed child evidence generically today. | SB05 |
| Template document model | `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs` | Needs typed metadata model and loader validation. | SB04, SB08 |
| Template summaries | `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateStepSummaries.cs` | Must surface typed subprocess contracts and artifact descriptors. | SB04, SB08 |
| Launch enrichment | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` | Creates assignments, required tool names, launch variables, produced slots. | SB04, SB07, SB08 |
| Step brief builder | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs` | Renders template step details and subprocess guidance. | SB04, SB08 |
| Agent readiness | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/AgentProcessReadinessEvaluator.cs` | Current readiness is metadata/capability-level only. | SB07 |
| Runtime tool provider | `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` | Actual subprocess tool requires governed context and `ExecuteExternalAction`. | SB07 |
| Tool policy/catalog | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`, `ToolCapabilityRegistry.cs`, `ToolContractCatalog.cs` | Exact preflight should reuse known tool contracts without duplicating magic strings. | SB07 |

## Process Template Inventory

Implementation-time parser transcript: `bundle://proof/SB01/transcripts/template-inventory.txt`.
All nine subprocess parent steps currently rely on `SubprocessChildStepKey`/`SubprocessChildArtifactTitle`; no row has a populated `SubprocessChildArtifactExpectationId`.

| Parent process | Step | Child process | Current machine mapping | Missing typed contract |
| --- | --- | --- | --- | --- |
| `dotnet-development-slice` | `prepare-solution-skeleton` | `dotnet-solution-setup` | `solution-skeleton-evidence` -> `setup-handoff` | Accepted repaired handoff, no-go escalation, manual-skip policy, parent synthesis mode. |
| `dotnet-development-slice` | `implement-code-change` | `dotnet-feature-function-implementation` | `slice-change-set` -> `code-change` | Accepted `feature-handoff`, repaired handoff, no-go escalation, child validation evidence. |
| `dotnet-development-slice` | `slice-repair-code-change` | `dotnet-feature-function-implementation` | `slice-repair-change-set` -> `code-change` | Repair-scoped accepted/no-go semantics and inherited repair target proof. |
| `software-delivery` | `architecture-review` | `dotnet-architecture-design-review` | two child mappings | Typed terminal architecture handoff, classification, and review risk mapping. |
| `software-delivery` | `implementation` | `dotnet-development-slice` | two child mappings | Accepted/repaired slice handoff and slice no-go escalation. |
| `software-delivery` | `capture-ui-screenshots` | `dotnet-ui-screenshot-writeback` | `ui-screenshot-writeback` -> `screenshot-handoff` | Screenshot/no-UI accepted output plus image-analysis receipt requirements. |
| `software-delivery` | `capture-ui-screenshots-after-repair` | `dotnet-ui-screenshot-writeback` | repaired parent artifact -> `screenshot-handoff` | Repaired screenshot accepted output plus image-analysis receipt requirements. |
| `software-delivery` | `record-runtime-commands` | `dotnet-runtime-command-writeback` | `runtime-command-writeback` -> `runtime-command-handoff` | Run-command handoff plus command-node receipt requirements. |
| `software-delivery` | `record-runtime-commands-after-repair` | `dotnet-runtime-command-writeback` | repaired parent artifact -> `runtime-command-handoff` | Repaired run-command handoff plus command-node receipt requirements. |

## Artifact Template Inventory

- `repo://Templates/Processes/shared/artifacts` contains shared artifact schemas and markdown templates used by broader process templates.
- SB08 must audit shared artifact templates that describe process handoff, QA proof, runtime command, screenshot, implementation, review, or escalation evidence. Any artifact template that encodes hard completion semantics in markdown only must gain typed validation metadata or an explicit exception.
- The first implementation pass must focus on artifacts referenced by the subprocess parents above. Other shared artifacts are in audit scope for detection and follow-up rows; they are not all necessarily edited if no hard process gate is encoded there.

## Test Inventory To Build

Implementation-time source/test transcript: `bundle://proof/SB01/transcripts/source-test-inventory.txt`.
CodeAnalytics snapshot: `snap-20260708111133-0494a6f9`; dependency cycles `[]`; large-class hotspots remain in `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, `AgentToolInvocationPolicy.cs`, `ProcessTemplatePackLoader`, and runtime adapter/projection partial clusters.

- Projection/observation tests for exact step lookup and no blind retry.
- AgentFramework result summary tests for structured process output and failure paths.
- Runtime/application tests for subprocess contract parsing and parent bridge states.
- Adapter tests proving `ExecuteRunAsync` is not called when runtime-owned subprocess handling can decide.
- Runtime tests for applied-result artifact ledger behavior.
- Template-loader validation tests for subprocess contract consistency and manual skip policy.
- Tool preflight tests for missing, denied, uncomposed, and available tools.
- Architecture guard test or static assertion preventing new process/adapter logic from being added only as new partial dumps.
