# SB08 - No-progress retry and active run adoption hardening

## Status

Completed.

- Subbundle status: Completed

## Objective

Strengthen no-progress retry detection and avoid finalizing active non-terminal executions.

## Covered Inputs

- RQ11, RQ12

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

- Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

- This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

- Critical semantic validation required.

## Implementation Steps

1. Create a no-progress fingerprint using missing tool names, failed tool names, artifact expectation ids, attempted paths, content hashes, and validation statuses.
2. Record no-progress attempts in journal or execution diagnostics.
3. Compress retries when the same invalid artifact or same failed validation repeats without a newly satisfied requirement.
4. Do not treat writing any evidence as progress if the same expectations remain unsatisfied.
5. Change concurrent active execution adoption to adopt only terminal runs or observe active runs with bounded polling.
6. Add tests for repeated malformed artifact attempts, repeated wrong-root write, and active running execution not finalized.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Repeated bad evidence compresses before burning all attempts.
- [ ] Legitimate new progress still allows retry.
- [ ] Active non-terminal execution does not produce a final process transition.

## Proof Required

Create/update:

- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`
- `proof/SB08/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB08/transcripts/passing.txt`
- `proof/SB08/transcripts/source-assertions.txt`
- `proof/SB08/transcripts/anti-stub-audit.txt`
- `proof/SB08/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB08 - No-progress retry and active run adoption hardening` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.

