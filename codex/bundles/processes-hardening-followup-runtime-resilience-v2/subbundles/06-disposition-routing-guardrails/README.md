# SB06 - Disposition routing guardrails

## Status

Ready.

## Objective

Prevent branch routing from masking missing artifacts on artifact-production steps while preserving review/approval disposition routing.

## Covered Inputs

RQ09

## Prerequisites

- Bundle prepared-stage validation completed.
- Earlier critical subbundles in `plan/01-phase-plan.md` are completed when this subbundle depends on them.
- Current repository branch is verified before edits.
- PostgreSQL remains the canonical runtime database.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Scope

Implement only the runtime, tests, and proof required for this subbundle. Keep process core generic.

## Dependency Impact

This subbundle affects downstream process runtime reliability. If this subbundle fails, later subbundles may pass from false assumptions.

## Validation Depth

Critical semantic validation required.

## Implementation Steps

1. Add a typed `ProcessDispositionPolicy` or equivalent decision helper.
2. Allow repair/no-go/escalation routing only for review, approval, QA, decision, or escalation steps.
3. Block or recover artifact-production steps when their own required artifact is missing.
4. Do not route missing upstream inputs to branch outcomes.
5. Require semantic compatibility between failure type and branch outcome.
6. Add positive test for QA review routing to repair branch.
7. Add negative test for architecture artifact step with missing ADR not routing to repair branch.

## Scope Exceptions

None planned. If implementation discovers a larger model change is required, repair this bundle before continuing.

## Do Not Do

- Do not add SQLite work.
- Do not hardcode Blazor/.NET/JavaScript as generic process semantics.
- Do not solve by prompt text alone.
- Do not accept source-assertion-only proof.
- Do not hide missing artifacts behind generic branch routing.

## Acceptance Checklist

- [ ] Review/approval steps can complete with repair/no-go branch when they have enough evidence.
- [ ] Artifact-producing steps cannot complete merely by selecting a negative branch when their own artifact is missing.
- [ ] Missing inputs stay blocked unless a modeled decision step owns the escalation.

## Proof Required

Create/update:

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- `proof/SB06/transcripts/failing-first.txt` or `red-team.txt`
- `proof/SB06/transcripts/passing.txt`
- `proof/SB06/transcripts/source-assertions.txt`
- `proof/SB06/transcripts/anti-stub-audit.txt`
- `proof/SB06/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible UI or runs browser proof red-team scenarios. If browser proof is run, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest and semantic invariant contract are complete.

## Suggested Agent Prompt

Implement `SB06 - Disposition routing guardrails` from `codex/bundles/processes-hardening-followup-runtime-resilience-v2`. Follow the exact source references, constraints, and proof requirements in this README. Keep the process core generic and validate with behavior tests, not only source assertions.
