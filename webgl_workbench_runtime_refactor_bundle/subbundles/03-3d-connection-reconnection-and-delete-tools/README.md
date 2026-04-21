# 3D connection, reconnection, and delete tools

## Status

- `Ready`

## Objective

- Add usable 3D authoring tools for selection, delete, connect, and reconnect so the stage can perform the requested node and edge mutations without depending on the old host-side form as the primary control surface.

## Covered Inputs

- `N004` connection and reconnection tools in 3D
- `N006` delete, selection, and related tools
- `RQ-12` through `RQ-14`

## Prerequisites

- `subbundles/01-runtime-foundation-refactor-and-api-shaping` completed and trusted
- `subbundles/02-in-scene-toolbar-and-settings-chrome` completed and trusted

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\wwwroot\js\runtime\workbench\01-webgl-workbench.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchEvents.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\ProcessWebGlSandboxSession.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessWebGlSceneAdapter.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWebGlSandboxSessionTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\WebGlSandboxSmokeTests.cs`

## Deliverables

- Tool-mode behavior for select, delete, connect, and reconnect.
- Stage-local selection of the entities needed by those tools.
- A delete path that is honest to the sandbox scope and survives rerender until reset.
- Updated sandbox-session or event-bridge logic for the new authoring requests.
- Updated automated proof for the stage-local authoring flows.

## Dependency Impact

- `04-sandbox-integration-regression-proof-and-closure` depends on this phase for the final authoring proof.
- If connect/reconnect/delete are weak here, the final closure proof becomes invalid even if the toolbar looks good.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Add or extend runtime hit-testing so the stage can target the entities required by connect, reconnect, and delete flows.
2. Add any new event or command contracts the sandbox host needs for deletion or richer authoring requests.
3. Replace the host-side connection form as the primary authoring path with stage-local tool flows.
4. Update the sandbox session to log and preserve the new actions across rerender.
5. Prove connect, reconnect, delete, and selection on the live route.

## Scope Exceptions

- If a delete operation must remain sandbox-local rather than full process-model persistence, record that explicitly in the execution report and raw-note closure table.

## Do Not Do

- Do not rely on the old host-side connection form as the primary proof surface.
- Do not hide unsupported mutation cases behind generic success messages.
- Do not call the reconnect flow complete without proving the edge endpoint actually changed.

## Acceptance Checklist

- A user can choose a stage-local selection tool and visibly select authoring targets.
- A user can perform a connect flow in 3D and see the resulting edge mutation.
- A user can perform a reconnect flow in 3D and see the target/source change that was requested.
- A user can delete from the stage-local tool surface and see the stage update honestly.
- The command log or equivalent sandbox feedback reflects the new actions.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ProcessWebGlSandboxSessionTests|ProcessWebGlSceneAdapterTests"`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter WebGlSandboxSmokeTests`
- Desktop browser pass on `/webgl/process-workbench?template=branching-code-review`
- Screenshot at `output/playwright/webgl-sandbox/bundle-03-authoring-tools-desktop.png`
- Manual Playwright MCP proof for select, connect/reconnect, and delete actions

## Browser Validation Logging

- Route: `/webgl/process-workbench?template=branching-code-review`
- Viewport: `1900x1200`
- Required Playwright MCP actions:
- switch tool modes from the stage-local toolbar
- perform a selection action
- perform a connect or reconnect action
- perform a delete action
- inspect `getSceneSnapshot` and capture a screenshot
- Required review questions:
- Are the active tool cues clear enough to understand?
- Is the authoring flow visually coherent and not dependent on hidden page controls?
- Does delete leave the stage in a sensible state?

## Progression Gate

- Downstream work may continue only when stage-local authoring tools mutate the sandbox state correctly, the resulting scene survives rerender, and the updated automated proof passes.

## Suggested Agent Prompt

```text
Implement this subbundle only.
```
