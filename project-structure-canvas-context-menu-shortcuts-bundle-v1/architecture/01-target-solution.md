# Target Solution

## Shortcut Contract

- Add an explicit accelerator field to the shared `CanvasWorkbenchAction` model so the shortcut system lives in action metadata rather than in hard-coded JavaScript conditionals.
- Build the final accelerator map in the project-structure catalog or action-adapter layer before the runtime receives the action tree.
- Preserve the architect-requested letters as explicit assignments and use a deterministic fallback helper for the rest of each sibling set.

## Assignment Strategy

- Fixed shortcuts win when the request named a letter.
- For other siblings in the same visible layer, choose the first unused alphabetic character from the rendered label or menu label.
- Keep obvious numeric presets numeric for progress and priority menus rather than forcing alphabetic remapping.
- Treat collision detection as a testable contract, not an informal convention.

## Runtime Behavior

- When the context menu is open, route printable keys through the current layer first.
- If the matching action has children, open that submenu immediately and keep the menu open for the next key.
- If the matching action is a leaf, execute it and close or update the menu the same way a pointer click would.
- Keep `Escape` behavior, existing pointer hover behavior, and editable-field protection intact.

## Visual And Accessibility Behavior

- Render a visible inline affordance that underscores the active shortcut letter inside the menu label when a textual label exists.
- Surface the effective shortcut in the action's accessible name or descriptive label so screen-reader users are not left with pointer-only hints.
- Keep the menu layout readable after the new affordance lands, especially on compact and nested menu variants.

## Help Modal Information Architecture

- Replace the current flat shortcut card with a small browsable help surface such as tabs or page pills.
- Minimum content structure:
  - `Basics` or equivalent overview page
  - `Menu shortcuts` page that explains the right-click keyboard flow and key map
  - `Selection and global shortcuts` page that preserves the existing canvas-global guidance
- Prefer deriving the documented menu shortcut content from the same action contract or centralized mapping that drives the runtime so the help text cannot drift silently.

## Maintainability Boundary

- Do not widen the refactor into a general workbench rewrite.
- Extract only the shortcut-heavy helpers that currently make `03-interaction-and-state.js` harder to maintain, such as accelerator parsing, label-highlighting helpers, or layer-local key-routing helpers.
- If a new runtime module is introduced, preserve the existing workbench boot order through the asset manifest or shared asset registration instead of renumbering unrelated modules blindly.
