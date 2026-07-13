# C# Pattern Selection Records

## PSR-01: Typed State Machine Over Prompt-Only Completion

- Decision: Model step finalization, handoff, and recovery routing as typed runtime state transitions.
- Reason: Prompt text can be lost through context compression and cannot safely gate downstream scheduling.
- Rejected option: More finalizer prompt instructions and broader retry heuristics.
- Risk: More contracts and persistence state. Mitigation is to keep contracts generic and test them directly.

## PSR-02: Artifact Lineage Ledger

- Decision: Preserve concrete artifact instance lineage separately from slot availability.
- Reason: A consumer step needs to know exactly which connected upstream artifact satisfies each required input, including non-direct previous steps.
- Rejected option: Continue using `AvailableArtifactSlots` as readiness proof.
- Risk: Persistence changes and migration. Mitigation is additive state and characterization tests before behavior changes.

## PSR-03: Step Contract Retrieval Tool

- Decision: Provide agents and finalizers a tool-backed way to fetch the durable current step contract and input package.
- Reason: Agents can lose context. Finalization must use fresh runtime facts, not stale conversation context.
- Rejected option: Embed all contract details in the initial prompt.
- Risk: Tool authorization and data exposure. Mitigation is scoped retrieval to current assignment/run and sensitivity-aware artifact package metadata.

## PSR-04: Recovery Router Strategy

- Decision: Extract recovery classification and routing into a cohesive runtime service with strongly typed categories and owners.
- Reason: Missing upstream artifact, denied capability, transient provider timeout, and current-step non-compliance are different failures with different owners.
- Rejected option: Adapter-provided `SafeToRetry` as the primary routing signal.
- Risk: Initial taxonomy gaps. Mitigation is manager-required unknown category with actionable diagnostics, never silent retry.

## PSR-05: Driver Policy Ports

- Decision: Move domain-specific completion, context packaging, and evidence policy behind process driver abstractions.
- Reason: Generic runtime must support arbitrary enterprise processes, while software-development processes need extra policy.
- Rejected option: Hard-code multi-team development behavior into runtime or application services.
- Risk: Over-abstracting. Mitigation is to add ports only where a concrete driver-specific decision exists.

## PSR-06: Service Extraction From Partial Clusters

- Decision: Extract cohesive services from `ProcessRuntimeEngine` and `AgentFrameworkProcessExecutionAdapter` responsibilities.
- Reason: More partial files would preserve test pain and unclear ownership.
- Rejected option: Continue partial-class expansion.
- Risk: Touching broad code. Mitigation is characterization-first, smallest extraction per responsibility, and source assertions after each extraction.
