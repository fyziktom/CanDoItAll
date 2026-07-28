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

## UI Composition Contract

- Use `N/A` only when this subbundle has no browser-visible UI.
- Primary surface and supporting-content placement:
- Stats treatment and reason:
- List/editor organization, including dialog, tab, inline, or split decision:
- Textarea sizing and dialog size rationale:
- First-viewport target and intended scroll owner:
- Read `candoitall-components-mcp/references/compact-ui-composition.md` instead of copying its detailed heuristics here.

## Deliverables

- List the concrete implementation results.

## Dependency Impact

- Describe the later subbundles, surfaces, or regression areas that depend on this phase, and why weak proof here would invalidate them.

## Validation Depth

- Proof tier: `Standard`, `Behavioral`, or `Governed`.
- State whether this is a critical foundation and name the affected validation surface.

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
- For CanDoItAll application UI, require a maximized or named large-screen desktop pass and screenshot review. Do not add narrower-width proof unless explicitly requested.
- For reusable basic `CanDoItAll.Components.BaseLib` work, require small, medium, and large viewport proof.
- Require normal-state and relevant open-overlay screenshots, including recorded first-viewport and scroll-owner findings.

## Browser Validation Logging

- Record the target route or window under test.
- Record the target desktop viewport. Add small/medium viewports only for reusable basic BaseLib or explicit scope.
- Record the Playwright MCP actions or assertions that must happen before the subbundle can close.
- Record the screenshot file names or evidence paths that should appear in the execution report.
- Record the screenshot review questions or visual findings that must be answered before the next dependent subbundle may start, including open dialogs, menus, dropdowns, tooltips, or floating surfaces.
- Use `N/A` only when this subbundle does not affect browser-visible or host-visible proof.

## Progression Gate

- State the exact proof or condition that must be true before downstream subbundles may continue.

## Reopen Triggers

- State which later findings invalidate this subbundle and which downstream work must be rechecked.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
