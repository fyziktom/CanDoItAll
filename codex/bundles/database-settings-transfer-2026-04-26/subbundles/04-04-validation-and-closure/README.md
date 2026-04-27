# 04-validation-and-closure

## Status

- `Completed`

## Objective

- Validate the completed implementation, update proof, close raw notes, and run final bundle checks.

## Covered Inputs

- All raw request lines.

## Prerequisites

- `01-01-transfer-foundation`, `02-02-workspace-transfer-handlers`, and `03-03-database-management-ui` closure gates must pass or have explicit blockers.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\codex\bundles\database-settings-transfer-2026-04-26\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\database-settings-transfer-2026-04-26\traceability\01-requirement-traceability.md`

## Deliverables

- Clean build/test proof or documented blockers.
- Browser validation analytics.
- Raw note closure table updated.
- Final bundle status synchronized.

## Dependency Impact

- This phase decides whether the original request is actually complete.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run build/tests.
2. Run browser validation for UI.
3. Update execution report with commands, artifacts, gate results, analytics, and raw note closure.
4. Run bundle final closure validator and repair any documentation mismatch.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not mark closure complete if UI proof is missing without a recorded blocker.

## Acceptance Checklist

- Every raw note is `Solved` or has an explicit non-solved status.
- Execution report matches actual commands and browser proof.
- Bundle validator passes for completed stage or documented blocker remains.

## Proof Required

- Build/test command output summary.
- Browser screenshot/evidence summary.
- `validate_bundle.py --stage completed` result.

## Browser Validation Logging

- Review and finalize rows created by subbundle 03.

## Progression Gate

- Bundle closes only when final proof is complete and status files match reality.

## Suggested Agent Prompt

```text
Run final validation, update the bundle execution report, and close every raw note against actual proof.
```
