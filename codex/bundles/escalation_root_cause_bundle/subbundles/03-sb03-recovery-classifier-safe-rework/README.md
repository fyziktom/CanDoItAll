# SB03 - Recovery Classifier And Safe Rework

## Status

- `Completed`
- Critical foundation: yes

## Objective

Replace blocked-result manager escalation as the default for safe/idempotent completion-gate failures. Classify recovery using typed diagnostics, retry safety, idempotency, policy, fingerprint, and budget, then route eligible failures to `SafeRetry` / `CurrentStepRetry`.

## Covered Inputs

- GPTPro safe auto-rework finding.
- REQ-005, REQ-006, REQ-017, REQ-018, REQ-020.
- Existing runtime states for safe retry/current-step retry.

## Prerequisites

- SB02 aggregate diagnostics complete.
- Current recovery decision source references refreshed.
- Retry budget and policy defaults identified.

## Exact Source References

- `bundle://codex/03-safe-auto-rework-recovery.md`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionRetryPolicy.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.RecoveryPolicy.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs`

## Deliverables

- `IProcessRecoveryClassifier` or equivalent service in the runtime boundary.
- Typed recovery input that includes aggregate diagnostics, retry safety, idempotency, source gate, policy, fingerprint, and attempt count.
- Mapping from safe/idempotent completion-gate issues to `SafeRetry` / `CurrentStepRetry`.
- Bounded retry budget with repeated-fingerprint escalation.
- Tests replacing current `ManagerRequired`/`Unknown` expectations for safe/idempotent diagnostics.

## Dependency Impact

- SB04 uses the classifier result to choose repair packet shape.
- SB12 validates first-attempt rework and budget-exhausted escalation.
- Incorrect classifier behavior blocks final closure because it is the direct escalation root cause.

## Validation Depth

- Critical foundation with policy, budget, and negative unsafe-diagnostic tests.
- Semantic proof must show the manager escalation is delayed only for safe/idempotent repairable failures.

## Implementation Steps

1. Inventory every caller of `BuildRecoveryDecision`, `ResolveRecoveryRouteKind`, and failure category classification.
2. Define typed classifier input and output records or use existing records if they already fit.
3. Replace substring/message classification for completion-gate diagnostics with typed diagnostic data.
4. Implement safe/idempotent completion-gate policy as `SafeRetry` / `CurrentStepRetry`.
5. Add fingerprinting for repeated diagnostics using stable structured facts.
6. Enforce a bounded retry budget and route budget exhaustion to manager escalation with root-cause details.
7. Preserve unsafe, non-idempotent, policy-denied, and unknown non-completion failures as explicit escalation routes.
8. Update runtime engine tests that currently assert `ManagerRequired` for safe/idempotent completion diagnostics.
9. Add tests for unsafe diagnostic, non-idempotent diagnostic, budget remaining, and budget exhausted.
10. Ensure logs include diagnostic code, fingerprint, attempt count, budget, and route without leaking sensitive content.

## Do Not Do

- Do not retry all blocked outcomes.
- Do not rely on free-form message text to decide recovery.
- Do not add an unbounded retry loop.
- Do not hide policy-denied or unsafe failures behind a fallback retry.

## Acceptance Checklist

- [x] First safe/idempotent completion-gate failure routes to `SafeRetry`.
- [x] Route kind is `CurrentStepRetry` for the incident class.
- [x] Budget exhaustion routes to manager escalation with diagnostic context.
- [x] Unsafe/non-idempotent diagnostics do not auto-retry.
- [x] Existing runtime state vocabulary is reused or extended intentionally.
- [x] Tests cover old broken behavior as failing-first evidence.

## Proof Required

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- Failing-first test showing current manager escalation for safe/idempotent issue.
- Passing classifier and runtime engine tests.
- Source assertions showing no substring fallback for completion-gate recovery.
- Production behavior artifact matrix if new recovery state records are introduced.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB04 and SB12 may proceed only after the classifier routes the incident class to bounded current-step retry.

## C# Architecture Impact

Moves recovery classification toward a typed runtime policy service.

## Boundary Ownership

`Processes.Runtime` owns recovery classification; adapter code supplies structured diagnostics.

## Dependency Direction

Runtime classifier must not reference `Modules.Processes`, Workbench, or template markdown.

## Pattern Decision

Use PSR-004: explicit policy classifier with typed inputs and no silent fallback.

## Testability Contract

Classifier tests must use plain records and run without DB, MAF, file system, or template loading.

## Partial Class Policy

No adapter partial expansion is expected except plumbing to expose aggregate diagnostic data.

## Architecture Proof Required

- Boundary note proving classifier is runtime-owned.
- Dependency/cycle check if contract placement changes.

## Suggested Agent Prompt

```text
Execute SB03 only. Implement typed recovery classification so safe/idempotent completion-gate failures route to bounded current-step retry, with budget-exhausted escalation. Replace tests that currently expect manager-required behavior for this repairable case.
```
