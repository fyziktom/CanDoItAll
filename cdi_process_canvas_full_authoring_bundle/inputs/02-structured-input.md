# Structured Input

## Core Objective

- Expand the process canvas from branch-router-specialized authoring into a canvas-first editor for the full process graph, including step structure, participant-role connections, branch routing, and any other canonical process relationships that must be visible and editable from the canvas.

## Hard Constraints

- Keep the additive advanced-node approach; do not break legacy single-anchor canvas behavior.
- Start with full node and port analysis before implementation sequencing.
- Preserve literal many-to-many, single-to-many, and many-to-single semantics instead of hiding them behind generic links.
- Prefer strong typing and explicit semantics over stringly canvas heuristics.
- Be honest about canonical-model gaps instead of letting the canvas fake relationships that the database cannot persist.
- Treat the process canvas as the primary editing surface, not just a decorative projection of form-only data.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`

## Input Coverage Signals

- The word `all` applies to the remaining node families, not only one more special case.
- `most of them must have options like participants roles` means role participation ports cannot stay hidden in forms if the canvas is claimed to be primary.
- The explicit request for `many2many`, `single2many`, and `many2single` means the bundle must inventory connection cardinality per node family, not only per rendered line.
- The request to `Start with analysis` means the bundle must ship with a real port matrix and canonical-gap assessment before execution begins.

## Dependency And Sequencing Signals

- Shared node and port semantics must be fixed before persistence or UI work, otherwise downstream phases will code against drifting assumptions.
- Canonical persistence must be settled before UI authoring is expanded, otherwise canvas flows will look editable but snap back or vanish on save and reload.
- Shared rendering and gesture parity must land before node-specific authoring, otherwise browser proof will be invalid.
- Scenario seeding and runtime projection validation must come last because they depend on the preceding semantics, persistence, and UI behavior all being trustworthy.

## Validation Expectations

- Prepared-stage bundle validation must pass before implementation starts.
- Each implementation subbundle must include targeted tests and explicit progression gates.
- UI-bearing phases require real Playwright proof on `/processes`, large-screen screenshots, and screenshot review.
- Final closure must include seeded software-development scenarios that prove the canvas can author realistic review, QA, approval, and rework flows.

## UI Validation Strategy

- Run the first browser validation pass in a maximized large-screen Playwright session on `/processes`.
- Capture close-up screenshots for multi-port badge alignment and connection readability.
- Review screenshots against readability, alignment, overlap, layering, intentional space use, and connector-target affordance.
- Add narrower-width follow-up passes after the large-screen pass is stable whenever node layout, pill wrapping, or overlay placement changes.

## Browser Validation Analytics

- Each UI subbundle must record route, viewport, Playwright actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.
- Critical UI foundations must record one downstream smoke on the real process workspace before the next dependent subbundle may begin.
- Final closure must summarize whether the browser analytics are strong enough for the claim that the canvas is now the primary authoring surface.

## Working Assumptions

- `process-step`, `process-role`, and the branch-router runtime and definition variants are the relevant current node families exposed by the process module today.
- The current seeded software-development scenarios are rich enough to validate generalized canvas authoring once the canvas can edit those relationships.
- Role participation and dependency relationships are already canonical in storage; artifact-consumption links are not yet canonical and may require model extension.

## Primary Risks

- The process canvas may appear feature-complete visually while still failing to persist some relationships if the model extension is skipped.
- Node ports may drift away from step-kind semantics if a strongly-typed port catalog is not introduced first.
- Runtime projection may lag behind definition semantics and make the authored process harder to understand after execution starts.
- Weak scenario coverage would let a happy-path authoring demo pass while real rework and approval loops still break.
