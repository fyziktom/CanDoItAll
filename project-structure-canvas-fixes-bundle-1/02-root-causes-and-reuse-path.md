# Root Causes And Reuse Path

This file maps the current structure page architecture to the existing prompt factory solution so implementation can reuse working patterns instead of inventing another one-off overlay.

## Root Cause 1: The shared stage still reserves an inspector column

Current path:

- `src\CanDoItAll.ComponentKit\Components\CanvasWorkbenchStage.razor`
- `src\CanDoItAll.ComponentKit\wwwroot\canvas-workbench.css`
- `src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`

Relevant facts:

- the stage supports `Canvas`, `Inspector`, and `Supporting` regions
- the structure page currently uses all three
- the shared CSS assigns a large second column when `Inspector` is present

Decision:

- for the structure page, the `Inspector` slot must be removed
- the page should switch to the same canvas-only stage mode already used in prompt factory canvas mode

## Root Cause 2: Floating behavior is page-specific in prompt factory, not shared yet

Current prompt factory implementation:

- inspector rendered inside `OverlayContent`
- JS interop in `src\CanDoItAll.Modules.Factory\wwwroot\js\promptFactoryInterop.js`
- drag handle and reset logic in `PromptFactoryPage.CanvasInspector.cs`
- resize via CSS `resize: both`
- hide/show via local page state
- drag clamp explicitly respects the toolbar bottom edge

What this proves:

- the product already has a successful pattern for in-canvas inspector behavior
- the missing step is extraction into a shared canvas window host usable by more than one page

Decision:

- create a shared floating canvas window host in `CanDoItAll.ComponentKit`
- move prompt factory onto that shared host or keep prompt factory on the same behavior contract while structure adopts it
- do not duplicate another page-specific JS file with slightly different rules

## Root Cause 3: Overlay placement has no formal safe-zone contract

Current state:

- the toolbar is a persistent absolute layer
- the health overlay is another absolute layer
- only prompt factory drag logic understands that panels must remain below the toolbar

Decision:

- the shared floating window host must define a safe-top region
- all default panel spawn positions and all drag clamping must respect that safe-top region
- the toolbar should remain the top-most operational surface, not just another overlay fighting for z-index

## Root Cause 4: Inspector content and capability logic are mixed directly into the page markup

Current state:

- `ProjectStructurePage.razor` contains empty, multi-select, single-select, attachment, action, and create content inline
- the page already has the business logic for progress, marker, priority, commands, attachment preview, and selection routing

Decision:

- preserve the existing behavior contract first
- move the presentation into a dedicated in-canvas selection panel component or render fragment set
- only after parity is preserved should layout density and control compaction be applied

## Root Cause 5: Local file opening requires a real host bridge

Current state:

- the page can open managed file routes in a browser tab
- there is no browser-safe way to force `xlsx`, `docx`, or similar files to open directly from the user drive without a trusted native or backend path

Decision:

- implement `Open locally` only through an explicit host-side action
- the open action must resolve a trusted local path or managed file path on the backend and launch it via the OS shell
- if that capability is unavailable, the UI must clearly fall back to `Open in new tab`

## Reuse Plan

Use these existing assets:

- prompt factory drag-and-clamp logic as the starting JS behavior
- prompt factory floating inspector CSS rules as the starting visual behavior
- the shared component kit namespace for the extracted floating window host
- the existing structure page selection/action logic for parity
- the current attachment preview logic as the baseline preview engine

Do not use these as the final answer:

- hard-coded one-off top offsets for each page
- CSS-only overlap avoidance
- browser-only anchor links for local file open
- a second dedicated page column disguised as a narrower inspector
