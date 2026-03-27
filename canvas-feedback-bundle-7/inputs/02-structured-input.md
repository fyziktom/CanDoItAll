# Structured Input

## Core Objective

- Close all notes from `feedback7.docx` by improving project-structure canvas nodes and settings behavior without replacing the existing CanvasLib and Workbench interaction model.

## Hard Constraints

- Keep the change minimal and maintainable inside the existing `CanvasLib` plus `Modules.Workbench` boundaries.
- Use strongly typed C# contracts for any new node presentation data. Do not parse command intent or path semantics from arbitrary strings in JavaScript.
- Preserve existing preview behavior for nodes that already support preview.
- Derive quick actions from existing node command and inspector logic instead of inventing a second action system.
- Do not silently skip node types that reach the non-preview double-click path. Unsupported or non-editable cases must be explicit.
- Reuse the current project styling and component patterns. Do not introduce Tailwind, a new dialog framework, or a new menu system.

## Source Artifacts

- Raw feedback file: `C:/Users/lucys/OneDrive - TechnicInsider/Produkty/CanDoItAll/feedbacks/feedback7.docx`
- Extracted feedback notes: `C:\repositories\CanDoItAll\canvas-feedback-bundle-7\inputs\03-feedback7-extracted.md`
- Reference screenshots:
  - `C:\repositories\CanDoItAll\canvas-feedback-bundle-7\inputs\feedback7-media\image1.png`
  - `C:\repositories\CanDoItAll\canvas-feedback-bundle-7\inputs\feedback7-media\image2.png`
  - `C:\repositories\CanDoItAll\canvas-feedback-bundle-7\inputs\feedback7-media\image3.png`
- Current implementation owners:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.RuntimeLaunch.cs`

## Validation Expectations

- Proof must cover all five raw notes, not a reduced subset.
- Completion requires targeted automated validation plus browser evidence because the feedback is interaction-heavy and layout-sensitive.
- The final execution report must list exact commands, exact screenshot paths, and an explicit raw-note closure matrix.

## UI Validation Strategy

- Run a maximized browser pass against the project-structure surface to inspect node presentation, double-click modal behavior, and settings overlay placement.
- Capture screenshots for:
  - a long path-backed node using the new compact presentation
  - the non-preview double-click quick-action modal
  - the settings overlay opened below the toolbar
- Follow with a narrower-width pass to confirm the settings overlay still stays clear of the toolbar and the node/card composition remains readable.

## Working Assumptions

- Path-backed node facts are already derived in `ProjectStructureNodeDescriptor`, so the safest change is to enrich the mapped canvas node payload instead of teaching JavaScript how to infer path semantics from display strings.
- Non-preview double-click handling should stay page-owned because the correct secondary action already depends on Workbench page state and `ProjectWorkbenchService`.
- The shared canvas toolbar and settings overlay are the right place to solve the `cfg` icon and toolbar-overlap note because the issue is part of the reusable `CanvasWorkbench` chrome.

## Primary Risks

- Changing the shared canvas node contract can affect other graph surfaces if the new metadata is not optional and additive.
- A quick-action modal can drift from the existing action catalog if the mapping is duplicated instead of derived from current command resolution.
- Some system-managed nodes may not support edit semantics even though they can be double-clicked, so the implementation must make those exceptions explicit instead of faking an `Edit` action.
- Compacting path content can regress discoverability or clipboard behavior if the tooltip and copied-state feedback are weak.
