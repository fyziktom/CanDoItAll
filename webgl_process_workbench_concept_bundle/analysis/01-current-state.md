# Current state

## What already exists in the repository

- A rich typed-canvas workbench wrapper in `CanDoItAll.Components.CanvasLib`.
- A large JS runtime for the current workbench, already exposing semantic automation hooks.
- A process module that projects authoring semantics into the current 2D workbench.
- A template pack with multiple real process scenarios.
- An existing sandbox pattern for interactive components.
- Component and Playwright tests that already assert process/canvas behavior.

## Strengths to preserve

- Typed contracts between Blazor and JS.
- Stable process IDs and connection categories.
- Real template-backed source data.
- Semantic browser-test strategy rather than raw pointer-only automation.
- Clear separation between generic canvas infrastructure and process-specific projection.

## Constraints to respect

- Current canvas/runtime hotspots are already large, so the concept should avoid hidden production rewrites.
- Interactive-server style interop makes per-frame server-bound interaction a bad idea.
- Dense diagrams need readable labels more than they need dramatic camera freedom.
- The concept must be testable even when direct WebGL pointer automation is awkward.

## Immediate architectural implication

The concept should add a **new** library and a **new** sandbox instead of trying to morph the production workbench first.
