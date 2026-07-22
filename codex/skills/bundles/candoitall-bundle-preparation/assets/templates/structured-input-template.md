# Structured Input

## Core Objective

- State the primary outcome without implementation detail.

## Success Criteria

- List the observable conditions that prove the outcome is done.

## Hard Constraints

- List the non-negotiables.

## Allowed Side Effects

- State what may be changed. Use `none beyond documented subbundles` when scope is tight.

## Source Artifacts

- Reference the files, docs, screenshots, or prompts that define the task.

## Input Coverage Signals

- List each raw note or artifact that cannot be safely collapsed, merged, or deferred.

## Dependency And Sequencing Signals

- Note which requested outcomes obviously unlock or block later work.

## Validation Expectations

- Describe the proof required before implementation is complete.

## Evidence Contract

- List the commands, screenshots, browser checks, host checks, or artifacts that must exist before closure.
- Assign `Standard`, `Behavioral`, or `Governed` proof per subbundle.

## UI Validation Strategy

- For CanDoItAll application UI, note the large-screen desktop Playwright pass and screenshot review questions; omit small/medium work unless explicitly requested.
- For reusable basic `CanDoItAll.Components.BaseLib` work, include small, medium, and large viewport proof.
- For each UI outcome, record the primary surface, supporting-content placement, stats treatment, list/editor dialog-or-tab decision, textarea and dialog sizing rationale, first-viewport target, and intended scroll owner.
- Route detailed choices through `candoitall-components-mcp/references/compact-ui-composition.md` rather than restating its guidance.

## Browser Validation Analytics

- If the task is UI-related, note how each subbundle will log route, viewport, scroll owner, normal and open-overlay states, Playwright MCP actions, assertions, screenshot paths, visual findings, and result.

## Working Assumptions

- Record the assumptions made during bundle preparation.

## Primary Risks

- Record the main delivery, UI, architecture, or regression risks.
