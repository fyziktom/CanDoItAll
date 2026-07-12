# {{SUBBUNDLE_TITLE}}

## Status

- `Ready`

## Objective

- Describe the outcome of this subbundle.

## Success Criteria

- List the observable conditions that make this subbundle done.

## Covered Inputs

- List the requirements, notes, or findings that this subbundle owns.

## Prerequisites

- List earlier subbundles, fixtures, proof, or bundle state required before implementation starts.
- Use `- none` only when this subbundle is truly independent.

## Exact Source References

- Add absolute paths to the relevant files.

## Deliverables

- List the concrete implementation results.

## Dependency Impact

- Describe the later subbundles, surfaces, or regression areas that depend on this phase, and why weak proof here would invalidate them.

## Validation Depth

- State the exact validation depth or closure type for this phase, for example `Critical foundation`, `Critical UI foundation`, `UI, component-test, and browser-proof`, `End-to-end regression and closure`, or `Process-critical closure`.

## Implementation Steps

1. Add the exact ordered steps.

## Scope Exceptions

- Add explicit exceptions when any raw note cannot be fully closed in this phase.

## Do Not Do

- List the boundaries for this phase.

## Acceptance Checklist

- Add observable validation points.

## Proof Required

- List the commands, screenshots, artifact paths, or DOM checks required to prove completion.
- If this subbundle changes UI, require a maximized large-screen browser pass, screenshot review, and narrower-width follow-up when layout is affected.

## Browser Validation Logging

- Record the target route or window under test.
- Record the required viewport passes.
- Record the Playwright MCP actions or assertions that must happen before the subbundle can close.
- Record the screenshot file names or evidence paths that should appear in the execution report.
- Record the screenshot review questions or visual findings that must be answered before the next dependent subbundle may start.
- Use `N/A` only when this subbundle does not affect browser-visible or host-visible proof.

## Progression Gate

- State the exact proof or condition that must be true before downstream subbundles may continue.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
