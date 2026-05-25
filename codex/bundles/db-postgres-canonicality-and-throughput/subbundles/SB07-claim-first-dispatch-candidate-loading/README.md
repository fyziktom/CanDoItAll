# SB07 — Claim-first dispatch candidate loading

## Status

Completed with elapsed-time telemetry and no query-count capture.

## Objective

Reduce DB load and race window in process dispatch candidate selection.

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

- Refactor candidate selection into two phases: minimal eligible step discovery/claim, then detailed hydration for the claimed step.
- Do not load all run artifacts, all step runs, all role requirements, and execution-run history before a durable claim unless required for eligibility.
- Batch or precompute execution-run blocking checks instead of per-step N+1 calls.
- Preserve dependency, branch, subprocess, workflow, and artifact input semantics.
- Add telemetry around candidate-load query count and elapsed time.
- Add tests for parallel independent steps, branch-dependent steps, and missing artifact materialization.

## Dependency Impact

This subbundle affects downstream canonicality and throughput proof. If it fails, later subbundles must not claim merge readiness.

## Validation Depth

Critical semantic validation is required. Do not rely only on build success.

## Implementation Steps

1. Refactor candidate selection into two phases: minimal eligible step discovery/claim, then detailed hydration for the claimed step.
2. Do not load all run artifacts, all step runs, all role requirements, and execution-run history before a durable claim unless required for eligibility.
3. Batch or precompute execution-run blocking checks instead of per-step N+1 calls.
4. Preserve dependency, branch, subprocess, workflow, and artifact input semantics.
5. Add telemetry around candidate-load query count and elapsed time.
6. Add tests for parallel independent steps, branch-dependent steps, and missing artifact materialization.

## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not reintroduce SQLite support.

## Do Not Do

- Do not hide validation failures.
- Do not weaken canonical runtime DB semantics.
- Do not replace durable DB ownership with only in-memory locks.
- Do not add comments in non-English inside source code.

## Acceptance Checklist

- [ ] Claim happens before heavy hydration where semantically safe.
- [ ] Independent ready steps can be claimed by parallel workers without double execution.
- [ ] Branch/dependency semantics remain correct.
- [ ] Query count or elapsed-time evidence improves or is at least instrumented.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- query/telemetry evidence
- integration tests

## Browser Validation Logging

N/A unless UI-visible behavior is changed.

## Progression Gate

All acceptance checklist items must pass, and `proof/SB07/manifest.md` must contain changed-file hashes, command transcripts, source assertions, and residual risk notes.

## Suggested Agent Prompt

Execute SB07 only. Read this README, implement the scoped changes, run the required validation, write the proof manifest, and stop before downstream work unless the progression gate passes.
