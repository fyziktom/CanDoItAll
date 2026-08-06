# ADR-007: Process semantics and recovery are owned by Processes

- Status: Accepted for implementation
- Date: 2026-08-06

## Context

The MAF project currently contains `ProcessArtifactRecoveryService`, which knows process source kinds, process run/step identifiers, managed artifact paths, process status semantics, and `ProcessStepOutcomeResult`. Generic Core also contains process-specific provider selection and criticality branches.

This contradicts the intended direction: Processes may consume AgentFramework integration; AgentFramework and MAF must not depend on Processes or encode process-domain rules.

## Decision

Introduce provider-neutral application policies:

- `IExecutionProviderSelectionPolicy`
- `IAgentExecutionOutcomeRecoveryPolicy`
- `IExecutionCriticalityPolicy` or an immutable criticality/output policy captured at admission

The MAF adapter reports typed partial evidence and typed failure/recovery opportunities. It does not read process artifacts or synthesize a process outcome.

`CanDoItAll.Modules.Processes` implements:

- process provider-selection policy,
- process artifact recovery policy,
- process-specific criticality/output policy,
- translation into `ProcessStepOutcomeResult`,
- canonical completion and evidence gates.

A recovered outcome must enter the same `ProcessStepCompletionCoordinator` path as a synchronously observed result. It cannot bypass normal completion gates.

## Consequences

- No literal `process-step`, process status type, or process artifact path remains in the MAF assembly.
- Generic execution Core reasons from typed policy snapshots rather than source-kind string checks.
- Process behavior can be tested without constructing MAF.
- MAF failures can support other domains through other policies.

## Proof

- Architecture scan finds no process-specific symbol or source-kind branch in MAF.
- Process recovery tests instantiate the Processes policy directly.
- Integration test proves MAF typed failure -> application recovery pipeline -> ordinary process completion gates.
- Dependency graph remains `Processes -> AgentFramework contracts`, never the reverse.
