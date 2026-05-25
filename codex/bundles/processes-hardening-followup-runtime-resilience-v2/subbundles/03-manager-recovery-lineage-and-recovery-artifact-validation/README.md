# SB03 - Manager recovery lineage and recovery artifact validation

## Status

Ready.

## Objective

Fix recovery lineage so manager recovery artifacts validate against the recovery execution that produced them and the original execution they recover for.

## Covered Inputs

RQ05

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

Critical semantic validation required.

## Implementation Steps

1. Extend recovery outcome handling so post-recovery validation receives the recovery execution detail or recovery execution run id.
2. Add typed provenance fields or structured journal payload linking RecoveryExecutionRunId, RecoveredForExecutionRunId, RecoveryDecisionId, and ReworkPacketId.
3. Ensure ProjectExecutionArtifacts for manager recovery records recovery producer kind or recover-for lineage.
4. Update finalizer validation to accept recovery artifacts with valid recovery lineage even when they do not contain the original execution run id.
5. Add a regression test where direct agent misses artifact, manager recovery writes it, and finalizer accepts it.
6. Add negative test where unrelated later execution artifact is rejected.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Recovered artifact is accepted when produced by the manager recovery execution and tied to the original step.
- [ ] Recovered artifact is rejected when no recover-for lineage exists.
- [ ] Recovery does not rely on shared mutable `DispatchCandidate` sets as the source of truth.

## Proof Required

Create/update:

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- `proof/SB03/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB03/transcripts/passing.txt`
- `proof/SB03/transcripts/source-assertions.txt`
- `proof/SB03/transcripts/anti-stub-audit.txt`
- `proof/SB03/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB03 - Manager recovery lineage and recovery artifact validation` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.
