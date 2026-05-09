# Validation And Closure

## Status

- `Blocked`

## Objective

- Run final targeted validation, close every raw note, update bundle proof, and run completed-stage bundle validation.

## Covered Inputs

- All notes N001-N009.
- All requirements R001-R008.

## Prerequisites

- Subbundles 01 through 04 are completed or honestly blocked.
- Workbook path is recorded if subbundle 04 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_node_tooling_bundle\README.md`
- `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_node_tooling_bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_node_tooling_bundle\traceability\01-requirement-traceability.md`

## Deliverables

- Targeted test results recorded.
- Browser validation analytics or explicit validation gap recorded.
- Raw note closure rows updated.
- Completed-stage validator run and result recorded.

## Dependency Impact

- Final closure is the bundle-level release gate.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run targeted component and integration tests.
2. Run any build/test command needed to cover changed projects.
3. Verify workbook artifact exists.
4. Update subbundle statuses and execution report.
5. Run `validate_bundle.py --stage completed`.

## Scope Exceptions

- Any unimplemented workbook recommendation must remain a recommendation, not a solved raw note.

## Do Not Do

- Do not mark raw notes solved without proof.
- Do not leave subbundle statuses as `Ready`.

## Acceptance Checklist

- Every raw note row is `Solved`, `Partially solved`, or `Not solved`.
- Tests and workbook proof are recorded.
- Completed validator passes or blocker is explicit.

## Proof Required

- Targeted test commands and outcomes.
- Workbook artifact path.
- Completed-stage bundle validator output.

## Proof Captured

- Unit, component, and integration test commands are recorded in `reviews/01-execution-report.md`.
- Final closure is partial because the requested XLSX artifact is blocked by missing spreadsheet runtime support.

## Browser Validation Logging

- Record any browser checks captured by earlier subbundles or state the explicit test-only validation gap.

## Progression Gate

- Bundle can close only when code, tests, workbook, raw-note closure, and validator output agree.

## Suggested Agent Prompt

```text
Run final closure for this bundle. Reopen any weak subbundle instead of summarizing around missing proof. Update statuses, raw-note closure, browser analytics, residual risks, and validator output.
```
