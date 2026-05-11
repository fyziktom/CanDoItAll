# 11 Final Browser and Scenario Closure

## Status

- `Completed`

## Objective

Close the reopened bundle only after UI proof, API proof, PostgreSQL test-instance proof, scenario proof, provider attempts, and raw-note closure all agree.

## Covered Inputs

- All original inputs plus `inputs/03-follow-up-request.md` and `inputs/04-multistep-llm-transfer-request.md`.

## Prerequisites

- Subbundles `08`, `09`, and `10` are completed or explicitly blocked with precise evidence.

## Exact Source References

- `C:\repositories\CanDoItAll\.codex\bundles\workflow-executors-maf-tools\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\.codex\bundles\workflow-executors-maf-tools\reviews\02-architecture-closure.md`
- `C:\repositories\CanDoItAll\.codex\bundles\workflow-executors-maf-tools\requirements\02-input-coverage-matrix.md`
- `C:\repositories\CanDoItAll\.codex\bundles\workflow-executors-maf-tools\traceability\01-requirement-traceability.md`

## Deliverables

- Final execution report with browser screenshots, API proof, PostgreSQL/test-instance proof, scenario matrix, and raw-note closure.
- Updated architecture closure describing plugin-ready executor UI boundaries, persistence gaps, and any durable-workflow follow-up.
- Completed-stage bundle validator proof.

## Dependency Impact

- This is the final gate.
- Earlier subbundles must reopen when proof contradicts their closure.

## Validation Depth

- Full bundle validator.
- Manual closure audit.

## Implementation Steps

1. Reopen raw notes and map every follow-up sentence to shipped behavior or a blocker.
2. Run required build/tests/browser/API/scenario commands.
3. Update execution report and architecture closure.
4. Run prepared validator if bundle changed materially, then completed validator.

## Scope Exceptions

None beyond blockers already recorded in subbundles `08` through `10`.

## Do Not Do

- Do not mark the bundle complete with missing Playwright/browser proof for the canvas changes.
- Do not hide exact model/provider/database limitations as residual risk.

## Acceptance Checklist

- All reopened raw notes have `Solved`, `Partially solved`, or `Not solved`.
- Browser validation analytics include open-state proof for floating windows and modals.
- API/scenario/database proof rows are complete.
- Final validators pass or exact blockers are recorded.

## Proof Required

- Prepared-stage validator after bundle repair.
- Completed-stage validator after implementation.
- Build/test/browser/API/scenario proof.

## Browser Validation Logging

- Required final browser analytics rows in `reviews/01-execution-report.md`.

## Progression Gate

- Pass only when the bundle can be handed back without hidden UI, API, scenario, or database gaps.

## Suggested Agent Prompt

Implement subbundle 11 only. Audit every raw note, rerun validators, and close the bundle with proof that matches the implemented behavior.
