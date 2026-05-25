# SB05 - Upstream materialization unblock and resume lifecycle

## Status

Ready.

## Objective

Complete the lifecycle from missing upstream artifact request to downstream unblock/resume.

## Covered Inputs

RQ08

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService*.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

Critical semantic validation required.

## Implementation Steps

1. Add event type `MissingUpstreamArtifactMaterializationResolved`.
2. When an upstream source step records the missing artifact or completes after materialization, find blocked downstream steps with matching request fingerprints.
3. Re-evaluate artifact inputs for those downstream steps.
4. Transition downstream step from Blocked to Ready or WaitingApproval only when dependencies and approvals allow it.
5. Keep the operation idempotent.
6. Add tests for downstream unblock after upstream artifact appears.
7. Add tests where downstream remains blocked when artifact still missing.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Downstream step does not remain blocked after upstream materialization succeeds.
- [ ] Duplicate materialization requests do not cause duplicate reruns.
- [ ] Unblock does not bypass approvals, dependency rules, or process run terminal state.

## Proof Required

Create/update:

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- `proof/SB05/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB05/transcripts/passing.txt`
- `proof/SB05/transcripts/source-assertions.txt`
- `proof/SB05/transcripts/anti-stub-audit.txt`
- `proof/SB05/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB05 - Upstream materialization unblock and resume lifecycle` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.
