# Canvas Interaction Repair Checklist

This checklist tracks the post-migration repair pass requested on 2026-03-19 for the shared canvas workbench.

## Second-pass QA findings

- The current radial menu still exposes only a narrow create subset instead of the broader project object vocabulary.
- Create actions are not consistently available from every node, which breaks the "mindmap from anywhere" expectation.
- Keyboard note flow needs an explicit QA pass for repeated `Tab` / `Enter` chaining after each save.
- Right-click create needs a stronger guarantee that the newly created node becomes visible and selected immediately after the modal saves.
- Zoom is still vulnerable to state-sync jitter because Blazor rerenders can echo the same client-side viewport state back into JS.

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

## Second-pass execution checklist

### Create vocabulary and menu coverage

- [x] Expose the full project object create palette in the radial menu, not only the current narrow subset.
- [x] Keep create actions available from every node, while ordering them by likely next-step relevance for the selected node type.
- [x] Expose the same broader typed create vocabulary in the quick-create rail.
- [x] Ensure complex create actions such as `Link`, `Connector`, `File`, `TestPlan`, `ValidationRun`, and `SecretReference` open the in-canvas modal immediately.

### Infinite note-flow behavior

- [x] Keep the newly created simple note selected after keyboard creation.
- [x] Restore keyboard focus to the canvas after note save so `Tab` / `Enter` can continue the chain without another click.
- [x] Verify repeated `Tab` / `Enter` flows can continue from newly created notes without breaking selection.

### Post-create visibility and viewport stability

- [x] Ensure nodes created from the radial menu appear on the canvas immediately after modal confirmation.
- [x] Keep newly created nodes visible in the current viewport or bring them into view when needed.
- [x] Remove the remaining zoom-in / zoom-out glitch caused by client-state and Blazor-state echoing.
- [x] Re-check fit, zoom buttons, slider zoom, and wheel zoom after the state-sync fix.

### QA closure

- [x] Add regression coverage for broader create actions and repeated keyboard note chaining.
- [x] Re-run iterative Playwright QA with screenshot review after each major fix.
- [x] Capture fresh screenshots for the expanded create palette, create modal, and chained note flow.

## QA closure notes

- Automated verification passed with `dotnet test CanDoItAll.slnx --no-restore -v minimal`.
- Manual Playwright QA on `http://127.0.0.1:5196` confirmed the full radial create palette, the in-canvas typed `Link` modal, chained `Tab` and `Enter` note creation without re-clicking, immediate canvas rendering for right-click-created items, and stable 55% zoom with the selected node still inside the canvas bounds.
