# Three.js runtime foundation and host component

## Status

- Completed

## Objective

- Implement the JS-owned WebGL runtime foundation, host-state lifecycle, label overlay shell, and the first interactive component wrapper for the new library.

## Covered Inputs

- `IN-03`
- `IN-05`
- `IN-12`
- `RQ-03`
- `RQ-04`
- `RQ-07`
- `RQ-09`
- `RQ-10`
- `RQ-15`
- `RQ-22`

## Prerequisites

- `02-universal-webgl-library-skeleton-and-typed-contracts`

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07a-runtime-interaction-router.js
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07b-runtime-rendering.js
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/services/viewport-controller.js
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/services/selection-model.js
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs

## Deliverables

- Initial WebGL runtime foundation built around a JS scene host and a global `window.CanDoItAll.webglWorkbench` entry.
- Default perspective authoring camera for the sandbox path, scene lifecycle, fit/focus/update/dispose flows, and deterministic test mode hooks.
- DOM label/accessibility/automation mirror shell aligned with the scene host.

## Dependency Impact

- Gate A depends on this runtime boundary being correct before process-specific projection begins.
- If labels or automation mirrors are postponed, later proof becomes brittle.

## Validation Depth

- High
- Build + focused host tests + smoke browser proof on a tiny generic scene

## Implementation Steps

1. Bundle Three.js (or the chosen JS engine) into the new asset pipeline without CDN dependencies.
2. Implement create/update/dispose/getState/fitView/focusNode foundations behind a single runtime entry point.
3. Add a DOM mirror layer for labels, anchor coordinates, and accessibility metadata.
4. Wire the Blazor component wrapper to the runtime with coarse-grained interop and explicit disposal semantics.


## Do Not Do

- Do not route every pointer move through .NET.
- Do not ship the first version without a DOM mirror or diagnostics surface.
- Do not add process semantics yet.

## Acceptance Checklist

- A minimal generic scene can render in the new component.
- The runtime exposes lifecycle and view controls behind a stable global API.
- Labels and anchor data are available through the DOM mirror layer.

## Proof Required

- Build the solution.
- Add focused tests for runtime wrapper initialization and state dispatch.
- Capture a browser smoke screenshot of a minimal generic scene to prove the runtime boots.
- Validation commands to run for this subbundle:
- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~WebGl|FullyQualifiedName~CanvasWorkbench" -v:minimal`

## Browser Validation Logging

- Route: temporary generic scene host in the new library or sandbox shell.
- Viewports: `1600x900` and `430x932` if a smoke host exists.
- Review questions: does the scene load, do labels remain readable, and does the camera fit the scene deterministically?

## Progression Gate

- Gate A may only run after the generic runtime is live, the DOM mirror exists, and the view-control API is stable enough to support later template projection.

## Suggested Agent Prompt

```text
Implement only subbundle 03. Build the WebGL runtime foundation behind a JS-owned scene host, add the Blazor wrapper, perspective-capable camera controls, diagnostics and DOM mirror shell, prove a minimal generic scene loads, and stop before process-template projection.
```

## Preserved Bundle Notes

### Review questions

- Does the runtime clearly own the frame loop and hit-testing?
- Are label clarity and automation hooks available from day one?
- Did the component wrapper stay thin?

### Validation commands

- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~WebGl|FullyQualifiedName~CanvasWorkbench" -v:minimal`

### Corrective trigger

- If this subbundle fails, open `_corrective-renderer-boundary-reset` before continuing downstream.

### Corrective template

- `subbundles/_corrective-renderer-boundary-reset`

### Repository touchpoints (relative)

- `src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07a-runtime-interaction-router.js`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07b-runtime-rendering.js`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/services/viewport-controller.js`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/services/selection-model.js`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
