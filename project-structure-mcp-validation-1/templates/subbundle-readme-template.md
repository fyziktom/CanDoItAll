# {{SUBBUNDLE_TITLE}}

## Status

- `Ready`

## Objective

- Describe the outcome of this subbundle.

## Covered Inputs

- List the requirements, notes, or findings that this subbundle owns.

## Prerequisites

- List earlier subbundles, fixtures, or environment proof required before this phase can start.
- Use `- none` only when this subbundle is truly independent.

## Exact Source References

- Add absolute paths to the relevant files.

## Deliverables

- List the concrete implementation results.

## Dependency Impact

- List the later subbundles, surfaces, or regression areas that depend on this phase.

## Validation Depth

- Standard
- Critical foundation
- State the extra validation expected before dependent subbundles may continue.

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

- State the exact proof that must pass before the next dependent subbundle can start.

## Suggested Agent Prompt

```text
Implement this subbundle only.
```
