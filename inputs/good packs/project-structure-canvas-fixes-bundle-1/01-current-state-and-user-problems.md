# Current State And User Problems

This file captures the current failures visible in the supplied structure canvas screenshot and confirms them against the current code.

## Observed Problems

## 1. Canvas Health blocks the toolbar

What is visible:

- the `Canvas health` window sits in the same top-left region as the toolbar
- the health window visually covers the toolbar, so core canvas actions are not always reachable

Why this happens in code:

- `ProjectStructurePage.razor` renders the health card inside `OverlayContent`
- `ProjectStructurePage.razor.css` positions it at `top: 1rem` and `left: 1rem`
- the shared toolbar in `canvas-workbench.css` also starts at `inset: 1rem 1rem auto 1rem`
- the health overlay is not draggable, not resizable, not minimizable, and not clamped below the toolbar

Consequence:

- the first interaction zone of the canvas is already contested before the user moves anything
- the toolbar fails the user requirement of being accessible all the time

## 2. Toolbar width does not behave like a true canvas header

What is visible:

- the toolbar feels squeezed into the remaining stage shell rather than acting as the top frame of the full canvas
- the zoom rail and action groups consume significant width and the rail depends on horizontal overflow instead of a deliberate compact mode

Why this happens in code:

- `CanvasWorkbenchStage` still renders the structure page in a two-column grid
- `.cw-main-grid` uses `grid-template-columns: minmax(0, 1.18fr) minmax(20rem, 0.82fr)`
- the toolbar is an absolute strip inside the canvas surface, but the canvas itself already lost width to the right inspector shell
- the toolbar has no responsive priority system beyond basic scrolling and smaller mobile rules

Consequence:

- users do not get a full-width workbench feel
- toolbar fit problems get worse exactly when the right inspector consumes width

## 3. The right panel takes too much space

What is visible:

- a large dedicated inspector column sits to the right of the structure canvas
- the canvas no longer dominates the page even though the work is canvas-first

Why this happens in code:

- `ProjectStructurePage.razor` passes content through the `Inspector` slot
- `CanvasWorkbenchStage.razor` renders that slot as a separate `<aside class="cw-inspector-shell">`
- the shared workbench CSS gives that inspector shell a large reserved column and fixed visual weight

Consequence:

- the stage width is permanently reduced
- the structure page falls behind prompt factory’s more advanced in-canvas model

## 4. The current right panel is functional, but not space-efficient

What exists today:

- empty selection guidance
- multi-select view with clear/focus/border/status actions
- single-node summary
- attachment preview for image, video, and some document types
- node action buttons
- grouped create-next-to-source tooling

What is inefficient today:

- multiple stacked cards with repeated headings and generous padding
- many full-text buttons where icon-plus-tooltip or shorter labels would work
- a create section that can become very tall
- large margins and card gaps for content that must fit beside a canvas
- repeated copy that adds height but not operational value

Consequence:

- the panel spends too much of its area on chrome and explanatory copy
- scrolling becomes more likely than necessary

## 5. Multi-select is incomplete in the inspector surface

What exists today:

- clear selection
- focus first
- border
- clear border
- status updates for `In progress`, `Done`, and `N/A`

What is missing relative to the user request and current backend capability:

- progress controls
- marker controls
- priority controls
- a shared-capability model that only shows actions common to the selected node set

Why this is a real gap:

- the action catalog and page code already support progress, marker, and priority updates
- those capabilities are available through canvas actions, but not fully surfaced in the multi-select inspector view

## 6. File and media behavior is only partially solved

What exists today:

- image preview
- video preview
- document preview for supported types
- `Open in new tab`

What is missing:

- audio preview
- `Open locally` for spreadsheet and similar file nodes when the file exists on the user machine
- a clear capability matrix by file type
- a consistent fallback when inline preview is not possible

## 7. Supporting cards remain outside the canvas workflow

What exists today:

- outline card below the stage
- graph health card below the stage

Why this matters:

- they are not the main width problem, but they continue the split-surface model
- they should be reviewed during this bundle and either compacted or prepared for later migration into optional canvas utility windows if needed

## Bundle Direction

The structure page should adopt the same canvas-first direction already used in prompt factory:

- remove the dedicated inspector column
- move node-aware panel behavior into the canvas
- make all canvas windows behave like proper workbench windows
- then optimize density once parity is protected
