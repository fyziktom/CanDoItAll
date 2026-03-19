# Reference And Gap Analysis

## Evidence legend

- `Code-confirmed`: verified in `zyphonote-web` or `CanDoItAll` source.
- `Screenshot-confirmed`: directly visible in the screenshot set.
- `Inference`: required interpretation to bridge screenshot evidence and implementation shape.

## What the reference system actually is

The reference is not just one `<canvas>` with some nodes.

It is a layered authoring workbench made of:

1. A page shell with wizard steps and surrounding layout.
2. A large rounded canvas host with internal overlay chrome.
3. A right-side inspector that mirrors the selected node's true editor.
4. Lower supporting surfaces such as structure tools, outline, score workspace, and advanced editors.
5. A reusable canvas engine with state persistence, node rendering, context actions, and viewport math.
6. Optional generic workbench chrome from `zy-canvas-workbench.js` for context menus, toolbars, ribbons, and docks.

That layered composition is the main parity target.

## Reference capability inventory

### Page shell and layout

| Capability | Evidence | Notes |
| --- | --- | --- |
| Step-based wizard shell above the canvas | Code-confirmed, Screenshot-confirmed | Learning builder uses a multi-step builder flow. Prompt wizard should keep its own step flow but adopt the same layout language. |
| Left canvas stage plus right inspector stage | Code-confirmed, Screenshot-confirmed | `.lp-builder-stage`, `.lp-stage-canvas`, `.lp-stage-inspector`. |
| Canvas stage header above the host | Code-confirmed, Screenshot-confirmed | Includes kicker, title, help copy, and stat chips. |
| Lower supporting panels below the main stage | Code-confirmed | Structure controls, score workspace, advanced JSON, preview content. |
| Read-only preview canvas mode | Code-confirmed | Same engine, different mode and inspector behavior. |

### Canvas host and chrome

| Capability | Evidence | Notes |
| --- | --- | --- |
| Large rounded host with soft border and overflow clipping | Code-confirmed, Screenshot-confirmed | `.lp-canvas-host`. |
| Warm-to-cool gradient background with subtle dot pattern | Code-confirmed, Screenshot-confirmed | Rendered directly in canvas. |
| Top-left canvas actions | Code-confirmed, Screenshot-confirmed | Add button and "Focus First" style action cluster. |
| Top-right canvas utility controls | Code-confirmed, Screenshot-confirmed | Fit, maximize, help, zoom out, slider, zoom in, zoom percent. |
| Bottom-left hint text | Code-confirmed | Changes by mode. |
| Help overlay inside the canvas | Code-confirmed, Screenshot-confirmed | Centered help card with gestures and shortcuts. |
| Maximize mode | Code-confirmed | Host becomes fixed overlay; body scroll locked. |

### Interaction contract

| Capability | Evidence | Notes |
| --- | --- | --- |
| Click to select | Code-confirmed | Primary selection drives inspector focus. |
| Alt-drag marquee selection | Code-confirmed | Bulk-selects item nodes. |
| Ctrl/Cmd-drag to move nodes | Code-confirmed | Dragging sections drags children; dragging one selected item drags all selected items. |
| Empty-space pan | Code-confirmed | Left background drag pans. |
| Middle mouse pan | Code-confirmed | Explicit support. |
| Wheel zoom anchored under pointer | Code-confirmed | Not center zoom. |
| Keyboard shortcuts | Code-confirmed | `+`, `-`, `0`, `?`, `h`, `Escape`. |
| Branch collapse/expand | Code-confirmed, Screenshot-confirmed | Package and section branch controls. |
| Double-click behavior | Code-confirmed | Toggle branch or open score. |
| Context menu actions | Code-confirmed | JS workbench context menu plus radial/hex style affordances in screenshots. |
| Persisted view and selection state | Code-confirmed | Selection, zoom, pan, collapse, maximize, manual positions. |

### Node system

| Capability | Evidence | Notes |
| --- | --- | --- |
| Package, section, and item families | Code-confirmed, Screenshot-confirmed | The visual hierarchy is strong and intentional. |
| Dark package card | Screenshot-confirmed | Root card has strongest contrast and prominence. |
| White section cards with accent rail | Code-confirmed, Screenshot-confirmed | Section cards are distinct from root and items. |
| Pastel item cards by type | Code-confirmed, Screenshot-confirmed | Score, text, checkpoint, image are color-coded. |
| Type pill, duration pill, requirement chip, footer chips | Code-confirmed, Screenshot-confirmed | These small metadata elements are visually important. |
| Soft connector lines | Code-confirmed, Screenshot-confirmed | Curved, low-contrast links. |
| Branch toggle bubbles | Code-confirmed, Screenshot-confirmed | Small green plus/minus circles. |
| Manual positions layered over semantic layout | Code-confirmed | Dragging affects offsets, not semantic order. |

### Create and context affordances

| Capability | Evidence | Notes |
| --- | --- | --- |
| Node-aware context actions | Code-confirmed | Different actions for package, section, and item. |
| Hexagonal radial context affordance | Screenshot-confirmed, Inference | Visible in screenshot set and required by user. |
| Vertical quick-create hex rail | Screenshot-confirmed | Visible in `Screenshot 2026-03-19 144204.png`. |
| Add-next-to-source behavior | Code-confirmed | Section insertion uses `after_section_key`; item insertion uses `after_item_key`. |
| Item type add actions beyond plain notes | Code-confirmed | Score, text, checkpoint, image are wired. |
| User explicitly expects note, image, video, etc. | User-confirmed | The plan must include user-requested broader item palette even where current reference wiring is narrower. |

