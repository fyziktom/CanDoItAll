# Normalized Requirements

## R1 Viewport Width Usage

- When the process definition canvas is slightly unzoomed, the rendered working area must use the available width inside its host instead of leaving obvious avoidable dead space.

## R2 Badge-Style Summary Tile

- `SummaryTile` must gain an opt-in mode that renders like a compact badge:
  - label and value stay on one row
  - the value is visually smaller than the default tile value
  - existing callers stay unchanged unless they opt in

## R3 Processes Workspace Height Efficiency

- The processes workspace must apply the new badge-style summary tile mode and any related small density gains needed to save height without making the page harder to scan.

## R4 Shared Recomposition Command Set

- The canvas recomposition feature set must expose three distinct commands:
  - `Collisions`
  - `Add Space Around`
  - `Recomposition`

## R5 Shared Toolbar Menu Contract

- Those commands must sit under one common toolbar control with a proprietary icon and a hover-revealed dropdown, while remaining usable through normal click and focus interaction.

## R6 Shared C# Layout Computation

- The recomposition calculations that determine canonical node movement must happen on the C# side and not depend on JavaScript-only layout ownership.

## R7 Shared-First Modularity

- The collision-removal, spacing, and menu primitives should be modular enough to reuse across CanvasLib-backed workbenches instead of being embedded directly into the processes page.

## R8 Process-Specific Smart Recomposition

- The smarter `Recomposition` command for process definitions must account for the structure of process maps:
  - a readable main line from start toward completion
  - visible branches
  - roles and auxiliary nodes that do not collide with the main line
  - a result that reads more like a fishbone-style process map than a random explosion

## R9 Persisted Process Result

- Recomposition results must persist through the existing process-definition workflow so reopening the definition keeps the improved layout.

## R10 Managed SQLite Proof

- The implementation must be applied and proven against the managed SQLite workspace located at `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db`.

## R11 Proof Depth

- Closure requires focused automated tests, browser screenshots, and database verification strong enough to show:
  - saved height in the workspace
  - improved width usage on slight unzoom
  - distinct recomposition behaviors
  - persisted clearer process-node placement
