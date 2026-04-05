# 06 - Follow-up Readability And Selection Hardening

## Status

- `Blocked`

## Objective

- Improve the remaining large-graph usability debt exposed by fresh validation: readable fit-to-view behavior for dense subproject maps, and a clearer selection-to-recompose flow when routes open without an active selection.

## Covered Inputs

- `RQ-14`

## Prerequisites

- `05-fresh-sqlite-canonical-bundle-backfill-and-pm-validation` must be completed or honestly blocked before this phase starts.

## Exact Source References

- `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\fresh-validation\fresh-validation-contact-sheet.png`
- `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\fresh-validation\acr-001-fit-1600.png`
- `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\fresh-validation\acr-012-fit-1600.png`
- `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\fresh-validation\acr-015-fit-1600.png`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\`

## Deliverables

- a concrete fix or UX affordance for routes where `Fit canvas` reaches a readable geometry but text becomes too small for one-screen review
- a clearer root-selection fallback so recomposition is not silently disabled on load
- rerun screenshots proving the improved readability or interaction affordance

## Dependency Impact

- no additional phases are planned after this follow-up inside the current bundle

## Validation Depth

- live browser proof on the previously densest routes plus any targeted code validation needed by the chosen repair

## Implementation Steps

1. Reproduce the densest fresh-validation subproject views and identify whether the readability issue belongs to layout, node density, fit-to-view heuristics, or missing overview/detail affordances.
2. Reproduce the disabled-`Recompose` state on load and identify the clearest selection fallback that does not hide failures.
3. Implement the smallest correct repair or affordance.
4. Rerun Playwright MCP on the densest routes and compare before and after screenshots.

## Do Not Do

- Do not hide dense graphs behind ever-lower fit zoom and call that readable.
- Do not silently auto-mutate large graphs if the actual problem is missing overview/detail affordances.
- Do not make recomposition appear available when there is still no deterministic selection target.

## Acceptance Checklist

- users can tell how to reach a recomposable state without trial-and-error
- at least the densest subproject maps no longer depend on `15%` to `18%` fit zoom for one-screen review, or an explicit overview/detail affordance is shipped
- the follow-up closes with fresh browser proof

## Proof Required

- code or interaction repair
- Playwright MCP rerun on the previously densest subproject routes

## Browser Validation Logging

- target routes: the densest fresh-validation subprojects, starting with `ACR-001`, `ACR-012`, and `ACR-015`
- required viewport pass: maximized `1600x1200` with fit-to-canvas and any new overview/detail affordance open
- expected screenshot location: `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\fresh-validation\follow-up-06\`

## Progression Gate

- this follow-up remains blocked until the current bundle closure is accepted and a new execution turn starts specifically for the readability and selection hardening work.

## Suggested Agent Prompt

```text
Harden the remaining readability and selection debt from the fresh canonical-bundle validation run. Make dense subproject maps easier to review on one screen, and remove the ambiguity around why Recompose can be disabled on route load.
```
