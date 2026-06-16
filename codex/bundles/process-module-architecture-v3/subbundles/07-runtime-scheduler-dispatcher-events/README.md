# SB07 Runtime, Scheduler, Dispatcher Claims, And Event Ports

## Status

- Completed

Completed on 2026-06-15.

## Objective

Implement runtime state transitions, scheduling, dispatcher claim lifecycle, strategy invocation boundary, idempotency behavior, cancellation, terminal states, and event/outbox/artifact ledger ports.

## Why This Bundle Exists

Runtime and dispatcher were the old module's core weakness. This bundle makes state ownership, leases, idempotency, and event emission explicit and testable.

## Covered Inputs

- REQ-002, REQ-003, REQ-020, REQ-026.
- v3 persistence/event/outbox port model.

## Context Reset: Read These First

- SB06 execution report.
- `architecture/05-runtime-dispatcher-and-state-machines.md`
- `architecture/12-runtime-persistence-event-store-and-outbox.md`
- `architecture/04-builder-and-instance-composition.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/05-runtime-dispatcher-and-state-machines.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/12-runtime-persistence-event-store-and-outbox.md`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunTransitions.cs`

## Source Evidence To Use

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunTransitions.cs`
- SB01 dispatch/runtime archive.

## Prerequisites

- SB06 complete.
- Immutable plan fixtures available.

## In Scope

- Run state machine.
- Step state machine.
- Dispatch claim/lease state machine.
- Scheduler ready-work calculation.
- Result idempotency.
- Cancellation handling.
- Terminal state immutability.
- Runtime transition validation.
- Event envelope writes through ports.
- Runtime state/event/outbox/artifact ledger port contracts.

## Out Of Scope

- No EF implementation.
- No manager recovery behavior beyond events/ports.
- No concrete drivers/adapters.
- No UI projections.

## Target Projects / Files

- `src/CanDoItAll.Processes.Runtime`
- runtime tests.

## Deliverables

- Runtime engine.
- Dispatcher boundary.
- Scheduler.
- Event/outbox port contracts.
- Runtime tests.

## Expected Deliverables

- Dispatcher invokes bound strategies and returns envelopes.
- Runtime owns all state mutation.
- Duplicate results and lost leases are safe.

## Dependency Impact

- SB08 implements persistence ports.
- SB09 uses runtime transitions for manager decisions.
- SB10 consumes events.

## Validation Depth

- Validate with transition tests, claim/lease tests, duplicate result tests, lost lease tests, cancellation tests, terminal-state tests, event envelope tests, and dependency scans.

## Architecture Invariants That Must Hold

- Runtime does not reference EF.
- Dispatcher does not decide domain recovery.
- Strategies do not mutate runtime state.
- Every accepted transition emits event/outbox records through ports.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Implement state transition validators.
2. Implement scheduler ready-work calculation.
3. Implement claim/lease lifecycle.
4. Implement dispatcher invocation boundary.
5. Implement idempotency and duplicate result handling.
6. Implement cancellation and terminal state handling.
7. Add tests.

## Refactoring Review Checkpoint

- Split runtime transitions, scheduler, dispatcher, claims, and idempotency into focused classes.
- Verify no giant dispatcher replacement.
- Verify failure-path tests exist.

## Required Tests / Proof

- State transition tests.
- Claim lease/expiry/reclaim tests.
- Duplicate result tests.
- Lost lease tests.
- Cancellation tests.
- Terminal immutability tests.
- Event envelope tests.

## Search Proof

- Search Runtime for EF/DbContext references.
- Search Dispatcher for branch/recovery/domain decision logic.
- Search for old dispatcher symbols.

## Stop And Report Conditions

- Stop if Dispatcher starts deciding branch/recovery/artifact validity.
- Stop if Runtime requires EF implementation types.
- Stop if state can change without transition validation.

## Do Not Do

- Do not recreate old dispatcher.
- Do not let Dispatcher mutate runtime state directly.
- Do not call agents/workflows/Git/UI from Runtime.

## Acceptance Checklist

- [x] Runtime state machine tests pass.
- [x] Claim lifecycle tests pass.
- [x] Idempotency tests pass.
- [x] Event port tests pass.
- [x] Dependency scans pass.

## Proof Required

- Test output.
- Dependency scan.
- Old-symbol scan.
- Runtime integrity review.

## Proof Recorded

- `bundle://proof/SB07/manifest.md`
- `bundle://proof/SB07/semantic-invariants.md`
- `bundle://proof/SB07/runtime-integrity-review.md`
- `bundle://proof/SB07/build-unit-sb07.txt`
- `bundle://proof/SB07/test-unit-sb07.txt`
- `bundle://proof/SB07/build-solution-sb07.txt`
- `bundle://proof/SB07/runtime-forbidden-dependency-scan.txt`
- `bundle://proof/SB07/dispatcher-domain-decision-scan.txt`
- `bundle://proof/SB07/old-symbol-scan.txt`
- `bundle://proof/SB07/performance-scan-summary.json`
- `bundle://proof/SB07/codeanalytics-snapshot-summary.txt`
- `bundle://proof/SB07/bundle-validator-prepared-sb07.txt`

## Browser Validation Logging

- Browser validation is not required because no UI behavior is implemented.

## Progression Gate

- Satisfied. SB08 may start after runtime ports and state machine tests pass.

## Suggested Agent Prompt

Execute SB07 from `codex/bundles/process-module-architecture-v3/subbundles/07-runtime-scheduler-dispatcher-events`. Implement runtime state and dispatcher claims with event ports. Keep domain recovery out of Dispatcher.

## Handoff Notes For Next Bundle

Record port contracts, fake persistence used in tests, event schemas, claim behavior, and known persistence requirements for SB08.
