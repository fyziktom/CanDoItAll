# SB02 - Tool policy boundary enforcement and metadata no-autopromotion

## Status

Ready.

## Objective

Make tool policy enforce process boundaries for external targets and managed output product paths, and prevent prompt alias auto-promotion.

## Covered Inputs

RQ02, RQ03, RQ04, N005, N006

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

Critical semantic validation required.

## Implementation Steps

1. Audit the production tool policy path that consumes allowed/read-only external target aliases.
2. Make prompt alias grounding aware of process boundary metadata.
3. Do not let `GroundPromptExternalTargetAliases` promote read-only process aliases to writable aliases for trusted governed process runs.
4. Add managed path classification: managed artifact/evidence path vs managed output product path.
5. Permit artifact/evidence writes on non-mutating steps but deny product-like writes to external or managed output product paths.
6. Add tests for external-target product write denial and managed output source write denial on architecture/review steps.
7. Add positive tests that architecture/review steps can still write required managed process artifacts.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Read-only external target aliases remain read-only even if the prompt contains them.
- [ ] Non-mutating steps cannot write `external-target/.../src/...` or managed output product files.
- [ ] Non-mutating steps can write required managed artifacts under the current-run artifact root.
- [ ] Product mutation steps can mutate explicitly allowed targets.

## Proof Required

Create/update:

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- `proof/SB02/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB02/transcripts/passing.txt`
- `proof/SB02/transcripts/source-assertions.txt`
- `proof/SB02/transcripts/anti-stub-audit.txt`
- `proof/SB02/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB02 - Tool policy boundary enforcement and metadata no-autopromotion` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.
