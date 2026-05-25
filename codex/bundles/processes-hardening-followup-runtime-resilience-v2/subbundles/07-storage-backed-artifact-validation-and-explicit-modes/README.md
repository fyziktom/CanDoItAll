# SB07 - Storage-backed artifact validation and explicit modes

## Status

Completed.

- Subbundle status: Completed

## Objective

Make artifact validation storage-backed, explicit-mode friendly, and less brittle for generic processes.

## Covered Inputs

- RQ10

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

- Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

- This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

- Critical semantic validation required.

## Implementation Steps

1. Resolve relative managed artifact paths through the actual storage/workspace service.
2. Parse JSON content for relative managed `.json` paths when JSON is required.
3. Add optional explicit artifact mode metadata to expectations or validation summaries.
4. Keep heuristic mode detection as fallback only.
5. Add tests for malformed relative JSON rejection and valid relative JSON acceptance.
6. Add non-software tests: legal decision log, manufacturing inspection checklist, finance cash-flow report, research dataset summary.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Malformed relative managed JSON does not satisfy a JSON artifact contract.
- [ ] Valid relative managed JSON passes.
- [ ] Generic business/legal/research/manufacturing artifacts are not incorrectly forced into runtime proof mode.
- [ ] Explicit artifact mode overrides heuristic detection.

## Proof Required

Create/update:

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- `proof/SB07/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB07/transcripts/passing.txt`
- `proof/SB07/transcripts/source-assertions.txt`
- `proof/SB07/transcripts/anti-stub-audit.txt`
- `proof/SB07/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB07 - Storage-backed artifact validation and explicit modes` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.

