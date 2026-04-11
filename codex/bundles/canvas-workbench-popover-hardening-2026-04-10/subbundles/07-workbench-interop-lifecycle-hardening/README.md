# Workbench interop lifecycle hardening

## Status

- `Completed`

## Objective

- Harden the shared `CanvasWorkbench` JS interop boundary so null or disconnected hosts do not crash the Blazor circuit during after-render synchronization, especially on tab-driven canvas rerenders.

## Covered Inputs

- `N009` Processes Run tab crashes in `selectNodes` because `host` is null
- `N010` test all canvases used in the CanDoItAll app and repair remaining buggy canvas behavior
- `R012` exported workbench runtime methods must tolerate null or disconnected hosts
- `R013` after-render synchronization must avoid stale-host fragility across multiple awaited JS calls

## Prerequisites

- `06-runtime-entry-splitting-and-regression-proof`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`

## Deliverables

- A shared host-state resolver in the workbench runtime API
- A single after-render sync path from the Blazor wrapper into the runtime create/update methods
- Behavior-preserving null-host handling instead of circuit-breaking JS exceptions

## Dependency Impact

- `08-cross-canvas-app-proof-and-blockers` depends on this phase because route-level proof is only meaningful after the shared runtime API and the Blazor wrapper stop breaking the circuit on stale hosts.

## Validation Depth

- `Runtime lifecycle fix with real Processes Run-tab proof`

## Implementation Steps

1. Add a shared workbench-state resolver in `07-runtime-entry.js` so exported runtime methods can reject null or disconnected hosts consistently.
2. Collapse selection, maximize, and optional fit-view synchronization into the same create or update call instead of spreading one render pass across several awaited JS invocations.
3. Keep the public `CanDoItAll.canvasWorkbench` API stable while changing the return contract only where the Blazor wrapper needs a create or update success signal.
4. Prove the fix on the Processes route by exercising `Steps -> Runs -> Definition -> Runs` with clean console capture and a successful managed build.

## Scope Exceptions

- Do not widen this phase into unrelated Prompt Factory or calendar fixes.

## Do Not Do

- Do not change the public surface contract of `CanvasWorkbench` consumers outside the internal create or update success signal.
- Do not hide real runtime defects behind blanket `try/catch` blocks in the exported JS API.
- Do not widen this phase into floating-window rewrites unless the lifecycle proof shows they are the primary defect.

## Acceptance Checklist

- Switching into the Processes `Runs` tab no longer throws in `CanDoItAll.canvasWorkbench.selectNodes`
- The circuit stays connected and floating-window geometry publishing does not cascade-fail from the original exception
- The shared workbench API still preserves current behavior on reachable workbench routes

## Proof Required

- Real browser proof on `/projects/{ProjectId}/processes`
- Clean console capture after `Steps -> Runs -> Definition -> Runs`
- Managed build success on `src\CanDoItAll.Web\CanDoItAll.Web.csproj`

## Browser Validation Logging

- Route under test: `/projects/{ProjectId}/processes`
- Required viewport passes: `1600x900`, then `1280x800`
- Required Playwright actions: load the Processes workspace, switch through `Steps`, `Runs`, `Definition`, and back to `Runs`, inspect runtime state on each canvas tab, and confirm the browser console stays clean
- Expected screenshots: one proof screenshot on the repaired `Runs` canvas with the floating selection window visible
- Required visual review: the canvas host remains connected, the selected runtime node stays mirrored into the workbench state, and the circuit does not drop

## Progression Gate

- Downstream route-matrix proof may continue only after the Processes lifecycle scenario passes without a JS exception or Blazor disconnect.

## Closure Note

- Completed on `2026-04-10`.
- `07-runtime-entry.js` now resolves shared workbench state defensively, and `CanvasWorkbench.razor` performs a single create or update sync per render pass instead of chaining multiple awaited JS calls across a volatile tab-rerender boundary.
- Real Processes proof passed on the `Runs` tab and during `Steps -> Runs -> Definition -> Runs`, with a clean console and a successful managed build.

## Suggested Agent Prompt

```text
Implement subbundle 07 only. Harden the shared CanvasWorkbench interop lifecycle so stale or disconnected hosts cannot break the circuit during after-render synchronization, then prove it on the Processes route.
```
