# SB06 — Process dispatch claim-token canonicality

## Status

Completed.

## Objective

Make durable dispatch claim ownership a hard mutation precondition.

## Covered Inputs

- User requested review of latest `db-remove-sqlite` branch.
- User requested another DB bottleneck review after SQLite removal.
- User requested canonicality protection while unlocking PostgreSQL performance.

## Prerequisites

See dependency map in `plan/01-phase-plan.md`.

## Exact Source References

- `repo://src/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs`
- `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
- `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`
- `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`

## Deliverables

- Change `RenewStepDispatchClaimAsync` so renewal failure returns a result and causes the worker to stop mutation work.
- Before artifact projection and final transition, verify the dispatch claim is still held and unexpired.
- Add claim token to transition/projecting helper calls where needed, or add explicit `VerifyDispatchClaimAsync` gates.
- Ensure failure transition also respects claim ownership unless the step is still unclaimed and safe to mark failed.
- Remove or shrink `StepDispatchGuards`; remove dictionary entries after release.
- Add stale worker tests: worker A claim expires, worker B claims, worker A attempts completion and must fail/no-op.

## Dependency Impact

This subbundle affects downstream canonicality and throughput proof. If it fails, later subbundles must not claim merge readiness.

## Validation Depth

Critical semantic validation is required. Do not rely only on build success.

## Implementation Steps

1. Change `RenewStepDispatchClaimAsync` so renewal failure returns a result and causes the worker to stop mutation work.
2. Before artifact projection and final transition, verify the dispatch claim is still held and unexpired.
3. Add claim token to transition/projecting helper calls where needed, or add explicit `VerifyDispatchClaimAsync` gates.
4. Ensure failure transition also respects claim ownership unless the step is still unclaimed and safe to mark failed.
5. Remove or shrink `StepDispatchGuards`; remove dictionary entries after release.
6. Add stale worker tests: worker A claim expires, worker B claims, worker A attempts completion and must fail/no-op.

## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not reintroduce SQLite support.

## Do Not Do

- Do not hide validation failures.
- Do not weaken canonical runtime DB semantics.
- Do not replace durable DB ownership with only in-memory locks.
- Do not add comments in non-English inside source code.

## Acceptance Checklist

- [ ] A stale or stolen dispatch claim cannot commit artifacts, branch outcomes, completion, or failure.
- [ ] Claim renewal failure is not just a warning.
- [ ] Process-local semaphore is only a local fast-path and not the canonical lock.
- [ ] Tests prove negative stale-claim behavior.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- stale claim negative tests
- parallel dispatch tests

## Browser Validation Logging

N/A unless UI-visible behavior is changed.

## Progression Gate

All acceptance checklist items must pass, and `proof/SB06/manifest.md` must contain changed-file hashes, command transcripts, source assertions, and residual risk notes.

## Suggested Agent Prompt

Execute SB06 only. Read this README, implement the scoped changes, run the required validation, write the proof manifest, and stop before downstream work unless the progression gate passes.
