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

## Third-pass QA findings

- Maximize is not delivering true editor takeover behavior. The canvas needs to fill the window, sit on top, and lock the viewport behind it.
- `Focus root` must always select the root item and center the graph on it, regardless of the current selection.
- Child-collapse affordances should sit where the outgoing branch starts so hide/show feels tied to the graph structure.
- The create vocabulary still lacks generic project blocks with subtype choices such as feature, revision, testing, prompting, financial, and marketing.
- The right-click and `+` create menus now need hierarchical second-layer hex menus for grouped vocabularies such as blocks, prompts, and attachments.
- Multi-select needs explicit `Ctrl + Shift + click` add/remove behavior without disturbing the current selection set.
- The inspector `Create next to source` surface needs grouped tooling rather than one flat long row.
- Image and video items need actual file upload support, including file picker and drag/drop from the create dialog.

## Third-pass execution checklist

### Stage behavior and graph controls

- [x] Make maximize turn the workbench into a full-window overlay with top z-order and viewport lock.
- [x] Make `Focus root` always select the root node and center the canvas on it.
- [x] Move collapse/expand affordances to the branch-origin side of nodes with children.

### Hierarchical create vocabulary

- [x] Add generic `ProjectBlock` support with standard planning and management subtypes.
- [x] Add image and video item types to the project structure domain.
- [x] Build second-layer hex menus for grouped create actions such as blocks, prompts, and attachments.
- [x] Reuse the same hierarchical hex vocabulary from the canvas `+` button.

### Editor interaction improvements

- [x] Add `Ctrl + Shift + click` multi-select add/remove behavior.
- [x] Keep existing single-select and keyboard note flows intact after the multi-select change.
- [x] Group the inspector `Create next to source` tools into logical accordion sections.

### Media create flows

- [x] Allow image items to upload via file picker.
- [x] Allow video items to upload via file picker.
- [x] Support drag/drop into the in-canvas create dialog for image and video items.
- [x] Persist uploaded media metadata and make the created media node appear immediately on the canvas.

### QA closure

- [x] Add regression coverage for maximize, grouped create vocabularies, and media creation where practical.
- [x] Run full solution verification again.
- [x] Perform another manual Playwright QA pass with screenshots for maximize, grouped menus, block creation, and media upload.

## Fourth-pass QA findings

- The workbench takeover behavior is now functionally correct, but the latest QA pass still needed proof that dock/max transitions preserved the viewport lock and full-window host sizing in both directions.
- `Focus root` still had a centering drift after selecting another node because click-selection updates were echoing stale viewport state back into JS, and the old centering math did not use the rendered node box.
- The media create surface needed explicit verification of both upload entry points:
  - drag/drop for image creation
  - file-input change handling for video creation
- The grouped radial menu needed a final QA sweep to prove submenu coverage from the actual canvas surface rather than only from the inspector.

## Fourth-pass closure notes

- `Focus root` now selects the project root and centers it exactly by:
  - keeping canvas-originated selection changes out of the stale view-state echo path in `ProjectStructurePage.razor`
  - centering against the rendered DOM node box in `canvasWorkbenchInterop.js`
- Maximize now verifies cleanly in both states:
  - docked host is smaller than the viewport and does not lock the body
  - maximized host fills the viewport and enables body lock
- Manual Playwright QA on `https://127.0.0.1:7271` confirmed:
  - grouped `+` and right-click hex menus, including `Assets` and `Blocks` submenus
  - `Ctrl + Shift + click` multi-select add/remove
  - `Tab` child-note chaining, `Enter` sibling-note chaining, host refocus after save, and double-click note re-edit
  - right-click `Link` modal creation with the new node appearing immediately and selected
  - drag/drop image upload with immediate canvas render and inspector image preview
  - file-input video upload handling with immediate canvas render and inspector video preview
- Regression coverage was expanded in:
  - `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
  - `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
