# Structured Input

## Core Objective

- Deliver real branching-node behavior on the process canvas so the user can add a branch from the canvas, see it as its own node, route explicit branch outputs and decision-role inputs through readable curves, author new connections by left-clicking visible connector circles, and trust that moved nodes and authored links persist correctly.

## Hard Constraints

- Preserve the literal user scope around `must`, `each`, `default`, and `error`.
- Do not remove or behaviorally regress the current legacy workbench node model.
- Keep shared canvas contract work in CanvasLib and process-specific mapping in the processes module.
- Use real Playwright validation and screenshot review for UI-facing closure.
- Do not fake many-to-many joins or persisted layout by keeping them only in transient browser state.

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
- `N011` Start connection authoring with left click on the small connector circle and confirm it with left click on a specific target circle.
- `N012` Position each connector circle exactly on the badge that explains the corresponding input or output, and do not miss required circles such as the `Review lead` router badge.
- `N013` Support many-to-many routing semantics where one input point can accept multiple upstream outputs and one output can fan out to multiple downstream inputs when the process requires that join or aggregation behavior.
- `N014` Persist moved node positions and other canvas edits correctly so role, router, and other derived nodes do not snap back after editor interactions, double-click, or reload.
- `N015` Repair the bundle and its subbundles before implementing the latest follow-up scope.

## Dependency And Sequencing Signals

- Scenario definition, canonical-gap logging, and persistence-risk logging must happen before more shared-canvas implementation.
- The additive shared workbench contract must cover left-click authoring and exact badge-anchor geometry before process-specific branch-node authoring can work cleanly.
- Process workspace authoring and persistence behavior must be proven before seeded scenarios and final browser closure can be trusted.

## Validation Expectations

- Run the bundle readiness validator before code implementation begins.
- Validate critical shared-canvas changes with tests and at least one dependent process-workspace browser smoke.
- Validate user-visible branching behavior in a headed browser with large-screen screenshots and narrower-width follow-up.
- Validate many-to-many or join-style input semantics in both canonical data and the rendered canvas, not only by drawing multiple curves.
- Validate persisted node movement by moving nodes, reopening an editor or rerender-triggering interaction, and confirming the positions stay stable.

## UI Validation Strategy

- Use `/processes` as the primary browser-validation route.
- Start with a large-screen pass at `1600x900`, then use `1280x800` for a narrower-width check when layout density changes.
- Review screenshots for readability, clipping, curve overlap, port spacing, exact badge-circle alignment, and visual coherence with the existing app.

## Browser Validation Analytics

- Each UI subbundle must record route, viewport, Playwright actions, screenshot paths, and result in `reviews/01-execution-report.md`.
- Final closure must include screenshot-backed answers for whether the branch node is visually separate, ports are readable, circles sit on the intended badges, left-click authoring works end to end, and moved nodes remain in place after rerender-triggering interactions.

## Working Assumptions

- The current process definition and runtime models may be insufficient for true many-to-many joins, and this must be verified instead of assumed away.
- The missing capability is no longer only in the shared canvas contract; canonical connection and layout persistence may also require process-module changes.
- The existing `/processes` route is stable enough to act as the primary validation surface for this work.

## Primary Risks

- The current workbench contract may still be too simple for stable per-port geometry unless badge-aligned anchor placement is extended additively.
- The current process model may not support many-to-many joins or aggregated inputs without a stronger canonical dependency representation.
- Derived-node movement may currently live only in transient UI state, which would make browser-only proof misleading.
- Browser proof will be weak if screenshots are captured but not actually reviewed for badge alignment, readability, and rerender stability.
