# SB04 - Workflow/subprocess artifact adapters and parent versioning

## Status

Ready.

## Objective

Add explicit workflow/subprocess artifact adapters and source-run versioning.

## Covered Inputs

RQ06, RQ07

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

Critical semantic validation required.

## Implementation Steps

1. Implement a workflow process artifact projection adapter that runs before finalizer validation for workflow-backed steps.
2. Map workflow run outputs to `ProcessArtifactRecord` with workflow run id, node/output id, content hash, expectation id, and producer kind.
3. Require current workflow run id in finalizer validation for workflow artifacts.
4. Add subprocess parent projection metadata for source child run id and source artifact id.
5. When child run changes, supersede or replace stale parent projections for the same expectation.
6. Ensure source-less subprocess gaps do not satisfy artifacts and do not route incorrectly.
7. Add tests for current child run acceptance, stale child run rejection, and workflow output projection.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Workflow completion with valid outputs satisfies process artifacts.
- [ ] Workflow completion without mapped outputs blocks or routes according to policy.
- [ ] Subprocess parent artifacts reference the current child run.
- [ ] Stale parent projection from an older child run is rejected.

## Proof Required

Create/update:

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- `proof/SB04/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB04/transcripts/passing.txt`
- `proof/SB04/transcripts/source-assertions.txt`
- `proof/SB04/transcripts/anti-stub-audit.txt`
- `proof/SB04/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB04 - Workflow/subprocess artifact adapters and parent versioning` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.
