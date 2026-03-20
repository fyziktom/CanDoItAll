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

## Fifth-pass QA findings

- The second-layer radial menu still visually collides with the first layer because it does not render its own soft overlay or backdrop region.
- Submenus are still hover-fragile because the current implementation ties them to the hovered hex instead of the full submenu orbit, so moving through the gap toward submenu items can collapse the layer.
- The menu stack is still modeled as a single transient submenu, which blocks clean future chaining into third and fourth layers.
- Keyboard `Tab` and `Enter` note creation regressed because canvas focus is not being restored reliably after node clicks.
- Typed create leaves such as `Link` are still vulnerable to skipped modal opening when submenu hover state collapses before the click resolves.

## Fifth-pass execution checklist

### Shared radial menu stack

- [x] Add a semi-transparent submenu backdrop so deeper menu layers visually separate from the previous layer.
- [x] Keep submenu layers open while the pointer remains inside the submenu orbit, not only while it remains over the parent hex.
- [x] Model menu layers as a stack so deeper layers can be closed independently and future third or fourth layers can reuse the same logic.
- [x] Allow right-click inside a submenu orbit to step back one layer without collapsing the whole menu.

### Keyboard note flow

- [x] Restore reliable canvas focus after clicking a node so `Tab` and `Enter` shortcuts work again from the current selection.
- [x] Re-verify repeated note chaining after create and after save, not only from the initial selection.

### Typed create dialogs

- [x] Make grouped create leaves such as `Link`, `Connector`, `Secret`, `File`, `Image`, and `Video` open their modal consistently from radial submenus.
- [x] Re-check that typed create confirmation still renders the new node immediately and keeps it selected.

## Fifth-pass closure notes

- The shared menu stack now renders submenu layers as circular orbits with their own blurred backdrop, so the second layer no longer visually merges into the first.
- Submenu lifetime is now geometry-based rather than hover-target-based:
  - moving through the gap toward submenu hexes keeps the submenu open
  - right-click steps back one submenu level
  - deeper layers can now reuse the same stack model
- Canvas click handling now restores host focus after node selection, which brought `Tab` child-note flow and `Enter` sibling-note flow back without requiring an extra click.
- Typed modal actions were re-verified across grouped create leaves:
  - `Link`
  - `Connector`
  - `Secret`
  - `File`
  - `Image`
  - `Video`
  - plus representative grouped creates from `Blocks`, `Prompts`, and `Assurance`
- Verification for this pass:
  - `dotnet test CanDoItAll.slnx --no-restore -v minimal /p:BuildProjectReferences=false`
  - Playwright project passes against the live local runtime by setting `CANDOITALL_PLAYWRIGHT_BASEURL=http://127.0.0.1:5032`
  - manual Playwright MCP review confirmed the submenu backdrop, submenu persistence, typed `Link` modal, and restored inline note flow

## Sixth-pass QA findings

- The collapse toggle needed one more live-runtime confirmation because earlier browser reports showed the branch affordance ignoring clicks.
- The file-picker click path for image creation needed proof from the refreshed build, not only from drag/drop and automated tests.
- The root radial menu was still clipping against the left viewport edge in the live app, which meant the placement math still underestimated the real menu footprint.
- Aggressive wheel-based zoom-out needed one more browser check with repeated synthetic wheel input to confirm the trackpoint-style down-scroll no longer oscillated.

## Sixth-pass execution checklist

### Canvas interaction repairs

- [x] Re-verify the `+` / `-` branch toggle against the refreshed browser runtime.
- [x] Re-verify that clicking the image chooser surface opens the native file dialog path.
- [x] Fix root radial menu placement so all root actions remain fully inside the visible canvas bounds.
- [x] Re-check repeated wheel zoom-out at high delta so the viewport stabilizes instead of bouncing.

### Regression coverage

- [x] Add a browser regression that fails if the root radial menu clips past the viewport edge.
- [x] Re-run the Playwright project after the menu-placement repair.

## Sixth-pass closure notes

- Root radial menu placement now clamps against the actual layer footprint instead of a rough scalar extent, which keeps the full action ring visible even when the source node is near the window edge.
- Live browser QA on `https://localhost:7271` confirmed:
  - collapse reduces the canvas to the root node and switches the branch affordance to `+`
  - clicking the image chooser surface opens the native file chooser path
  - root right-click actions stay fully inside the viewport after the placement repair
  - repeated wheel zoom-out settles cleanly at `55%` without the previous bounce-back loop
- Verification for this pass:
  - `dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore -v minimal`
  - `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --no-restore -v minimal /p:BuildProjectReferences=false`
  - manual Playwright MCP screenshots reviewed:
    - `output/playwright/structure-menu-clamped-fixed.png`
    - `output/playwright/structure-collapse-working-final.png`
    - `output/playwright/structure-image-picker-modal-final.png`
    - `output/playwright/structure-zoom-wheel-stable-final.png`
