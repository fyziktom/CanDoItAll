# {{SUBBUNDLE_TITLE}}

## Status

- `Ready`

## Objective

- State the observed defect or execution slice this subbundle owns.

## Covered Inputs

- Link the requirement ids and evidence that caused this subbundle to exist.

## Prerequisites

- State the cycle/stage evidence required before implementation starts.

## Exact Source References

- Add absolute paths to affected code, source data, evidence files, and tracker rows.

## Deliverables

- State the code, data, script, or analysis outputs required.

## Dependency Impact

- Explain which later memory cycle, review decision, or chat probe becomes untrustworthy if this work is weak.

## Validation Depth

- Use `Repair-critical`, `Critical execution foundation`, `Critical quality gate`, or `End-to-end rerun`.

## Implementation Steps

1. Reproduce the observed issue from evidence.
2. Make the smallest correct repair.
3. Run focused tests or API/browser proof.
4. Rerun the affected stage or chat probe.
5. Update `reviews/01-execution-report.md`.

## Scope Exceptions

- State any behavior not fixed by this repair.

## Do Not Do

- Do not mask the issue by changing sample data unless the sample data is wrong.
- Do not skip rerun proof.

## Acceptance Checklist

- Completed: Reproduction evidence exists.
- Completed: Repair is implemented or blocker is explicit.
- Completed: Affected cycle or chat probe was rerun.

## Proof Required

- Add commands, API outputs, screenshots, or transcript paths.

## Browser Validation Logging

- Use `N/A` only if the repair has no UI or chat-visible behavior.

## Progression Gate

- Downstream work may continue only after rerun proof is recorded.

## Suggested Agent Prompt

```text
Implement this repair subbundle only. Start from the observed evidence, make the smallest correct fix, rerun the affected memory cycle or chat probe, and update the execution report before returning to the parent bundle.
```
