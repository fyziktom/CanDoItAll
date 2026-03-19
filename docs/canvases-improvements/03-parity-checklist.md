# Parity Checklist

Use this as the completion checklist for the separate implementation agent.

## Shared foundation

- [ ] One shared canvas workbench component exists and is consumed by both target editors.
- [ ] One shared JS engine/interoperability layer exists and is not duplicated per module.
- [ ] One shared CSS token/component layer exists for the canvas workbench look.
- [ ] Shared DTOs normalize both project structure data and prompt wizard data into one canvas contract.
- [ ] Shared UI state model persists selection, collapse, zoom, pan, maximize, and manual positions.

## Shared stage layout

- [ ] Left canvas stage plus right inspector stage is used in both editors.
- [ ] Canvas stage header contains kicker, title, explanatory copy, and stat chips.
- [ ] The host uses large rounded corners, soft border, overflow clipping, and the same atmospheric background language as the reference.
- [ ] Lower supporting panels exist below the main stage where domain-specific tools need them.
- [ ] Mobile and tablet layouts stack without breaking the overlay chrome.

## Shared canvas chrome

- [ ] Top-left add launcher exists.
- [ ] Top-left focus action exists.
- [ ] Top-right fit button exists.
- [ ] Top-right maximize toggle exists.
- [ ] Top-right help button exists.
- [ ] Top-right zoom rail exists as one pill cluster.
- [ ] Zoom out button exists.
- [ ] Zoom slider exists.
- [ ] Zoom in button exists.
- [ ] Zoom percent readout exists.
- [ ] Bottom-left hint exists on desktop.
- [ ] In-canvas help overlay exists.
- [ ] Help overlay explains click, right-click, marquee, move, pan, and zoom.

## Shared interactions

- [ ] Click selects.
- [ ] Right-click opens node-aware actions.
- [ ] Empty-space drag pans.
- [ ] Middle-mouse drag pans.
- [ ] Wheel zoom anchors under the pointer.
- [ ] `Ctrl`/`Cmd` + drag moves node(s).
- [ ] `Alt` + drag starts marquee selection.
- [ ] Marquee selection supports multi-select.
- [ ] Multi-select state is visible in the inspector.
- [ ] Double-click collapses or expands collapsible groups.
- [ ] Domain-specific double-click open behavior still works where needed.
- [ ] Keyboard shortcuts `+`, `-`, `0`, `?`, `h`, and `Escape` work.
- [ ] Panning clamps reasonably to scene bounds.
- [ ] State survives refresh when node ids remain stable.

## Shared node system

- [ ] Root nodes use the dark high-emphasis package/session visual treatment.
- [ ] Group nodes use the section-style visual treatment.
- [ ] Item nodes use pastel type-specific cards.
- [ ] Connectors use the softer reference line style.
- [ ] Branch toggle controls exist for collapsible nodes.
- [ ] Selection styling matches the reference quality level.
- [ ] Metadata chips and footer chips are present where appropriate.
- [ ] Required/optional or equivalent status chips appear on item cards.
- [ ] Duration/state pills appear on the upper-right of relevant cards.
- [ ] Visual hierarchy is obvious without reading every label.

## Shared create and context actions

- [ ] A persistent quick-create launcher exists in the canvas chrome.
- [ ] The quick-create launcher visually matches the screenshot-observed hex style.
- [ ] A node-aware context action menu exists.
- [ ] Add-next-to-source placement is implemented so new nodes do not stack on top of the source node.
- [ ] Create actions are typed, not generic-only.
- [ ] Remove actions exist where allowed.
- [ ] Duplicate actions exist where allowed.
- [ ] Edit/focus actions exist where allowed.

## Shared inspector system

- [ ] Empty inspector state is polished and informative.
- [ ] Single-selection inspector has a header surface and body surface.
- [ ] Multi-selection inspector exists.
- [ ] Root inspector can use tabs where needed.
- [ ] Inspector actions do not look like raw default buttons.
- [ ] Inspector uses the same visual language as the canvas stage.
- [ ] Inspector actions remain usable without canvas gestures.
- [ ] Read-only preview inspector mode exists where the editor has a preview mode.

## Project structure editor

- [ ] `ProjectStructurePage` uses the shared stage layout and shared canvas chrome.
- [ ] The current basic `workbenchInterop.js` canvas is replaced or upgraded to the shared workbench engine.
- [ ] Project root is rendered as a proper root card.
- [ ] Phases and similar branch nodes use group-card styling.
- [ ] Leaf work objects use typed item-card styling.
- [ ] Existing domain actions still work: open, branch, validate, test, skip, mark used, and link.
- [ ] Linking behavior still works after the refactor.
- [ ] The inspector is upgraded from the current generic action card into a proper mirrored editor surface.
- [ ] Outline panel remains available as a lower or secondary supporting surface.
- [ ] Project-specific create actions are available from both the persistent launcher and node context actions.
- [ ] New object placement is adjacent to the source node instead of overlaying it.
- [ ] Cross-link rendering still works.
- [ ] View state persistence still works after the migration.

## Prompt wizard canvas editor

- [ ] `PromptFactoryPage` becomes a canvas-first flow editor.
- [ ] Top wizard steps remain, but the flow itself is no longer a plain stacked list.
- [ ] Prompt session is rendered as the root card.
- [ ] Branch groups are visually distinct from prompt steps.
- [ ] Prompt steps are rendered as item cards with state-aware chips and accents.
- [ ] Branching from a step remains supported.
- [ ] Opening linked prompt artifacts remains supported.
- [ ] The selected prompt node is edited from the right-side inspector.
- [ ] Lower supporting panels still cover generated prompt preview, session state, governance, and save/export/send actions.
- [ ] Prompt wizard uses the same overlay chrome, node language, and inspector language as project structure.
- [ ] Prompt wizard persists its own canvas UI state.

## Screenshot-driven visual details

- [ ] The host background uses the warm-left to cool-right gradient look.
- [ ] The zoom cluster is a purple pill rail.
- [ ] The top-left add affordance is visually dominant.
- [ ] The right inspector card matches the screenshot layout proportions.
- [ ] The package/root card is dark and visually anchors the scene.
- [ ] Pastel item cards match the soft reference palette style.
- [ ] The vertical quick-create rail is not omitted.
- [ ] Canvas-adjacent modals use the same rounded, soft, purple-accented language.

## User-requested feature parity

- [ ] Toolbar/chrome parity is present.
- [ ] Zoom and pan parity is present.
- [ ] Nice hexagonal menu parity is present.
- [ ] Modals inside the canvas workflow use the shared style.
- [ ] Drag and move of items is present.
- [ ] Proper line connections remain present.
- [ ] Adding new item next to source is present.
- [ ] Help modal inside canvas is present.
- [ ] Select tool for one or multiple items is present through marquee and inspector bulk mode.
- [ ] Shared visual look is consistent between project structure and prompt wizard.

## Testing and QA

- [ ] Unit or component tests cover shared component state round-tripping where practical.
- [ ] End-to-end tests cover canvas selection, zoom, pan, and primary actions.
- [ ] Project structure tests cover create, move, link, and open flows.
- [ ] Prompt wizard tests cover branch, select, focus, and artifact open flows.
- [ ] Visual smoke testing is done on desktop and mobile widths.
- [ ] Maximize mode is tested.
- [ ] Keyboard shortcuts are tested.
- [ ] Empty states, single selection, and multi-selection are all tested.
