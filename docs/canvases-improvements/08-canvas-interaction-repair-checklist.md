# Canvas Interaction Repair Checklist

This checklist tracks the post-migration repair pass requested on 2026-03-19 for the shared canvas workbench.

## Analysis summary

- The visual/context issues are concentrated in `CanDoItAll.ComponentKit`:
  - `CanvasWorkbench.razor`
  - `wwwroot/js/canvasWorkbenchInterop.js`
  - `wwwroot/canvas-workbench.css`
- The selection persistence bug is partly shared-canvas behavior and partly page wiring:
  - `ProjectStructurePage.razor` was updating local selection but not refreshing persisted canvas state on click selection.
- The create/edit regressions require shared interop plus page/service support:
  - `ProjectStructurePage.razor`
  - `ProjectWorkbenchModels.cs`
- Prompt Factory is less affected by creation flows, but still inherits the shared right-click menu and zoom fixes.

## Execution checklist

### Shared canvas interaction repairs

- [x] Replace the stacked right-click menu with a cursor-centered radial/hex affordance.
- [x] Give context actions distinct per-tone colors so the menu matches the screenshot language.
- [x] Keep clicked nodes visibly selected with a stable highlighted border after Blazor rerenders.
- [x] Fix zoom-out and fit/pan behavior so unzoom does not lose the graph or drift the viewport.

### In-canvas create and edit flows

- [x] Add an in-canvas create dialog for typed create actions so users can enter basic information immediately.
- [x] Add inline simple-note creation on `Tab` as a child node below the selected item.
- [x] Add inline simple-note creation on `Enter` as a sibling node under the same parent as the selected item.
- [x] Make the newly created simple note take selection and enter text edit immediately.
- [x] Save inline note text on `Enter` and render it back as plain text bubble content.
- [x] Reopen inline text editing on double-click for simple-note nodes.

### Project Structure domain wiring

- [x] Extend project object creation/update flows to accept inline-canvas create payloads.
- [x] Make right-click create actions refresh the structure surface so created items appear immediately.
- [x] Keep right-panel create actions aligned with the new in-canvas create workflow.

### Verification

- [x] Update automated coverage where it provides meaningful regression protection.
- [x] Run solution tests successfully.
- [x] Run Playwright MCP manual QA for selection, menu shape, create dialogs, child note creation, sibling note creation, note editing, and zoom-out behavior.
- [x] Capture fresh screenshots proving the repaired canvas interactions.
