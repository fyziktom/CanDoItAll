# Validation Harness And Drift Guardrails

## Status

- `Completed`

## Objective

- Add a practical guardrail that catches future route/docs/skills drift before it becomes another manual audit.

## Success Criteria

- A focused script or test compares current route inventory against expected high-priority docs/skills/API coverage.
- The guardrail has a documented command and is cheap enough to run during API/docs/skills changes.
- The guardrail fails on representative missing high-priority route coverage and passes with current repaired state.

## Covered Inputs

- RQ-006 drift guardrails.
- GAP-015 no route/docs/skills parity guardrail.

## Prerequisites

- SB01 inventory reviewed.
- SB02 contract repairs complete.
- SB04 docs refresh complete.
- SB05 skills refresh and active sync complete.

## Exact Source References

- `repo://tests`
- `repo://tools`
- `repo://docs`
- `repo://codex/skills`
- `repo://src/CanDoItAll.Web/Api`
- `bundle://inventories/api-docs-skills-gap-map.xlsx`

## Deliverables

- New or updated parity validation script/test.
- Command documented in execution report and relevant docs if appropriate.
- Negative or representative failure proof recorded where practical.

## Dependency Impact

- SB07 final closure depends on this guardrail because it is the long-term protection against repeated drift.

## Validation Depth

- Guardrail and regression prevention.

## Implementation Steps

1. Choose the smallest guardrail that detects high-value route/docs/skills drift.
2. Reuse the workbook generator logic if practical, or factor shared route extraction cleanly.
3. Add expected coverage rules for high-risk surfaces.
4. Run the guardrail in passing state.
5. Capture a negative proof if feasible without leaving the repo dirty.
6. Record command output and any known limitations.

## Scope Exceptions

- Do not build a large docs generation framework unless a smaller script/test cannot enforce the needed guardrail.
- Do not require full semantic docs understanding from an automated check.

## Do Not Do

- Do not create brittle string checks for every prose sentence.
- Do not make the guardrail depend on local absolute paths.
- Do not skip documented limitations.

## Acceptance Checklist

- Guardrail command is documented.
- Passing proof exists.
- Failure mode is described or demonstrated.
- Guardrail covers the high-risk surfaces from the workbook.

## Proof Required

- Guardrail command and output.
- Updated tests/scripts path.
- Execution report limitations and residual risks.

## Browser Validation Logging

- `N/A`: validation harness work does not change UI.

## Progression Gate

- SB07 may begin only after the drift guardrail has passing proof or a concrete blocker is recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Add the smallest useful drift guardrail for API/docs/skills route coverage, run it, record proof and limitations, and stop if it cannot run reliably in this repo.
```
