# Target Solution

Describe the intended end state and important boundaries.
# Target Solution

## Design Intent

- Fix the requested behaviors by extending the existing project-structure and CanvasLib architecture instead of layering a second styling or mutation system on top.
- Keep node presets strongly typed and centrally resolved so new block presets, block mutation, note conversion, and rendered canvas appearance all share one contract.
- Keep page code focused on orchestration while moving reusable transformation logic into services or adapters that already own the relevant boundary.

## Unified Visual Preset Architecture

- Extend the node visual profile contract so a node carries all canvas-facing preset data needed by the adapter, including semantic palette identity and Tailwind-backed style tokens.
- Collapse the current split between `ProjectWorkbenchService.ResolveVisualProfile` and `ProjectStructureGraphAdapter.ResolvePalette` into one source of truth.
- Define preset resolution by object type and subtype in one modular registry or resolver so newly added block kinds inherit the same behavior as existing ones.
- Keep raw class strings or style tokens inside the preset definitions, not scattered through Razor markup or JavaScript runtime code.

## Catalog And Mutation Model

- Keep standard block creation in `ProjectStructureCanvasCatalog` and `ProjectStructureCanvasCatalog.RichDefinitions`.
- Add new common block presets there, then reuse the same definitions to power block-type change flows where possible.
- Treat note-to-block conversion as a specialized typed mutation that maps note text into block title and retained body fields rather than inventing a separate one-off conversion path.

## Clipboard And Transfer Workflows

- Expand the CanvasLib clipboard event contract to support recursive subtree snapshots and explicit cut actions.
- Keep keyboard detection and raw clipboard envelopes in CanvasLib, but let the project-structure page own persisted subtree duplication, cut removal, and paste placement.
- Reuse subtree recomposition logic for layout consistency instead of inventing a second subtree placement algorithm.
- Implement subtree-to-subproject transfer as an explicit project-structure workflow with strong preconditions and predictable refresh behavior.

## Validation Strategy

- Add or update component tests for preset resolution, node mutation mapping, and page-level mutation side effects.
- Add or update integration tests where subtree movement or project-link behavior crosses service boundaries.
- Add Playwright coverage that proves the rendered canvas state, keyboard shortcuts, action affordances, and screenshots required by the user’s mandate.
