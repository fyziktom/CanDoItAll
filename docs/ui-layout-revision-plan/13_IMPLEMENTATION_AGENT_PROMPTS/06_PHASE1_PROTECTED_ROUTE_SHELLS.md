# Prompt: Phase 1 Protected Route Shells

You are applying shell improvements around the protected workbench routes only.

## Scope

- Project Structure route framing
- Prompt Factory route framing
- any shell logic required to support `FocusWorkbench`

## Read First

- `../03_PHASE1_PROTECTED_AREAS.md`
- `../08_LAYOUT_PATTERNS_AND_ASCII_SKETCHES.md`
- `../10_IMPLEMENTATION_STRATEGY.md`

## Goals

1. Give protected routes a quieter shell mode.
2. Reduce duplicate route introduction.
3. Remove or collapse the global right rail.
4. Preserve current inner workbench behavior exactly.

## Forbidden Changes

- no `CanvasWorkbench` logic changes
- no prompt-node inspector behavior changes
- no project-structure selection/create/link behavior changes
- no JS/CSS contract changes for the workbench engine

## Expected Outputs

- protected routes render with more usable width
- surrounding chrome is reduced
- internal workbench tests and selectors remain valid

## Self-Check Before Finishing

- Did you only change shell framing?
- Do the protected routes still load with the same core content and interactions?
- Would the Playwright workbench tests still have the same behavioral expectations?

