# C# Current State Inventory

## CodeAnalytics Evidence

- Snapshot id: `snap-20260709103653-3a49f8a9`.
- Solution: `repo://CanDoItAll.slnx`.
- Scope included 22 projects and 634 documents across process contracts, application, runtime, templates, drivers, Modules.Processes, Modules.Workbench, MAF workflow/executor projects, and unit tests.
- Dependency analysis reported no cycles.
- Dashboard non-blocking diagnostics included duplicate generated type display names and known `Microsoft.OpenApi` advisory warnings in unrelated projects.

## Source Files Inspected

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionGates.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Types.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepRecoveryInstructionBuilder.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/software-delivery/steps/qa-validation.md`
- `repo://Templates/Processes/processes/software-delivery/steps/qa-recheck.md`
- `repo://Templates/Processes/processes/software-delivery/steps/quality-repair.md`

## Large Classes And Partial Classes

- `AgentFrameworkProcessExecutionAdapter` is a large partial class cluster in `Modules.Processes`. It owns MAF execution, completion gate validation, receipt checks, product completion parsing, branch signal conversion, managed artifact handling, and recovery policy conversion.
- The bundle must not add another permanent partial file as the final architecture. Temporary partial migration is allowed only if a later subbundle removes or thins the old adapter responsibility.
- `ProcessLaunchApplicationService` is a broad application service that currently participates in launch variable normalization.
- `ProcessStepRecoveryInstructionBuilder` is a focused file but has wrong domain ownership.

## Constructor Dependency Counts

Exact constructor counts must be captured during SB01 before edits. Current preparation evidence is static and does not replace that requirement.

SB01 must record:

- adapter constructor dependencies before extraction;
- new evaluator/resolver/router constructor dependencies;
- whether extracted services can be instantiated in unit tests without MAF runtime.

## Current Responsibilities

- Adapter partial class owns receipt/product/content/process completion evaluation and result conversion.
- Contracts own capability receipt shape but not product completion receipt rule shape.
- Application launch service owns step-scoped launch variable selection and string formatting.
- Workbench contributor owns .NET/Blazor-specific launch variable emission and scaffold checks.
- Recovery instruction builder owns generic recovery orchestration but currently also owns .NET/QA guidance.

## Current Tests

- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessCapabilityScopeContractTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRequiredRuntimeToolNamesTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessStepRecoveryInstructionBuilderTests.cs`

## Missing Tests

- Incident fixture reproducing the four Tetris QA attempts without LLM.
- Branch-aware product receipt parser tests for object arrays and by-step maps.
- Branch-aware capability/product receipt dedup tests.
- Completion issue route tests for accepted-branch content failure.
- Retry-budget tests proving branch-routable issue does not consume same-step retry.
- Architecture forbidden-token tests for generic runtime/application code.
- Template inventory migration tests.
- Acceptance criteria matrix tests for Calculator-like and Tetris-like project structures.

## Risk Notes

- The adapter partial cluster is a high-risk extraction target; first extraction must preserve behavior.
- Do not create a second monolith service named `ProcessCompletionGateEvaluator` that still owns domain-specific template knowledge.
- Do not solve by adding `if stepKey == "qa-validation"` or equivalent helper constants in generic code.

## Corrective Inventory 2026-07-09

- Fresh CodeAnalytics snapshot: `snap-20260709195146-c1b7a73e`.
- Healthy corrective scope: seven projects, 455 documents, no blocking snapshot error, no reported dependency cycle.
- `AgentFrameworkProcessExecutionAdapter` spans 20 partial files and approximately 7,900 lines.
- The type has nine constructor dependencies and owns MAF invocation, readiness, runtime preflight, runtime-owned step dispatch, subprocess coordination, managed-artifact lifecycle, grounding, completion gates, product-path inspection, receipt/lifecycle correlation, branch inference/routing, retry classification, and adapter result conversion.
- The old `ProcessCompletionGateEvaluator` extraction is shallow: it owns aggregation/ordering while its gate delegates call private methods on the adapter partial type.
- `ProcessRuntimeArchitectureBaselineTests.AgentFrameworkProcessExecutionAdapter_partial_cluster_has_no_unplanned_growth` freezes the known partial list and therefore permits the invalid architecture indefinitely.
- Domain leaks remain in adapter receipt/evidence code: .NET build/test/new tool matching, .NET setup step-key switches, and .NET requirement-text expansion.

| Current source | Responsibility | Dependencies | Corrective owner | Independent test seam | Risk |
|---|---|---|---|---|---|
| adapter main | boundary plus execution orchestration | assignment store, agent catalog, MAF workspace | thin adapter plus agent step executor | fake executor for adapter; fake MAF boundary for executor | high |
| subprocess partials | child launch, pending state, artifact bridge | stores, coordinators, bridge | subprocess coordinator | in-memory stores and fake bridge | high |
| managed artifact/grounding partials | materialize, accept, readback, grounding | workspace file service | managed artifact service and grounding validator | fake workspace files | high |
| completion partials | paths, receipts, state, criteria, routes | assignment/output/receipts | completion gate owners and result router | pure records and fake policy catalog | high |
| result conversion/recovery partials | branch inference, retry classification, result envelopes | completion services | outcome interpreter and adapter result factory | pure input/output tests | high |
| metadata/types partials | invocation metadata and parsing primitives | launch data only | focused builders/parsers/models | pure tests | medium |

## Persistent Repair Reopen Inventory 2026-07-10

- `ProcessRecoveryClassifier` computes one fingerprint for the complete diagnostic batch from every `Code:EvidenceHash` pair.
- Whole-batch comparison treats removal of an incidental diagnostic as a new failure even when another stable diagnostic identity is unchanged.
- Default policy allows four automatic retries and three repeats of an identical whole-batch fingerprint; the production incident therefore dispatched five executions before manager action.
- `software-delivery/quality-repair` is a direct product-mutating work step. It combines evidence interpretation, defect diagnosis, mutation, runtime proof, and self-acceptance in one agent execution.
- The .NET subprocess contract provider already isolates typed child-process selection from the generic runtime and is the correct owner for a new `.NET quality repair` contract.
- Existing `blazor-app-repair-fix` demonstrates repair/revalidation branching but is application-family-specific and too broad to become the generic software-delivery quality-repair contract.

| Current source | Current problem | Target owner | Test seam |
|---|---|---|---|
| `ProcessRecoveryClassifier` | aggregate fingerprint hides persistent diagnostic atoms | generic persistent-diagnostic progress classifier inside the existing focused type | arbitrary diagnostic codes and hashes with prior receipts |
| `software-delivery/quality-repair` | one agent diagnoses, mutates, validates, and accepts itself | runtime-owned `dotnet-quality-repair` subprocess | template projection and subprocess contract tests |
| .NET recovery advice/templates | known failed browser proof may be described as residual risk | .NET repair diagnosis and revalidation prompts | generic .NET UI/non-UI fixtures without sample names |
| human `repair-escalation` | reached before manager/specialist diagnosis-guided second repair | child no-go output after bounded bughunt | child accepted/no-go bridge tests |
