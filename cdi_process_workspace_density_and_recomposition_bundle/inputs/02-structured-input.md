# Structured Input

## Core Objective

- Make the processes workspace fit better into limited vertical space and make the definition canvas readable by giving users explicit recomposition actions that persist clearer node positions.

## Hard Constraints

- Keep the change maintainable and strongly typed.
- Do not solve process-node overlap with page-local JavaScript heuristics alone; the requested calculations must happen on the C# side.
- Reuse or extend shared CanvasLib and BaseLib contracts where that meaningfully reduces future duplication.
- Keep the process definition persisted through the real product path, then prove the result against the managed SQLite profile at `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db`.
- Keep the change additive where possible; do not destabilize unrelated workbench surfaces for the sake of process-only layout rules.

## Source Artifacts

- `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\inputs\00-original-request.md`
- `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\inputs\01-source-artifacts.md`
- `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\inputs\03-inline-screenshot-reference.md`
- `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle`
- `C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureSubtreeRecompositionEngine.cs`

## Input Coverage Signals

- `N001` When the user unzooms a little, the canvas surface must use the available width instead of leaving obvious dead space.
- `N002` `SummaryTile` needs a badge-style mode where label and value stay on one row and the value is visually smaller to save height.
- `N003` Process canvas nodes must stop overlapping; the result must be readable.
- `N004` There must be three recomposition commands: `Collisions`, `Add Space Around`, and smarter `Recomposition`.
- `N005` Those recomposition actions must sit under one toolbar icon that opens a hover dropdown.
- `N006` The recomposition design should share common parts with project structure and be modular for wider CanvasLib reuse.
- `N007` Recomposition calculations must happen in C# and may use parallelism.
- `N008` The implementation must be exercised against the managed SQLite workspace, not only fake test data.

## Dependency And Sequencing Signals

- The width-fit and tile-density work is a UI foundation that should land before browser proof for the final workspace.
- The shared recomposition contract must stabilize before process-specific toolbar and persistence work starts.
- The managed SQLite application step depends on both the shared engine and the process integration being complete and trustworthy.
- Final browser and database closure is meaningless until persisted recomposition can be observed in the real workspace.

## Validation Expectations

- Focused component tests for `SummaryTile` and process workspace density behaviors.
- Focused shared tests for recomposition math, collision removal, and command routing.
- A browser pass on `/processes` at large desktop width plus a narrower-height follow-up to confirm density gains and no dead-width regression.
- Managed SQLite proof showing persisted process-node coordinates changed through the application path and produced a visibly clearer canvas.

## UI Validation Strategy

- Run one maximized large-screen pass on `/processes` to inspect the summary row, left list, canvas toolbar, and canvas viewport occupancy.
- Run one constrained-height follow-up pass to confirm the badge-style summary tiles and tighter vertical chrome still read cleanly.
- Open a real process definition with overlapping nodes, execute the three recomposition actions as applicable, and capture before and after screenshots.
- Review screenshots for: dead whitespace on slight unzoom, summary tiles wasting height, collision removal success, spacing behavior, fishbone readability, and dropdown usability.

## Browser Validation Analytics

- `subbundles/01` must log `/processes` desktop and constrained-height density checks plus screenshots of summary tiles and viewport occupancy.
- `subbundles/03` must log `/processes` canvas toolbar interactions, recomposition command execution, and before and after screenshots.
- `subbundles/04` must consolidate the final route, viewport, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- The required recomposition experience is mandatory on the process definition canvas shown in the screenshot; runtime-canvas parity is optional unless the shared toolbar contract makes it effectively free and safe.
- The managed SQLite profile can be opened through the existing product workspace so persistence can be proven without raw SQL writes.
- The existing project-structure recomposition engine is a reference implementation, not a mandate to copy its radial layout into processes.
- The current transient JavaScript collision nudging in CanvasLib may remain as a render-time safety net, but canonical recomposition must become C#-driven and persisted.

## Primary Risks

- Over-expanding scope by trying to fully generalize domain-specific layout semantics into CanvasLib on the first pass.
- Breaking existing canvas interaction chrome while introducing a hover dropdown into the toolbar.
- Producing nondeterministic or unstable recomposition results that make persisted layouts jump between runs.
- Mutating the managed SQLite workspace through an unsafe shortcut instead of the product path and therefore proving the wrong thing.
