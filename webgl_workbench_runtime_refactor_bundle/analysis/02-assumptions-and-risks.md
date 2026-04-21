# Assumptions And Risks

## Assumptions

- The sandbox route at `/webgl/process-workbench` remains the primary proof surface for this work.
- The user's request for a WebGL-drawn toolbar and menu applies to the stage-local authoring controls; the surrounding page scaffold may remain Blazor-rendered.
- The public runtime bridge should remain compatible enough that Playwright MCP and automated tests can keep using `getSceneSnapshot`, `getState`, `simulateDrag`, `simulateConnection`, and export helpers.
- If a full process-model delete path is too risky for this pass, a sandbox-local delete implementation is acceptable only if it is truly stage-driven, resettable, and called out honestly in the final closure notes.

## Critical Path Risks

- `01-runtime-foundation-refactor-and-api-shaping` is a critical foundation. If the split breaks state sync, interop wiring, or automation helpers, every later subbundle becomes hard to trust.
- `02-in-scene-toolbar-and-settings-chrome` is a critical UI foundation. If the toolbar and settings model are weak, the authoring-tool subbundle will either duplicate logic or reopen the chrome layer.
- Edge hit-testing and reconnect behavior are the most likely reasons `03-3d-connection-reconnection-and-delete-tools` could reopen `01` or `02`.

## Validation Risks

- Manual browser reasoning is insufficient here because the user explicitly asked for Playwright MCP and screenshots on the real WebGL result.
- Existing Playwright tests are likely to fail as soon as host HTML controls are removed or demoted, so proof must be refreshed during the execution loop rather than postponed to the end.
- WebGL-drawn chrome can look correct in DOM/state checks but still be visually cramped, clipped, or too small to use, especially at narrower widths.

## Reopen Triggers

- Reopen subbundle `01` if the public runtime API changes in a way that breaks `WebGlWorkbench.razor`, the sandbox session, or the automated proof helpers.
- Reopen subbundle `02` if the toolbar, settings panel, or context menu are still primarily HTML host controls instead of stage-local WebGL chrome.
- Reopen subbundle `03` if connect, reconnect, or delete actions only work through the old host-side form or cannot survive a rerender.
- Reopen any UI subbundle if the desktop screenshots show clipping, illegible labels, or unusable toolbar/menu hit targets.
