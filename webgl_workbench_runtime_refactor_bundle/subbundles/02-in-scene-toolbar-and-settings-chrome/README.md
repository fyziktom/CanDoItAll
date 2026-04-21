# In-scene toolbar and settings chrome

## Status

- `Ready`

## Objective

- Add a real stage-local toolbar, settings surface, and right-click context menu to the WebGlLib runtime so the requested authoring chrome is drawn in the WebGL workbench instead of relying on host-side HTML controls.

## Covered Inputs

- `N003` right-click menu drawn in WebGL
- `N005` top toolbar drawn in WebGL
- `N007` detailed, miniature, and hidden node-info settings
- `N008` additional useful settings
- `RQ-06` through `RQ-11`

## Prerequisites

- `subbundles/01-runtime-foundation-refactor-and-api-shaping` completed and trusted

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\wwwroot\js\runtime\workbench\01-webgl-workbench.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\wwwroot\css\workbench\webgl-workbench.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchSurface.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchUiState.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\Components\Pages\ProcessWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\wwwroot\webgl-sandbox.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WebGlWorkbenchUiStateTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\WebGlSandboxSmokeTests.cs`

## Deliverables

- A WebGL-drawn toolbar inside the stage with the primary tool controls.
- A settings surface inside the stage that includes node-info density and at least one additional useful scene setting.
- A stage-local right-click context menu with open-state proof.
- UI-state plumbing for the stable settings that must survive updates and rerenders.

## Dependency Impact

- `03-3d-connection-reconnection-and-delete-tools` depends on this phase for tool-mode selection and menu affordances.
- If this chrome layer is weak or HTML-first, later authoring proof will not satisfy the user's request and must reopen this subbundle.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Extend the WebGl runtime state and UI-state contract with explicit tool and settings fields.
2. Add the stage-local toolbar rendering and hit-testing.
3. Add the stage-local settings surface and the right-click context menu.
4. Replace or demote the old host HTML controls that duplicate the new stage-local chrome.
5. Prove the toolbar, settings, and menu in an open state before moving to authoring tools.

## Scope Exceptions

- none planned

## Do Not Do

- Do not implement the full connect/reconnect/delete behavior here beyond what is necessary to expose tool-mode selection and menu actions.
- Do not leave the final authoring chrome as the old Razor overlay and just restyle it.

## Acceptance Checklist

- The toolbar is rendered inside the stage and not only as external page HTML.
- The right-click menu opens inside the stage and is readable in its open state.
- Node-info density supports `detailed`, `miniature`, and `hidden`.
- At least one extra useful setting is implemented and visually observable.
- The chosen settings persist across runtime updates and rerenders.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WebGlWorkbenchUiStateTests`
- Desktop browser pass on `/webgl/process-workbench?template=branching-code-review`
- Screenshot at `output/playwright/webgl-sandbox/bundle-02-toolbar-settings-desktop.png`
- Screenshot at `output/playwright/webgl-sandbox/bundle-02-toolbar-settings-open-menu.png`
- Manual Playwright MCP proof for open toolbar/settings/menu states

## Browser Validation Logging

- Route: `/webgl/process-workbench?template=branching-code-review`
- Viewport passes: `1900x1200` desktop first, narrow follow-up only if layout changed materially
- Required Playwright MCP actions:
- open the stage
- open the toolbar/settings state
- open the right-click context menu
- toggle node-info mode and at least one extra setting
- inspect the resulting stage state and capture screenshots
- Required review questions:
- Is the toolbar readable and aligned with the stage?
- Is the menu clipped, crowded, or too small?
- Do settings changes visibly affect the scene in a way a user can understand?

## Progression Gate

- Downstream work may continue only when the stage-local toolbar and menu are real, visually usable, and the settings contract is stable enough for the authoring tools to build on.

## Suggested Agent Prompt

```text
Implement this subbundle only.
```