### Inspector system

| Capability | Evidence | Notes |
| --- | --- | --- |
| Empty-state inspector | Code-confirmed, Screenshot-confirmed | Clear instructional placeholder. |
| Package inspector with tabs | Code-confirmed | Basics, Image, Structure, Scores. |
| Section inspector | Code-confirmed | Mirrored editor with block metadata and section item list. |
| Item inspector | Code-confirmed, Screenshot-confirmed | Mirrored editor with typed fields and action buttons. |
| Bulk-selection inspector | Code-confirmed | Duplicate, remove, keep draft branch. |
| Preview inspector | Code-confirmed | Read-only representation for preview mode. |
| Inspector reuses real source editors | Code-confirmed | Clones inline editor DOM into the inspector. |

### Modal patterns

| Capability | Evidence | Notes |
| --- | --- | --- |
| Rich white modal with step pills and close pill | Screenshot-confirmed | `Screenshot 2026-03-19 131635.png`. |
| Modals visually match the same design system as the canvas | Screenshot-confirmed, Inference | Rounded large surface, light background, purple controls, soft shadows. |

## Current `CanDoItAll` state

### Project structure editor today

Current files:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Components\ProjectStructureCanvas.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\wwwroot\js\workbenchInterop.js`

Current behavior:

- JS canvas exists and is already wired through Blazor.
- Nodes and links render on canvas.
- Left-drag background pans.
- Wheel zoom exists.
- Nodes are draggable.
- There is a simple hex menu on right click.
- Double-click opens the selected node.
- View state saves pan/zoom/selection.

Current gaps versus reference:

| Missing or weak area | Current state |
| --- | --- |
| Shared workbench layout language | Page uses generic `SectionCard` layout, not the reference stage + inspector + lower-panel composition. |
| Canvas chrome | No fit button, no maximize, no help button, no zoom rail, no bottom hint, no in-canvas help overlay. |
| Background and polish | Grid background is generic; no reference gradient/dot stage feel. |
| Interaction fidelity | No multi-select, no marquee, no Ctrl/Cmd move mode, no middle-mouse pan, no branch collapse, no keyboard shortcuts. |
| Selection model | Only single selected node is supported. |
| State model | No collapsed-node state, no maximize state, no multi-select state, no manual-offset model equivalent. |
| Context affordances | Current radial hex menu is minimal and visually simpler than the reference. |
| Quick-create launcher | No persistent top-left add rail or typed quick-create palette. |
| Node visuals | Shapes are basic; metadata, chips, footer treatment, and type-specific palettes are far less refined. |
| Inspector | Plain generic card with text and square buttons, not a true mirrored editor surface. |
| Package/section/item tiers | Current graph treats all objects similarly; visual hierarchy is weaker than the reference. |
| Add-next-to-source | Current context actions forward the clicked node's exact coordinates for create commands, which risks overlap instead of adjacent placement. |

### Prompt wizard editor today

Current primary file:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`

Current behavior:

- Strong domain/service model exists.
- Wizard steps already exist.
- Prompt run nodes exist in persistence and service projections.
- Branching is supported through `PromptFactoryService.BranchNodeAsync`.
- The UI displays nodes as a vertical list of cards.

Current gaps versus reference:

| Missing area | Current state |
| --- | --- |
| Canvas at all | No canvas host, no viewport, no graph rendering, no connectors, no node geometry. |
| Shared visual system | Page cards do not use the learning builder canvas visual system. |
| Right-side inspector | No selected-node editor panel tied to graph selection. |
| Multi-select and bulk actions | None. |
| View state persistence | No canvas state because there is no canvas. |
| Context menus and quick-add rail | None. |
| Branch visualization | Branches are listed as cards, not spatially visualized. |
| Shared chrome | No fit, maximize, help, zoom, hint, or canvas help overlay. |
| Prompt-flow graph editing | No direct manipulation surface. |

## Critical implementation implication

`ProjectStructurePage` is a parity-upgrade problem.

`PromptFactoryPage` is a structural redesign problem.

That means the implementation plan must:

1. Extract a reusable shared workbench canvas foundation first.
2. Migrate project structure to that shared foundation.
3. Rebuild prompt factory around that same foundation instead of making a one-off prompt canvas.

## Screenshot-specific requirements that are easy to miss

These must be treated as required, even if some are only visible in screenshots and not fully expressed in the code snippets reviewed:

- The host surface is not flat white; it has a warm-left to cool-right atmosphere.
- The zoom rail is a pill, not loose independent controls.
- The add affordance is always visually prominent in the top-left cluster.
- The right inspector feels like a first-class editor, not an auxiliary panel.
- The node cards are large enough to show title, support text, chips, and small status indicators without crowding.
- The modal language matches the canvas language.
- The vertical quick-create hex rail is part of the interaction vocabulary shown in the screenshots and should not be ignored.

## Code-vs-screenshot watch items

These are not blockers, but they must be handled intentionally:

1. The engine code clearly wires `score`, `text`, `checkpoint`, and `image` add actions. The screenshots also show `exercise`, `hint`, and a more explicit vertical create rail. Treat those screenshot-observed options as required labels and decide during implementation whether they map to existing types, new types, or product aliases.
2. The engine declares `video` support in type labels, and the user explicitly called out video. The reference page wiring does not currently expose `add-video`. The `CanDoItAll` plan should still reserve and prepare for video support instead of hard-coding a dead-end item palette.
3. The first screenshot is a large multi-step modal unrelated to the raw canvas engine, but it proves the modal visual language the user expects for canvas-adjacent editing flows.
