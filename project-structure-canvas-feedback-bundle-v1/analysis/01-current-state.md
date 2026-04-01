# Current State

Document the relevant repo state and affected files.
# Current State

## Canvas Node Model And Palette Split

- `ProjectObjectVisualProfile` currently carries shape, accent color, icon, and badge, but not a unified palette or Tailwind token contract.
- `ProjectWorkbenchService` resolves accent colors by object type and subtype, including existing semantic colors for several file nodes such as PDF and Excel.
- `ProjectStructureGraphAdapter` separately maps canvas palette keys, so rendered color behavior is split between node visual profiles and adapter-only palette logic.
- This duplication already diverges from user intent because deployment and infrastructure colors do not consistently follow the common topic colors the user described.

## Catalog And Mutation Baseline

- `ProjectStructureCanvasCatalog` and `ProjectStructureCanvasCatalog.RichDefinitions` already define the standard block catalog and typed create actions.
- The catalog already includes several typed file and infrastructure presets, which is the correct extension point for adding common computer, router, and WiFi block presets.
- There is no existing general-purpose block type change workflow for common blocks, so mutation currently happens mostly through create and edit flows.

## Inline Note Editing Baseline

- The inline note editor is currently a single-line input created in the CanvasLib runtime composer.
- The current key handling commits on `Enter`, which blocks multiline input and does not satisfy the requested `Shift+Enter` behavior.
- `HandleNodeEditedAsync` in the workbench page currently treats note edits specially and derives the note title from the edited note content.

## Clipboard And Keyboard Baseline

- CanvasLib exposes copy, paste, and duplicate hooks, but the payload is shallow and only describes selected nodes rather than full descendant structure.
- The keyboard runtime currently handles copy, paste, and duplicate shortcuts but not cut.
- The project-structure page does not yet own a full subtree-aware clipboard persistence flow, so current runtime hooks are not enough to satisfy descendant-inclusive cut and paste.

## Hierarchy And Subproject Baseline

- Existing hierarchy flows support adding or reconnecting subprojects.
- `ProjectStructureSubtreeRecompositionEngine` already handles subtree layout and re-placement logic, which is useful groundwork for subtree transfer.
- There is no dedicated flow today that moves all descendants of a selected node into a subproject while preserving structure and project links.

## Validation Baseline

- Component tests already cover graph adapter palette mapping and note mutation patching.
- Integration coverage already exists for subtree recomposition placement.
- Playwright smoke tests already exercise the project structure route, note creation and editing, typed PDF and Excel blocks, and parts of the toolbox and selection window behavior.
- The current test suite is a good base, but none of the nine requested feedback items are fully proven yet.
