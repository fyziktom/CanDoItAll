# C# Boundary Map

## Contracts Layer

- Project: `CanDoItAll.Processes.Contracts`
- Owns serializable models only.
- Candidate additions:
  - `ProcessStepCapabilityContract`
  - `ProcessRequiredCapability`
  - `ProcessRequiredReceipt`
  - `ProcessToolSuppressionRule`
  - `ProcessScopedInstructionFragment` placement extensions if needed.
- Must not depend on MAF runtime implementations or UI modules.

## Application Layer

- Project: `CanDoItAll.Processes.Application`
- Owns process logic and orchestration decisions.
- Candidate services:
  - `IProcessStepCapabilityContractCompiler`
  - `IProcessStepReadinessEvaluator`
  - `IProcessRequiredReceiptGate`
  - `IProcessManagerFallbackPlanner`
- May depend on process contracts and abstractions.
- Must not depend directly on Blazor components.

## Runtime And Driver Layer

- Projects: `CanDoItAll.Processes.Runtime`, `CanDoItAll.Processes.Drivers.Abstractions`, `CanDoItAll.Processes.Drivers.Standard`
- Own process runtime identifiers, assignments, events, driver packages, and recovery strategies.
- Driver packages can contribute domain-specific contract fragments and fallback plans.
- Must not leak driver-specific rules into common MAF plugin prompts.

## Modules Layer

- Project: `CanDoItAll.Modules.Processes`
- Owns adaptation between process runtime/application services and AgentFramework execution.
- Maps `ProcessStepCapabilityContract` into MAF metadata and process UI projections.
- Should keep wiring and projections here, not core policy decisions.

## MAF Layer

- Projects: `CanDoItAll.AgentFramework.Core`, `CanDoItAll.AgentFramework.Maf`, `CanDoItAll.AgentFramework.Models`
- Own generic execution metadata, capability composition, tool invocation policy, and receipt observation.
- Accepts trusted process metadata but does not interpret process-domain semantics.
- Must not add software delivery, QA, project-management, or UI-design prompt normalization.

## Workbench And HR UI Layer

- Project: `CanDoItAll.Modules.Workbench`
- Owns project-structure process launch UI and readiness presentation.
- Calls application readiness services and renders concrete gaps.
- Must not duplicate contract compilation logic.
