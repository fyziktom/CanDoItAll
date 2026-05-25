# SB10 - Generic red-team validation suite

## Status

Ready.

## Objective

Add red-team scenarios proving the runtime is generic and resilient.

## Covered Inputs

RQ14, N003, N005, N006

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs`

## Scope

Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

Critical semantic validation required.

## Implementation Steps

1. Add Blazor architecture step red-team: architecture step must not implement app.
2. Add business plan process: artifact destination writes allowed, product mutation irrelevant.
3. Add legal approval process: review can reject/no-go without runtime proof.
4. Add manufacturing inspection process: evidence checklist and inspection log must validate without software assumptions.
5. Add research process: dataset/report artifacts validate without product mutation.
6. Add workflow-backed role scenario.
7. Add subprocess stale child projection scenario.
8. Add upstream materialization/unblock scenario.
9. Run focused integration tests and solution build.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Tests cover software and non-software processes.
- [ ] Tests fail on prompt-only fixes.
- [ ] Tests prove both no over-blocking and no over-permission.
- [ ] Final build passes.

## Proof Required

Create/update:

- `proof/SB10/manifest.md`
- `proof/SB10/semantic-invariants.md`
- `proof/SB10/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB10/transcripts/passing.txt`
- `proof/SB10/transcripts/source-assertions.txt`
- `proof/SB10/transcripts/anti-stub-audit.txt`
- `proof/SB10/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB10 - Generic red-team validation suite` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.
