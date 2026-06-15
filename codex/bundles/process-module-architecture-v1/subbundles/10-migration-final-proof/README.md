# SB10 Migration And Final Proof

## Status

Planned.

## Objective

Migrate templates, validate compatibility, complete final end-to-end tests, and remove temporary rewrite scaffolding.

## Covered Inputs

- REQ-035
- REQ-036
- REQ-050

## Prerequisites

- SB01 through SB09 complete.

## Exact Source References

- `bundle://requirements/01-normalized-requirements.md`
- `repo://Templates/Processes`
- `repo://tests`

## Deliverables

- Migrated template pack.
- Migration report.
- Compatibility report.
- Full test matrix.
- Final architecture compliance audit.
- Rewrite completion report.

## Dependency Impact

- Final closure.

## Validation Depth

- Critical final quality gate.

## Implementation Steps

1. Run template migrations for all templates.
2. Generate and review migration report.
3. Run unit, integration, component, and Playwright test suites.
4. Run architecture boundary audit.
5. Run red-team tests for driver/core leaks, loop budgets, stale live history, and artifact recovery.
6. Remove temporary scaffolding.
7. Produce final proof manifest.

## Scope Exceptions

No new architecture scope should be added here unless a previous phase is reopened.

## Do Not Do

- Do not waive failed migration records.
- Do not accept tests that only prove non-empty projections.
- Do not keep compatibility shims that hide old architecture.

## Acceptance Checklist

- All templates are migrated.
- No skipped migration waves.
- Live/history filters work.
- Artifact recovery and subprocess manager communication work.
- Git wrapper and Git UI flows are tested.
- Architecture boundary tests pass.

## Proof Required

- Full test transcripts.
- Browser screenshots.
- Migration manifest.
- Architecture audit.
- Final semantic adequacy review.
- `proof/SB10/manifest.md`.

## Browser Validation Logging

- Record final browser validation analytics for core Process UI flows.

## Progression Gate

- Bundle implementation complete only after this gate passes.

## Suggested Agent Prompt

Close the rewrite only with migration and end-to-end proof. Reopen earlier subbundles for any boundary, migration, runtime, or UI defect.
