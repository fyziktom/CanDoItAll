# C# Boundary Map

## Target Projects

- `CanDoItAll.Processes.Contracts`
  - Owns stable DTOs/enums for receipt rule metadata, route kind, route metadata, and trace contract if these are serialized across boundaries.
- `CanDoItAll.Processes.Abstractions`
  - Owns minimal interfaces only when a real test or provider boundary requires them.
- `CanDoItAll.Processes.Runtime`
  - Owns process state transition and generic recovery classification.
- `CanDoItAll.Processes.Application`
  - Owns application service coordination and generic recovery advice orchestration.
- `CanDoItAll.Modules.Processes`
  - Owns MAF adapter integration and composition for process execution in the module.
- `CanDoItAll.Modules.Workbench`
  - Owns .NET/software-delivery launch variables, scaffold content checks, browser/runtime receipt rule emission, and .NET recovery advice provider.
- `Templates/Processes/processes/*`
  - Own process-specific branch names, completion route metadata, and prompt/step evidence matrices.

## Target Top-Level Types

- `ProcessCompletionGateEvaluator`
- `ProcessCompletionGateContext`
- `ProcessCompletionGateEvaluation`
- `ProcessCompletionIssue`
- `ProcessCompletionIssueRouteKind`
- `ProcessCompletionIssueRoute`
- `ProcessCompletionIssueRouter`
- `ProcessCompletionReceiptRuleResolver`
- `ProcessCompletionRequiredToolReceiptRule`
- `ProcessRequiredToolReceiptEvaluator`
- `ProcessCompletionEvaluationTrace`
- `IProcessRecoveryAdviceProvider`
- `GenericProcessRecoveryAdviceProvider`
- `DotNetSoftwareDeliveryRecoveryAdviceProvider`

Exact namespaces are decided in SB01/SB02 after reading existing conventions.

## Contracts Vs Implementations

- Contracts may expose rule/route/trace records with generic names.
- Implementations may parse template JSON and create Workbench domain rules.
- Workbench providers may use `.NET`, Blazor, scaffold, and software-delivery branch constants.
- Generic evaluator/router must receive domain terms as data and must not compare against hardcoded domain literals.

## Composition Root Responsibilities

- Register evaluator/resolver/router services.
- Register recovery advice providers.
- Wire Workbench-specific provider only from module/service registration.
- Keep provider discovery explicit and testable.

## Old Class Responsibilities To Remove Or Leave

Remove from adapter:

- raw receipt rule parsing;
- branch applicability filtering;
- completion issue route decision;
- product/content gate ordering policy;
- durable evaluation trace construction.

Leave in adapter:

- MAF execution invocation;
- raw output parsing entry point;
- managed artifact materialization plumbing;
- conversion between evaluation result and `StrategyResultEnvelope`.

Remove from `ProcessStepRecoveryInstructionBuilder`:

- .NET tool names;
- QA step keys;
- software-delivery branch keys;
- Blazor/scaffold-specific guidance.

Leave in generic builder:

- diagnostic grouping;
- provider orchestration;
- generic blocker/retry language.

## Temporary Bridges And Removal Plan

- A temporary adapter facade may delegate old methods to extracted services during SB01-SB04.
- Any temporary partial file must be named as a migration bridge and removed or reduced to delegation by SB11.
- Architecture checkpoint after SB04 blocks dependent template work if the adapter still owns core route decisions.

## Corrective Target Boundaries

| Type | Project | Responsibility |
|---|---|---|
| `AgentFrameworkProcessExecutionAdapter` | Modules.Processes | Thin implementation of the two process-driver boundary interfaces; descriptors and delegation only |
| `AgentFrameworkProcessStepExecutor` | Modules.Processes | MAF agent-run orchestration and sequencing only |
| `ProcessSubprocessCoordinator` | Modules.Processes | Mapped child launch, pending-child deferral, and bridge translation |
| `ProcessRuntimeOwnedStepCoordinator` | Modules.Processes | Ordered runtime-owned executor dispatch and handoff to completion |
| `ProcessStepCompletionCoordinator` | Modules.Processes | Coordinates materialization, grounding, gates, acceptance, and result mapping |
| `ProcessManagedArtifactService` | Modules.Processes | Managed artifact materialization, acceptance, and readback hashing |
| `ProcessOutcomeGroundingValidator` | Modules.Processes | Validates cited path/evidence grounding against current-run evidence |
| `ProcessCompletionGateEvaluator` plus focused gate owners | Modules.Processes | Deterministic generic completion policy evaluation |
| `ProcessCompletionIssueResultFactory` | Modules.Processes | Retry/manager/branch route result envelopes and runtime-gate findings |
| `ProcessOutcomeInterpreter` | Modules.Processes | Branch inference and blocked/completed outcome normalization |
| `IProcessToolReceiptPolicy` catalog | Modules.Processes module boundary | Composes generic and domain-specific receipt semantics without runtime branching |
| .NET receipt policy contribution | domain driver/composition location selected in SB13 | Owns .NET tool families, template selectors, and setup-step guidance |

The adapter must not expose compatibility forwarding methods for moved behavior. Existing external consumers of static adapter helpers must be migrated to the real owner.

## Persistent Repair Reopen Boundaries

- `CanDoItAll.Processes.Runtime`
  - Owns only generic comparison of stable diagnostic identities, retry progress, and bounded manager routing.
  - Receives diagnostic code, evidence hash, retry safety, idempotency, and prior receipts as data.
- `CanDoItAll.Modules.Processes/Drivers/DotNet`
  - Owns the typed parent/child contract for `software-delivery/quality-repair` to `dotnet-quality-repair`.
  - Does not encode Tetris, Calculator, work-time logger, or SVG-app semantics.
- `Templates/Processes/processes/dotnet-quality-repair`
  - Owns .NET repair diagnosis, product mutation, current-execution validation, conditional runtime/browser proof, bughunt handoff, and no-go evidence.
- `Templates/Processes/processes/software-delivery`
  - Owns when the .NET repair subprocess is launched and how accepted/no-go child artifacts flow to QA recheck or escalation.
- Workbench .NET contributors
  - Continue to own .NET/Blazor content checks, browser evidence policy, launch variables, and domain recovery advice.

No generic runtime, dispatcher, application coordinator, or subprocess bridge may compare `.NET`, `Blazor`, UI text, sample-app names, file names, or software-delivery step/branch keys.
