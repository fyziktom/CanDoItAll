# Structured Input

## Core Objective

- Deliver real branching-node behavior on the process canvas so the user can add a branch from the canvas, see it as its own node, and route explicit branch outputs and decision-role inputs through readable curves.

## Hard Constraints

- Preserve the literal user scope around `must`, `each`, `default`, and `error`.
- Do not remove or behaviorally regress the current legacy workbench node model.
- Keep shared canvas contract work in CanvasLib and process-specific mapping in the processes module.
- Use real Playwright validation and screenshot review for UI-facing closure.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- `inputs/03-inline-screenshot-reference.md`
- `C:\repositories\CanDoItAll\cdi_process_management_audit_bundle`

## Input Coverage Signals

- `N001` Create a new branch node immediately when the user adds a branch from the canvas context menu.
- `N002` Represent branching as its own node, not hidden metadata on the source step.
- `N003` Expose one curve per matched output plus one default and one error route.
- `N004` Allow those curves to connect to downstream nodes or later parts of the process.
- `N005` Allow the decision-maker branch to receive an input curve from a role-definition node.
- `N006` Keep existing node types and existing single-anchor behavior unchanged; add an optional advanced type.
- `N007` Use the supplied screenshot as the visual reference for multi-port branch-style nodes.
- `N008` Prepare and execute a detailed CanDoItAll bundle with real Playwright and screenshot validation.
- `N009` Add proper examples of branching around software development, especially review, repair, QA, and approval loops.
- `N010` Record architecture troubles and missing foundations first instead of treating the renderer fix as sufficient.

## Dependency And Sequencing Signals

- Scenario definition and architecture trouble logging must happen before shared-canvas implementation.
- The additive shared workbench contract must land before process-specific branch-node authoring can work cleanly.
- Process workspace authoring must be proven before seeded scenarios and final browser closure can be trusted.

## Validation Expectations

- Run the bundle readiness validator before code implementation begins.
- Validate critical shared-canvas changes with tests and at least one dependent process-workspace browser smoke.
- Validate user-visible branching behavior in a headed browser with large-screen screenshots and narrower-width follow-up.

## UI Validation Strategy

- Use `/processes` as the primary browser-validation route.
- Start with a large-screen pass at `1600x900`, then use `1280x800` for a narrower-width check when layout density changes.
- Review screenshots for readability, clipping, curve overlap, port spacing, and visual coherence with the existing app.

## Browser Validation Analytics

- Each UI subbundle must record route, viewport, Playwright actions, screenshot paths, and result in `reviews/01-execution-report.md`.
- Final closure must include screenshot-backed answers for whether the branch node is visually separate, ports are readable, and loops remain understandable.

## Working Assumptions

- The existing process definition and runtime models already support enough branching semantics to avoid a domain rewrite.
- The missing capability is primarily in the shared canvas contract and the processes-to-canvas adapter.
- The existing `/processes` route is stable enough to act as the primary validation surface for this work.

## Primary Risks

- The current workbench contract may be too simple for stable per-port geometry unless it is extended additively.
- Default and error paths may reveal missing process semantics if they cannot be derived cleanly from current models.
- Browser proof will be weak if screenshots are captured but not actually reviewed for readability and line density.
