# Target Solution

## Path Presentation Model

- Path interpretation stays in C#, not in browser string parsing.
- `ProjectStructureNodeDescriptor` and `ProjectStructureGraphAdapter` should produce an additive typed presentation payload for path-backed nodes, including:
  - the full original path
  - a compact visible label
  - whether the path resolves to a file-like leaf
  - the promoted file name when applicable
- Shared canvas rendering should use that payload to draw a single compact path button with tooltip and transient copied-state feedback, while non-path nodes continue using the current generic lead-text flow.

## Non-Preview Double-Click Model

- Shared canvas JavaScript should continue raising the existing node-open callback instead of learning Workbench-specific node-type rules.
- `ProjectStructurePage` should become the decision point for non-preview double-click handling:
  - preview-capable nodes keep the current preview behavior
  - non-preview nodes open a centered quick-action modal hosted inside the existing page or canvas surface
- Quick-action choices should be resolved from existing page and service logic so button labels and command execution stay aligned with the current command catalog.
- The modal should remain narrow in scope: edit plus a single best secondary action, not a duplicate of the full inspector or context menu.

## Settings Surface Model

- `CanvasWorkbench.razor` should replace the literal `cfg` text with a proper settings icon using the project’s existing icon approach.
- Settings overlay placement should reserve the toolbar band and center the card inside the remaining usable stage area so its top edge never disappears behind the toolbar.
- The fix belongs in shared canvas chrome, not in page-specific CSS offsets.

## Proof Model

- Focused browser coverage must verify:
  - a long path-backed node renders a compact path control rather than dumping the full path on the card
  - clicking the compact path control copies the full path and shows the transient success state
  - double-clicking a non-preview node opens the quick-action modal with the expected action labels
  - the settings affordance shows iconography instead of `cfg`
  - the settings overlay stays below the toolbar on both wide and narrower layouts
- Targeted automated tests should cover the C# action-resolution and presentation mapping where feasible, while Playwright closes the layout and interaction-sensitive notes.
