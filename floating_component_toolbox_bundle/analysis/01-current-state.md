# Current State

## Shared Floating Windows

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.OverlayLib\Components\Core\OverlayWindow.razor` owns generic floating-window rendering, minimize, hide, normalize, geometry, and JS interop.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor` adapts `OverlayWindowState` to `CanvasWorkbenchWindowState` and keeps CanvasLib compatibility.
- WebGL sandbox already consumes `OverlayWindow` directly for selection and command windows.

## Existing Toolboxes

- Project structure has a domain-specific toolbox window in `ProjectStructureToolboxWindow.razor`. It uses `CanvasFloatingWindow`, pills, search, and `TreeView` over `ProjectStructureInspectorCreateGroup` and `CanvasWorkbenchAction`.
- Process canvas has a domain-specific toolbox window in `ProcessCanvasToolboxWindow.razor`. It uses `CanvasFloatingWindow`, search, groups, buttons, and `ProcessCanvasToolboxGroup`.
- Prompt factory embeds a larger custom toolbox directly in `PromptFactoryPage.razor`. It uses `CanvasFloatingWindow`, CanvasLib context-toolbox CSS classes, search, sections, groups, add buttons, and a preview popover.
- CanvasLib also contains a JS-created context toolbox in `wwwroot/js/runtime/workbench/03-interaction-and-state.js` with CSS in `05-overlays-and-composer.css`; that is a right-click/runtime menu surface, not the floating component-library window requested here.
- WebGL currently has host chrome actions and overlay windows but no reusable floating component toolbox.

## Existing Add Flows

- Project structure selected toolbox actions flow to `ProjectStructurePage.ComponentAdapters.cs`, then into existing create-dialog/canvas action logic.
- Process canvas selected toolbox actions flow through `ProcessWorkspace.StepsPresenter.OpenToolboxActionAsync`, then into `ExecuteCanvasActionAsync`.
- Prompt factory selected toolbox blocks call `AddComponentFromToolboxAsync`, either adding immediately or opening the existing create dialog for tokenized components.
- WebGL sandbox edits are in-memory through `ProcessWebGlSandboxSession`, `ProcessWebGlSceneAdapter`, and current surface rebuilds.

## Feasibility Decision

- A generic implementation is possible if it is a view/component shell plus generic toolbox item models, not a replacement for domain creation services.
- The proper shared home is `CanDoItAll.Components.OverlayLib` because the same floating toolbox must render over CanvasLib and WebGL surfaces.
- Canvas-specific wrappers can keep `CanvasFloatingWindow` where Canvas window state matters, while the toolbox body can be shared.
