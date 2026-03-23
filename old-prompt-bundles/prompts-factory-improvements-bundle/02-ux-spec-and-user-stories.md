# UX Specification And User Stories

## Proposed UX Model

Prompt Factory should be organized as page tabs:

### Tab 1: Canvas
- canvas
- floating in-canvas inspector
- persistent setup entry point inside the graph
- no lower support content

### Tab 2+: Stage workspaces
- setup
- governance
- assembly
- review

Only one page tab should be visible at a time.

## Core UX Changes

### A. Dismissible help system
- Replace sticky help behavior with dismissible popovers.
- Close on outside interaction.
- Close on mouse leave after a short grace delay.
- Keep the interaction fast and non-modal.

### B. Real page tabs
- The main Prompt Factory page should use a standard tab strip.
- The first tab is `Canvas`.
- Additional tabs expose `Setup`, `Governance`, `Assembly`, and `Review`.
- Switching away from `Canvas` hides both the canvas and the floating inspector.
- The active non-canvas tab becomes the full page workspace for that stage.

### C. Contextual inspector
- The inspector on the canvas tab should focus on the selected canvas item.
- Remove the large workflow duplication from the inspector.
- Keep item-level detail, status, and actions in the inspector.
- Move page-level forms and explorers into the matching page tabs.

### C2. Floating inspector behavior
- The inspector must render inside the canvas surface, not as a fixed column outside it.
- Default state is docked on the right edge of the canvas.
- The inspector can be dragged to another position inside the canvas.
- The inspector can be minimized into a compact restore handle.
- Maximized canvas mode must preserve the same floating inspector behavior.

### D. Canvas setup wizard
- Add a persistent `Session setup` node near the session root.
- The setup node is available on new sessions and remains editable later.
- Missing setup should be visually obvious but not blocking.
- Project facts should prefill the setup summary when available.

### E. Toolbox-style prompt-component picker
- Keep radial menu for generic actions.
- For prompt components, open a second-layer toolbox panel with:
- search
- section headers
- accordion groups
- list items
- hover preview
- click to add or configure

### F. Rich input attachments
- Treat file inputs as first-class prompt context.
- Differentiate visual type by file kind or extension.
- Ask the user what to extract, summarize, compare, or validate from each input.
- Keep the input presentation compact but legible in canvas and inspector.

### G. Bulk-action confirmation
- Confirm before actions that can add, replace, or clear many items.
- Explain impact with counts.
- Allow cancel.
- Preserve undo as a second safety layer, not the only one.

## User Stories

### Story 1: New user starts a blank prompt session
- User opens Prompt Factory with no project selected.
- User lands on the `Canvas` tab first.
- User sees canvas and inspector first.
- User is guided to open `Session setup`.
- User fills intent, language, app state, and repositories.
- User understands what to do next without scanning the full page.

### Story 2: Project-based session starts from known context
- User opens Prompt Factory from a project.
- Known values are prefilled from project metadata.
- Missing values are highlighted in the setup wizard.
- User confirms or completes the missing parts.

### Story 3: User wants to add a prompt component
- User opens the radial menu on a session or group node.
- User chooses `Components`.
- A toolbox-style panel opens with search and grouped sections.
- User hovers a candidate and sees a preview.
- User selects one item, not an unexpected batch.

### Story 4: User attaches evidence files
- User adds a PDF, spreadsheet, image, or note.
- The dialog asks what the AI should use it for.
- The attachment appears as a visually distinct node.
- The inspector shows both source identity and extraction intent.

### Story 5: User accidentally triggers a heavy recommendation action
- User clicks `Apply recommendations` or chooses a flow that changes many blocks.
- The system previews counts and asks for confirmation.
- User can cancel safely.

### Story 6: User needs deep work on the canvas
- User stays on the `Canvas` tab.
- Setup remains reachable from the setup node and inspector actions.
- Other stage work lives on separate tabs, so the canvas view stays visually clean.
- The floating inspector stays inside the maximized canvas, so the user does not lose the selected-node editor when switching to focused graph work.

## Expected UX Resolution By Area

### Orientation
- improved by real page tabs with canvas-first default
- improved by persistent setup node
- improved by contextual floating inspector instead of repeated stage work

### Discoverability
- improved by list-based component toolbox
- improved by hover previews
- improved by visible attachment type styling

### Safety
- improved by heavy-action confirmations
- improved by clear setup status
- improved by better dismissal behavior for transient help

### Efficiency
- improved by reduced scroll
- improved by search-first component picking
- improved by direct reopen of setup instead of hunting through page forms
- improved by keeping selected-node editing inside the canvas even in maximized mode
