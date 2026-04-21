# Runtime foundation refactor and API shaping

## Status

- `Ready`

## Objective

- Refactor the current WebGlLib runtime into smaller logical modules and classes/helpers while preserving the existing sandbox render baseline and the public `window.CanDoItAll.webglWorkbench` automation surface.

## Covered Inputs

- `N001` split the monolithic runtime file
- `N002` use CanvasLib as a structural comparison rather than a blind copy
- `RQ-01` through `RQ-05`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\wwwroot\js\runtime\workbench\01-webgl-workbench.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchSurface.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchUiState.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchEvents.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\Components\Workbench\WebGlWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\01-foundation.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03a-context-menu-shortcuts.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js`

## Deliverables

- A smaller module layout for the WebGlLib runtime with explicit ownership boundaries.
- A stable entry module that still exposes the runtime API expected by the Blazor component and proof surface.
- Any required contract updates in the WebGlLib C# surface/state/event types.
- Notes in the bundle or code comments explaining the chosen split versus the CanvasLib comparison.

## Dependency Impact

- Subbundles `02`, `03`, and `04` all depend on this phase.
- If the runtime split is weak, later chrome and authoring work will either duplicate state logic or break the automation bridge used by tests and Playwright MCP proof.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Identify the current runtime responsibilities and group them into cohesive modules.
2. Create the new files and classes/helpers under the WebGlLib workbench runtime folder.
3. Keep the public runtime bridge stable from the entry module.
4. Update any C# surface or interop code that must stay aligned with the refactor.
5. Re-run the baseline sandbox render proof before moving to subbundle `02`.

## Scope Exceptions

- none

## Do Not Do

- Do not add the toolbar, settings menu, or context menu here beyond the minimal infrastructure needed for the split.
- Do not widen into sandbox-page cleanup yet.
- Do not break or quietly remove existing automation helpers.

## Acceptance Checklist

- The runtime is no longer implemented as one all-responsibility file.
- The main entrypoint still exposes `window.CanDoItAll.webglWorkbench`.
- The sandbox route still renders nodes and edges after the split.
- The bundle records how the chosen split uses CanvasLib as a reference without cloning its large-file problems.

## Proof Required

- `npm run webgllib:verify-assets`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WebGlWorkbenchUiStateTests`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ProcessWebGlSandboxSessionTests|ProcessWebGlSceneAdapterTests|WebGlWorkbenchInteropTests"`
- Desktop browser pass on `/webgl/process-workbench?template=branching-code-review`
- Screenshot at `output/playwright/webgl-sandbox/bundle-01-runtime-foundation-desktop.png`

## Browser Validation Logging

- Route: `/webgl/process-workbench?template=branching-code-review`
- Viewport: `1900x1200`
- Required Playwright MCP actions:
- navigate to the route
- evaluate `getSceneSnapshot`
- evaluate `getState`
- capture a desktop screenshot
- Required review questions:
- Does the stage still render correctly after the split?
- Are labels, anchors, and diagnostics still positioned correctly?
- Is any current UI obviously broken before new chrome work begins?

## Progression Gate

- Downstream work may continue only when the split runtime still renders the sandbox route, the automation helpers remain callable, and the targeted .NET tests are green.

## Suggested Agent Prompt

```text
Implement this subbundle only.
```
