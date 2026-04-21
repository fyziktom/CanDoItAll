# Normalized Requirements

## Runtime Refactor

- `RQ-01` Split `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\wwwroot\js\runtime\workbench\01-webgl-workbench.js` into smaller logical modules and classes/helpers with clear ownership instead of leaving all runtime behavior in one file.
- `RQ-02` Preserve a stable entrypoint for `window.CanDoItAll.webglWorkbench` so the Blazor component, automated tests, and Playwright MCP proof can still drive the runtime after the refactor.
- `RQ-03` Keep the surface contract files in `WebGlWorkbenchSurface.cs`, `WebGlWorkbenchUiState.cs`, and `WebGlWorkbenchEvents.cs` aligned with the runtime split rather than letting the JS and C# contracts drift.

## Canvas-Informed Architecture

- `RQ-04` Use CanvasLib as an architectural comparison for how to separate foundation, menu logic, interaction logic, and runtime entry, while avoiding a one-to-one copy of CanvasLib's largest files.
- `RQ-05` Document the chosen WebGlLib split and the reasons it differs from CanvasLib where the WebGL runtime needs a different shape.

## In-Scene Authoring Chrome

- `RQ-06` Add a top toolbar inside the WebGL surface for the core workbench actions the user asked for, similar in role to the canvas toolbar but visually and technically rendered as part of the WebGL runtime.
- `RQ-07` Add a right-click context menu inside the WebGL surface and prove the open state visually.
- `RQ-08` Add explicit tool modes for selection, delete, connect, and reconnect that are available from the in-scene toolbar and/or context menu.

## Node Information And Settings

- `RQ-09` Add explicit node-info display modes for `detailed`, `miniature`, and `hidden`.
- `RQ-10` Add at least one additional useful setting beyond the requested node-info density modes. Preferred candidates are grid visibility, anchor visibility, and edge-label visibility.
- `RQ-11` Persist the new stage settings through the WebGL UI state contract so updates and rerenders keep the chosen authoring mode.

## 3D Authoring Behavior

- `RQ-12` Support selection directly in the stage for nodes and any authoring targets needed by delete/connect/reconnect behavior.
- `RQ-13` Support connection and reconnection flows in 3D without depending on the current host-side form as the primary authoring surface.
- `RQ-14` Support delete flows from the in-scene tools and keep the behavior honest to the sandbox scope if it remains resettable rather than production-persistent.

## Sandbox Integration And Proof

- `RQ-15` Update the WebGl sandbox host so the stage-local authoring actions are no longer primarily driven by the current overlay/button/form UI outside the stage.
- `RQ-16` Update or add .NET tests for the affected UI-state, session, interop, and scene-adapter contracts.
- `RQ-17` Update or add Playwright coverage for the live sandbox route to prove the toolbar, settings, menu, and 3D authoring flows.
- `RQ-18` Run manual Playwright MCP checks with screenshots on the live route before calling the work complete.
