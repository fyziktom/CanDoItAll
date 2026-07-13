# Current State

## GPTPro Analysis Summary

GPTPro concluded that the `prepare-solution-skeleton` blocker is not primarily evidence that an agent cannot scaffold a .NET project. The attached calculator output contains product skeleton files but lacks process-managed evidence such as:

```text
artifacts/process-runs/<parent-run-id>/steps/prepare-solution-skeleton.md
artifacts/process-runs/<child-run-id>/steps/setup-handoff.md
artifacts/process-runs/<child-run-id>/steps/setup-handoff-after-repair.md
```

The strongest hypothesis is that product side effects occurred while the parent process contract stayed unsatisfied. The runtime then blocked on missing parent evidence, the projection layer could not find exact AgentFramework result summary diagnostics, and manager rework repeated an ambiguous step.

## Local Template Inventory

Structured parsing of `repo://Templates/Processes/processes/*/definition.json` found nine subprocess parent steps:

| Process | Step | Child process | Manual skip | Parent artifact count | Child mappings |
| --- | --- | --- | --- | ---: | ---: |
| `dotnet-development-slice` | `prepare-solution-skeleton` | `dotnet-solution-setup` | `true` | 1 | 1 |
| `dotnet-development-slice` | `implement-code-change` | `dotnet-feature-function-implementation` | `false` | 1 | 1 |
| `dotnet-development-slice` | `slice-repair-code-change` | `dotnet-feature-function-implementation` | `false` | 1 | 1 |
| `software-delivery` | `architecture-review` | `dotnet-architecture-design-review` | `false` | 2 | 2 |
| `software-delivery` | `implementation` | `dotnet-development-slice` | `false` | 2 | 2 |
| `software-delivery` | `capture-ui-screenshots` | `dotnet-ui-screenshot-writeback` | `false` | 1 | 1 |
| `software-delivery` | `capture-ui-screenshots-after-repair` | `dotnet-ui-screenshot-writeback` | `false` | 1 | 1 |
| `software-delivery` | `record-runtime-commands` | `dotnet-runtime-command-writeback` | `false` | 1 | 1 |
| `software-delivery` | `record-runtime-commands-after-repair` | `dotnet-runtime-command-writeback` | `false` | 1 | 1 |

Every parent currently has one legacy child mapping per parent artifact. The child terminal processes expose richer accepted, repaired, and escalation outcomes in prose and step artifacts, but the parent metadata cannot express accepted arrays, no-go arrays, already-satisfied outputs, validation receipt requirements, or materialization modes.

## Child Terminal Outcomes Observed

| Child process | Accepted outputs | Repaired accepted outputs | No-go outputs |
| --- | --- | --- | --- |
| `dotnet-solution-setup` | `setup-handoff` / `setup-handoff-packet` | `setup-handoff-after-repair` / `setup-handoff-packet-after-repair` | `setup-repair-escalation` / `setup-repair-escalation-packet` |
| `dotnet-feature-function-implementation` | `feature-handoff` / `feature-handoff-packet` | `feature-handoff-after-repair` / `feature-handoff-packet-after-repair` | `feature-repair-escalation` / `feature-repair-escalation-packet` |
| `dotnet-development-slice` | `slice-handoff` / `slice-handoff-packet` | `slice-handoff-after-repair` / `slice-handoff-packet-after-repair` | `slice-repair-escalation` / `slice-repair-escalation-packet` |
| `dotnet-architecture-design-review` | `architecture-handoff` / `architecture-design-review-handoff` | Not present | Blocked child run or missing required architecture artifacts |
| `dotnet-ui-screenshot-writeback` | `screenshot-handoff` / `ui-screenshot-writeback-handoff` | Parent repaired path reuses same child process output | Missing screenshots/no-UI proof, missing image-analysis receipts, or blocked child run |
| `dotnet-runtime-command-writeback` | `runtime-command-handoff` / `runtime-command-handoff` | Parent repaired path reuses same child process output | Missing launcher-compatible command-node receipts or blocked child run |

## Runtime And Projection Source Findings

- `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessExecutionObservationContracts.cs` exposes `ProcessExecutionObservationQuery` with run ids and `TakePerRun`, but no step selector.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs` groups execution runs by run id and applies `TakePerRun` before exact step grouping.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` already supports filtering `ExecutionRunQuery.ProcessStepId`, so the projection reader can be corrected without inventing a new persistence query primitive.
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs` builds blocked operator action text from optional AgentFramework observation diagnostics and falls back to generic capability and artifact-slot hints when diagnostics are unavailable.
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs` builds generic manager rework instructions that lack a first-class blocked-step packet.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs` and `.SubprocessState.cs` already contain subprocess coordination, but completed child evidence is resolved generically from child step files, not from typed accepted/no-go mappings.
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs` creates `ProducedArtifactRef` values with `ArtifactInstanceId.New()` and a hash derived from output text plus step/slot ids.
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs` computes `appliedResult`, but `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs` builds artifact ledger events from `command.Result`.
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs` and `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessStepContractPromptBuilder.cs` expose required and expected artifacts mostly as slot ids and hashes.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/AgentProcessReadinessEvaluator.cs` checks required runtime tool names against agent access/capability metadata.
- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` composes `project_structure_process_subprocess_launch` only for governed process automation with process run id, step id, and `ExecuteExternalAction` scope.

## CodeAnalytics Evidence

- Snapshot id: `snap-20260708104406-98263759`
- Scoped solution: `repo://CanDoItAll.slnx`
- Scoped projects loaded: 16
- Documents loaded: 427
- Dependency cycles: `[]`
- Notable hotspots: large AgentFramework execution and process projection/runtime/template files, including `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, `ProcessRuntimeProjectionQueryService.cs`, and `ProcessTemplatePackLoader.cs`.
- Preparation caveat: CodeAnalytics class diagrams were truncated for large projects. Implementation subbundles must refresh targeted evidence after source edits.

## Implementation Implication

The correct bundle shape is not a single prompt fix. The repair needs layered runtime and template work:

1. make exact diagnostics recoverable;
2. persist structured process outcome summaries;
3. model typed subprocess terminal contracts;
4. bridge parent artifacts from accepted child evidence deterministically;
5. make artifacts semantic and content-grounded;
6. preflight exact composed tools;
7. harden every affected template and artifact contract;
8. prove the entire class with regression tests and architecture gates.
