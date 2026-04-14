# Browser proof playbook for `/processes`

## Minimum routes and surfaces

- `/processes`
- definitions/steps authoring surface
- runtime runs surface
- template dialog if touched
- any new smaller workspace components introduced by decomposition

## Required viewport order

1. Large desktop first, for example `1600x900`
2. Narrower-width pass second, for example `430x932`

## Minimum authoring actions

- Load an existing process definition.
- Open the steps/canvas area.
- Verify dependency and branch-related controls render coherently.
- If the phase changed authoring logic, perform the relevant authoring action and persist it.
- Verify no overlap, clipping, or unreadable content.

## Minimum runtime actions

- Open the runs surface.
- If runtime UI changed, start or inspect a run and perform the relevant action.
- Verify action gating and visible state are correct.
- Verify no visually broken layout under the tested viewport.

## Required screenshot questions

- Can all texts be read without zooming?
- Is anything clipped, overlapping, or visually colliding?
- Is spacing intentional?
- Is alignment consistent?
- Does the page use available space well?
- Do overlays and dialogs layer correctly?

## Recording rule

Record:
- route,
- viewport,
- actions,
- screenshots,
- answers to the review questions,
- final pass/fail result
in `reviews/01-execution-report.md`.
