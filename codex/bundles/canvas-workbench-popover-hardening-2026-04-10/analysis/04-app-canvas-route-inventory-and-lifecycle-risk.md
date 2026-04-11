# App Canvas Route Inventory And Lifecycle Risk

## Reachable CanDoItAll App Surfaces

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  Route: `/projects/{ProjectId}/structure`
  Surface: shared `CanvasWorkbench` plus floating windows.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
  Routes: `/processes` and `/projects/{ProjectId}/processes`
  Surface: shared `CanvasWorkbench` in the `Steps` and `Runs` tabs plus floating selection and toolbox windows.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`
  Route: `/projects/{ProjectId}/calendar`
  Surface: `CanvasCalendar`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
  Route: `/prompt-factory`
  Intended surface: shared `CanvasWorkbench` plus floating toolbox windows.

## Lifecycle Finding

- The failing stack from the user report lands in `CanDoItAll.canvasWorkbench.selectNodes` while `CanvasWorkbench.OnAfterRenderAsync` is synchronizing surface state.
- The runtime entrypoint still exported several methods that dereferenced `host.__canvasWorkbenchState` without tolerating a null or disconnected host.
- `CanvasWorkbench.OnAfterRenderAsync` also spread one render pass across multiple awaited JS calls (`create or update`, `setMaximized`, `fitView`, `selectNodes`), which increases the chance of using a stale ElementReference between awaits during tab switches or rerenders.

## Concrete Risk

- `ProcessWorkspace` reuses the same `CanvasWorkbench` component contract across `Steps` and `Runs`.
- The `Runs` tab can carry a preselected runtime node, so the after-render selection sync happens immediately on a tab activation path that is already changing the rendered subtree.
- If the host resolves to `null` on any of those awaited JS calls, Blazor loses the circuit and floating-window geometry publishing starts failing secondarily.

## Scope Decision

- Fix the shared `CanvasWorkbench` interop lifecycle in the runtime entrypoint and the Blazor wrapper.
- Re-prove the real app routes that actually exercise CanvasLib.
- Do not widen this bundle into the unrelated Prompt Factory server failure unless the missing manifest issue proves to be introduced by the canvas refactor itself.

## Route-Proof Status

- `ProjectStructurePage`: reachable and canvas proof available.
- `ProcessWorkspace` `Steps` and `Runs`: reachable and canvas proof available.
- `ProjectCalendarPage`: reachable and calendar proof available.
- `PromptFactoryPage`: blocked by an unrelated `InvalidOperationException` for missing `output/prompt-library/manifest.json`.
