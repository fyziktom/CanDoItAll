# SB08 - Final merge readiness

## Status

Completed with classified broad-suite caveats.

## Objective

Close validation caveats and prepare branch for merge.

## Covered inputs

- Current reports still mention broad integration/component caveats.
- User asked whether everything is OK.

## Deliverables

1. Full solution restore/build.
2. Full unit tests.
3. Full integration tests with correctly configured PostgreSQL credentials.
4. Full component tests.
5. Focused process DB tests.
6. EF pending model changes check.
7. SQLite/runtime residue audit.
8. Query-plan and benchmark artifact review.
9. Merge readiness report.

## Implementation steps

- Fix PostgreSQL test environment before running broad integration.
- If broad tests still fail, classify each failure:
  - caused by this branch,
  - pre-existing,
  - environment-only,
  - legitimate quarantined test.
- Produce final report with no hidden caveats.

## Do not do

- Do not claim ready if broad tests fail without classification.
- Do not rely only on focused tests.
- Do not ignore EF warnings if they are new.

## Acceptance checklist

- [x] Build passes.
- [x] Full unit tests pass.
- [x] Full integration tests pass or failures are classified with evidence.
- [x] Full component tests pass or failures are classified with evidence.
- [x] No active SQLite provider residue.
- [x] Process DB red-team tests pass.
- [x] Numeric benchmark included.
- [x] Final report says clearly whether merge is safe.

## Execution result

Process DB hardening is merge-ready. The broader repository is not all-green:

- Full integration: 922 passed, 3 failed before local default PostgreSQL role repair; the same 3 failures were rerun after creating the missing local `postgres` role and now fail on pre-existing runtime-switching test assumptions in untouched files.
- Component tests: main bUnit component suite has pre-existing/out-of-scope failures and hang behavior in untouched project/project-structure tests. The independent MCP component suite passed.

See `bundle://proof/SB08/final-execution-report.md` for evidence and classification.

## Proof required

- `proof/SB08/manifest.md`
- `proof/SB08/final-execution-report.md`
- `proof/SB08/full-build.log`
- `proof/SB08/full-unit-tests.log`
- `proof/SB08/full-integration-tests.log`
- `proof/SB08/full-component-tests.log`
- `proof/SB08/ef-pending-model-changes.log`
- `proof/SB08/runtime-residue-audit.log`

## Browser validation logging

Required only for Data Sources pending restart UI if impacted by the changes.

## Progression gate

This is the merge gate.

## Suggested agent prompt

Execute SB08 after SB02-SB07. Close all validation caveats and produce a final merge readiness report with no hidden assumptions.
