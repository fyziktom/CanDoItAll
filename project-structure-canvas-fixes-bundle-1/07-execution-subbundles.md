# Execution Sub-Bundles

This file turns bundle 1 into atomic implementation-sized work packages with explicit acceptance gates.

Status:

- ready for implementation on March 25, 2026
- prepared against the current running app session at `https://localhost:7271`
- scoped to hot-reload-friendly nearby edits instead of a single large refactor

## Sub-Bundle 1: Canvas Shell And Toolbar

Goal:

- remove the dedicated right inspector column and make the toolbar behave like the true top frame of the full-width canvas

Files:

- `subbundles/01-canvas-shell-and-toolbar-checklist.md`

Acceptance gate:

- structure canvas is full width and the toolbar remains reachable at all times

## Sub-Bundle 2: Shared Floating Window Host

Goal:

- extract a reusable in-canvas window shell for drag, resize, minimize, normalize, hide, and show behavior

Files:

- `subbundles/02-shared-floating-window-host-checklist.md`

Acceptance gate:

- selection and health windows can move and resize without covering the toolbar safe zone

## Sub-Bundle 3: Selection Panel Parity And Density

Goal:

- move the current inspector behavior into the canvas while preserving functions and adding the missing multi-select actions

Files:

- `subbundles/03-selection-panel-parity-and-density-checklist.md`

Acceptance gate:

- all current inspector functions remain available in-canvas and the panel is visibly denser than the current layout

## Sub-Bundle 4: File, Media, And Local Open

Goal:

- complete the node file/media action model and add a safe local-open path

Files:

- `subbundles/04-file-media-and-local-open-checklist.md`

Acceptance gate:

- preview behavior works by file type and supported files can open locally without falling back to browser download semantics

## Sub-Bundle 5: Validation And Regression

Goal:

- verify the new canvas-first behavior through repeatable checks and evidence capture

Files:

- `subbundles/05-validation-and-regression-checklist.md`

Acceptance gate:

- evidence for toolbar safety, panel behavior, density improvement, and file/media actions is written into the bundle artifacts
