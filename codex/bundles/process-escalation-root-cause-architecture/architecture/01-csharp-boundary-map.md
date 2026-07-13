# C# Boundary Map

## Target Projects

- `CanDoItAll.Processes.Contracts`: typed process capability/readiness contracts and serializable DTOs only.
- `CanDoItAll.Processes.Runtime`: domain-neutral runtime state transitions, result receipts, artifact lineage primitives, and diagnostic records.
- `CanDoItAll.Processes.Application`: orchestration services and composition of runtime, projections, dispatch queue, readiness checks, and recovery classification.
- `CanDoItAll.Processes.Drivers.Abstractions`: driver strategy contracts for readiness contributions, recovery classification, and domain-specific repair playbooks.
- `CanDoItAll.Processes.Drivers.Standard`: generic non-domain drivers and default recovery policies.
- `CanDoItAll.Modules.Processes`: composition root and adapter between process runtime and MAF/agent execution.
- `CanDoItAll.AgentFramework.Maf`: agent runtime context assembly and capability policy enforcement.
- Optional later project if justified: a domain driver project for software/.NET delivery, but only if it reduces generic/module coupling and is wired through existing composition roots.

## Target Top-Level Types

- `ProcessStepReadinessContract`: serializable contract describing required tools, MCPs, skills, suppressions, allowed operations, instruction fragments, and receipt gates.
- `ProcessStepReadinessDiagnostic`: domain-neutral diagnostic for missing, denied, suppressed, or incompatible capabilities.
- `ProcessFailureCategory`: domain-neutral enum or value object for missing artifact, missing capability, denied capability, policy violation, provider failure, timeout, child-run blocked, instruction non-compliance, and unknown.
- `ProcessStrategyResultDiagnosticRecord`: persisted safe diagnostic projection from `StrategyResultEnvelope`.
- `IProcessStepReadinessResolver`: application-level resolver that combines definition, assignment, agent, process scope, and driver contributions.
- `IProcessDriverRecoveryClassifier`: driver extension point for domain-specific recovery policy after generic classification.
- `IProcessBlockedDiagnosticProjector`: projection collaborator that converts persisted diagnostics into read-model summaries.

## Contracts Vs Implementations

- Contracts must contain only serializable process concepts and stable identifiers.
- Runtime must not know MAF, agent templates, project structure UI, .NET tools, or Playwright names except as generic capability identifiers supplied by contracts.
- Application implementations may coordinate stores and strategy factories but should not embed domain recovery rules.
- Module implementations may translate between CanDoItAll agents/MAF and process contracts.
- Driver implementations may know .NET/software-delivery details if placed in domain-owned driver/template areas.

## Composition Root Responsibilities

- Register readiness resolvers, diagnostic projectors, and recovery classifiers.
- Register domain drivers through explicit catalogs.
- Wire MAF capability scope overrides from process readiness results.
- Keep driver instances reusable; per-run data should be passed as immutable context rather than captured in service instances.

## Old Class Responsibilities To Remove Or Leave

- Leave state transition authority in `ProcessRuntimeEngine`.
- Remove diagnostic formatting and classification growth from `ProcessRuntimeDispatchApplicationService`.
- Remove projection enrichment growth from `ProcessRuntimeProjectionQueryService` into targeted collaborators when touched.
- Leave generic blocker text validation in `AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs`, but move domain proof requirements out of it.
- Leave generic image prompt normalization in `WorkspaceImageAnalysisPromptNormalizer`.

## Temporary Bridges And Removal Plan

- Bridge old `ProcessCapabilityScope` to the new readiness contract during migration.
- Keep existing `RequiredReceipts` compatibility until templates and drivers emit the new contract.
- Add removal notes in subbundle execution reports when old launch-variable keys remain only for backward compatibility.
- Do not add new partial files as bridge layers unless the subbundle explicitly marks them temporary and schedules removal.
