# Verified Findings And Refined Interpretation

## F001: Direct Agent Path Has Artifact Finalization, Workflow Path Does Not Show Equivalent Finalization

Severity: Critical

The direct AgentFramework path projects artifacts and runs missing-artifact recovery before transition. The workflow-backed path currently goes through `HandleWorkflowExecutionOutcomeAsync`, which only observes/transitions workflow outcome in the inspected code.

Refined interpretation:

This is not a “workflow bug”. It is a Processes boundary bug. Workflows should not learn process artifact semantics; the Processes dispatcher needs a process-owned finalization layer called after any executor outcome.

Required fix:

Introduce a `ProcessStepCompletionFinalizer` or equivalent service owned by `CanDoItAll.Modules.Processes`. It must accept executor-neutral completion context and return a final process transition decision.

## F002: Artifact Completion Is Too Close To “Record Exists”

Severity: Critical

Current missing required artifacts are resolved from expected artifacts minus recorded expectation ids. This is insufficient because it cannot distinguish valid evidence from placeholder, stale file, wrong format, final chat text, or incomplete recovery output.

Refined interpretation:

The runtime needs a validation state, not just a recorded id set. A required expectation is complete only when an artifact record exists and its content/provenance/mode/format/freshness validation passes.

Required fix:

Create an artifact contract validation model that returns statuses such as `Satisfied`, `Missing`, `InvalidFormat`, `InsufficientEvidence`, `StaleOrWrongRun`, `WrongProducerMode`, `PlaceholderOnly`, and `RecoveryRequired`.

## F003: Recovery Depends On Shared Mutable Candidate State

Severity: High

`DispatchCandidate` is a record-like object but contains mutable `HashSet<Guid>` and `HashSet<string>` collections. Recovery creates a `candidate with { ... }`, projects artifacts through the recovery candidate, then checks the original candidate for remaining missing artifacts.

Refined interpretation:

The current behavior depends on shared mutable collection references. This may work today, but it is fragile and implicit. Future defensive copying or refactoring can break recovery detection.

Required fix:

Projection/finalization must return explicit `ArtifactProjectionResult` and/or reload the artifact ledger from PostgreSQL after projection/recovery. Do not rely on in-memory mutation as the authoritative completion state.

## F004: Manager Recovery Exists, But Resolver Is Too Fuzzy

Severity: High

Manager recovery first tries run-bound manager id/name/assignment, then falls back to manager-like option/agent names and tokens including `lead`.

Refined interpretation:

Recovery of process artifacts is a governance operation. Selecting a random “lead” is worse than blocking with an explicit “no recovery manager” diagnostic.

Required fix:

Require an explicit process-manager assignment, manager agent id, or recovery capability/tag such as `process-artifact-recovery-manager`. Remove or heavily downgrade generic `lead` fallback.

## F005: Projection Skips Many Required-Artifact Failure Signals As Logs

Severity: High

Several projection paths log missing paths, unreadable files, or failed projections and continue. This is safe for optional artifacts, but not enough for required artifacts.

Refined interpretation:

Logs are not process artifacts and do not help the next process step. Required artifact projection failures need durable diagnostics that become recovery input and explain why retrying the same executor is not enough.

Required fix:

Add `ArtifactProjectionDiagnostics` or equivalent durable record/event with expectation id, attempted producer, source path, failure kind, evidence references, and retry/recovery recommendation.

## F006: Response Text Projection Is Useful But Needs Mode Guards

Severity: High

The runtime can project final assistant response text into a required managed text artifact path. That is good for narrative handoff artifacts but dangerous for evidence/deliverables.

Refined interpretation:

A final response can satisfy `NarrativeArtifact` or some `DecisionArtifact` contracts, but it must not satisfy `EvidenceArtifact`, `RuntimeProof`, or concrete `DeliverableArtifact` contracts unless the expectation explicitly allows it and validation passes.

Required fix:

Introduce artifact expectation modes or validation profiles with allowed producers.

## F007: Auto Decision Artifacts Can Complete Without A Durable File

Severity: Medium

Completed decision artifacts may be recorded from process metadata and response summaries without managed storage paths.

Refined interpretation:

This is acceptable for a decision summary only if the artifact contract declares it as an auto-recordable process decision. It must not satisfy evidence/deliverable expectations.

Required fix:

Separate auto-recordable decision artifacts from file-backed artifacts in the validation model.

## F008: Existing Managed File Projection Risks Stale Artifacts

Severity: Medium

Projection can import existing managed files when the expected path already exists.

Refined interpretation:

This helps recover artifacts written through tools that did not register them properly. But it can also accept stale files if the run id, execution id, or current-run root is not validated strongly enough.

Required fix:

Current-run validation should require one or more of: current process run path, execution run id, matching external reference key, tool receipt, file timestamp window, or explicit accepted carry-forward rule.

## F009: Retry Policy Does Not Yet Encode “Invariant Failure”

Severity: Critical

The user reports steps doing five retries while the same artifact remains missing or malformed.

Refined interpretation:

A retry is only useful when the next attempt has new information, a corrected prompt, a provider fallback, a recovered upstream artifact, or a different executor. If the failure is deterministic artifact contract non-compliance, the runtime should switch to diagnostics/recovery/blocking rather than repeat.

Required fix:

Track artifact failure fingerprints and use them to avoid repeated identical retries.
