# SB09 - Process definition lint integration and template quality gates

## Status

Ready.

## Objective

Integrate process definition lint into publish/start/readiness and improve templates.

## Covered Inputs

RQ13, N007

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService*.cs`
- `repo://src/CanDoItAll.Modules.Processes/Pages/**`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs`

## Scope

Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

Critical semantic validation required.

## Implementation Steps

1. Expose linter result through service/API/UI or publish/start validation surfaces.
2. Add strict mode for blocking errors and warning mode for advisory issues.
3. Add linter rules for missing explicit operation contract on ambiguous steps.
4. Add linter rules for artifact-producing steps with negative branch outcomes but no artifact recovery policy.
5. Add auto-fix suggestions for common process definition mistakes.
6. Update process templates to include explicit step boundaries and artifact modes.
7. Add component/API tests if UI/API surfaces change.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Operators can see lint issues before launching a process.
- [ ] Severe definition problems can block publish/start in strict mode.
- [ ] Existing templates are updated or flagged with clear migration warnings.

## Proof Required

Create/update:

- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`
- `proof/SB09/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB09/transcripts/passing.txt`
- `proof/SB09/transcripts/source-assertions.txt`
- `proof/SB09/transcripts/anti-stub-audit.txt`
- `proof/SB09/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB09 - Process definition lint integration and template quality gates` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.
