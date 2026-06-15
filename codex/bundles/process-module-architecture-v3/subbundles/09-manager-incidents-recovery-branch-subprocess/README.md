# SB09 Manager, Incidents, Recovery, Branch/Switch, Loop Protection, And Subprocess Control

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Implement manager runtime, incident lifecycle, error preprocessing, recovery/resupply, typed branch decisions, loop budgets/fingerprints, subprocess manager messages, parent/child artifact projection requests, and escalation events.

## Why This Bundle Exists

Manager-driven recovery, branch routing, and subprocess coordination are central reliability risks. This bundle prevents the manager from becoming a hidden dispatcher and replaces text-token branch routing with typed contracts.

## Covered Inputs

- REQ-015 through REQ-025.
- REQ-042 through REQ-045.
- v3 manager loop and branch/switch contract.

## Context Reset: Read These First

- SB08 execution report.
- `architecture/07-artifact-error-recovery-and-subprocess-model.md`
- `architecture/13-branch-switch-and-loop-contract.md`
- `architecture/14-manager-runtime-and-control-loop.md`
- `architecture/12-runtime-persistence-event-store-and-outbox.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/07-artifact-error-recovery-and-subprocess-model.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/13-branch-switch-and-loop-contract.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/14-manager-runtime-and-control-loop.md`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessBranchOutcomeRouting.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasBranching.cs`

## Source Evidence To Use

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessBranchOutcomeRouting.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasBranching.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Automation/Recovery/AgentRecoveryModels.cs`
- SB01 recovery/branch/subprocess archive.

## Prerequisites

- SB08 complete.
- Runtime and persistence event/ledger stores working.

## In Scope

- Manager queue/control loop.
- Deterministic manager baseline.
- Agent-backed/hybrid manager adapter contract.
- Incident lifecycle.
- Error preprocessing and restricted diagnostic links.
- Recovery eligibility policy.
- Recovery request lifecycle and dispatcher handoff.
- Typed branch decision request/result.
- Route application through runtime.
- Loop budget/fingerprint ledger.
- Subprocess control messages.
- Parent/child artifact projection request handling.
- Escalation events and projections inputs.

## Out Of Scope

- No concrete agent adapter implementation beyond contract/fakes.
- No UI rendering.
- No template migration.
- No full concrete domain driver slice.

## Target Projects / Files

- `src/CanDoItAll.Processes.Runtime`
- `src/CanDoItAll.Processes.Application`
- tests for manager, branch, recovery, subprocess control.

## Deliverables

- Manager runtime/control loop.
- Incident/recovery/subprocess message contracts and handlers.
- Typed branch route execution.
- Loop protection implementation.
- Tests for safety and failure behavior.

## Expected Deliverables

- Manager decisions are events.
- Automatic recovery is policy/budget/idempotency/access checked.
- Branch outcomes are typed.
- Subprocess managers communicate through durable messages.

## Dependency Impact

- SB10 consumes manager/incident/branch events for projections.
- SB12 uses branch migration diagnostics and runtime history decisions.
- SB13 displays incidents and branch state.

## Validation Depth

- Validate with incident lifecycle tests, recovery policy tests, branch decision tests, loop escalation tests, subprocess message tests, raw diagnostic restriction tests, and manager safety review.

## Architecture Invariants That Must Hold

- Manager does not mutate runtime state directly.
- Manager does not execute domain work directly.
- Branch display text does not determine runtime routing.
- Raw diagnostics are restricted evidence.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Implement manager work queue and idempotency.
2. Implement incident lifecycle.
3. Implement error preprocessing strategy boundary.
4. Implement recovery eligibility and lifecycle.
5. Implement branch decision request/result and route application.
6. Implement loop budget/fingerprint ledger.
7. Implement subprocess control messages.
8. Add tests and search proof.

## Refactoring Review Checkpoint

- Split manager policy, queue, incident, recovery, branch, and subprocess message handlers.
- Verify manager is not a dispatcher replacement.
- Verify branch logic has no token matching.

## Required Tests / Proof

- Missing artifact recovery tests.
- Stale artifact incident tests.
- Branch decision idempotency tests.
- Backward branch loop escalation tests.
- Subprocess parent/child message tests.
- Raw diagnostic restriction tests.
- Manager policy denial tests.

## Search Proof

- Search for `ProcessBranchOutcomeRouting` outside archive/migration references.
- Search for free-text branch token routing.
- Search manager code for direct runtime state mutation.

## Stop And Report Conditions

- Stop if manager needs to call concrete agents/workflows directly.
- Stop if branch routing falls back to text tokens.
- Stop if recovery can repeat without budget/fingerprint checks.

## Do Not Do

- Do not let Manager become a hidden dispatcher.
- Do not route branches by free-text tokens.
- Do not mutate runtime state from manager strategies.
- Do not expose raw diagnostics in normal UI projections.

## Acceptance Checklist

- [ ] Manager loop implemented.
- [ ] Incident lifecycle tests pass.
- [ ] Recovery policy tests pass.
- [ ] Branch/loop tests pass.
- [ ] Subprocess message tests pass.

## Proof Required

- Test output.
- Manager safety review.
- Branch token-routing search proof.
- Old-symbol scan.

## Browser Validation Logging

- Browser validation is not required because UI behavior is not implemented.

## Progression Gate

- SB10 may start after manager/branch/subprocess events and projection inputs are stable.

## Suggested Agent Prompt

Execute SB09 from `codex/bundles/process-module-architecture-v3/subbundles/09-manager-incidents-recovery-branch-subprocess`. Implement manager safety, typed branches, recovery, and subprocess control without creating a hidden dispatcher.

## Handoff Notes For Next Bundle

Record event types, incident projection inputs, branch projection inputs, subprocess message schemas, and known UI needs for SB10.
