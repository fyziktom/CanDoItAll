# Validation And Acceptance Plan

This file defines how bundle 1 must be validated.

## Acceptance Gate 1: Full-Width Canvas

Pass condition:

- the structure page no longer renders a dedicated right inspector column at desktop widths
- the canvas visibly expands to occupy the recovered width

Evidence:

- before and after screenshots at desktop width
- code diff showing the `Inspector` slot removed from the structure page stage usage

## Acceptance Gate 2: Toolbar Always Reachable

Pass condition:

- toolbar remains reachable without moving any default panel
- no default floating panel overlaps the toolbar
- dragged panels cannot be placed into the toolbar safe zone

Evidence:

- screenshot with default layout
- screenshot after dragging windows around
- interaction recording or Playwright capture showing toolbar remains usable

## Acceptance Gate 3: Window Behavior Parity

Pass condition:

- selection panel and health window can drag
- selection panel and health window can resize
- selection panel and health window can minimize
- selection panel and health window can normalize
- selection panel and health window can hide and show

Evidence:

- manual or Playwright step log for each behavior
- persisted state confirmed after reload

## Acceptance Gate 4: Functional Parity

Pass condition:

- empty selection state works
- single selection preserves current actions
- multi-selection preserves current actions
- attachment preview still works
- create-next-to-source still works
- connect mode still works

Evidence:

- node-state validation checklist completed
- focused component or integration tests where practical

## Acceptance Gate 5: Multi-Select Common Actions

Pass condition:

- multi-selection panel exposes shared status, progress, marker, priority, and grouping actions where valid

Evidence:

- screenshots of multi-select panel
- action result verification on selected nodes

## Acceptance Gate 6: Density Improvement

Pass condition:

- the panel uses less dead space than the current version
- the most common node states fit with less scrolling at common desktop widths
- primary actions remain immediately visible

Review questions:

- does the panel still waste vertical space?
- are there large blank areas between sections?
- are button labels longer than necessary?
- are repeated headings or descriptions still present without adding value?

Evidence:

- before and after screenshots for at least empty, single, multi-select, and file-node states

## Acceptance Gate 7: File And Media Actions

Pass condition:

- image, video, and document preview still work
- audio preview exists
- local-open action is available only when the bridge supports it
- unsupported file types degrade cleanly

Evidence:

- per-file-type validation notes
- success and fallback screenshots

## Acceptance Gate 8: Accessibility And Keyboard Safety

Pass condition:

- focus order remains usable
- hidden or minimized windows are still recoverable
- window controls have accessible labels
- panel drag handles do not trap keyboard-only users

Evidence:

- manual keyboard walkthrough
- accessibility spot check

## Suggested Validation Stack

- watch-run validation against the running app at `https://localhost:7271`
- focused component tests for extracted panel rendering logic
- focused Playwright validation for toolbar access, drag, resize, minimize, normalize, hide/show, and file/media actions

## Expected Evidence Output

Implementation should write evidence back into:

- `artifacts/before-after-screenshot-index.md`
- `artifacts/panel-behavior-validation.md`
- `artifacts/file-media-validation.md`
- `artifacts/watch-run-notes.md`
