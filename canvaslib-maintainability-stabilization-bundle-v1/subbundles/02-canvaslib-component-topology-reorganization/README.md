# CanvasLib component topology reorganization

## Status

- `Completed`

## Objective

- Move CanvasLib Razor components into topic-based subfolders that match the shared canvas feature surface while preserving namespace, build behavior, and consumer rendering.

## Covered Inputs

- `N003 too many files in one folder are not ok`
- `N004 organize CanvasLib Components into sub folders`
- `R003 CanvasLib Component Folder Reorganization`

## Prerequisites

- `subbundles/01-asset-ownership-and-duplicate-retirement` must be closed and trusted

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\_Imports.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Canvas`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- Topic-based subfolders under `Components`
- Preserved component namespaces and consumer compatibility
- Updated folder-density audit for the CanvasLib component surface

## Dependency Impact

- The graph and contract decomposition phase shares the same consumer routes and test surfaces. If component moves break discovery or rendering, later proof becomes noisy and misleading.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Define the topic-based folder taxonomy for CanvasLib Razor components.
2. Move the component files into those folders with the smallest viable set of edits.
3. Preserve namespaces and consumer imports through the existing `_Imports.razor` contract or explicit namespace directives where required.
4. Update any tests or file-path assumptions affected by the moves.
5. Re-run build and browser proof against real shared-canvas routes.

## Scope Exceptions

- Split component code-behind only when the move requires it. Do not turn this phase into an unrelated behavioral rewrite.

## Do Not Do

- Do not change graph model behavior in this phase.
- Do not redesign component parameters or semantics.
- Do not split large C# contract files here unless a trivial consumer fix is required.

## Acceptance Checklist

- CanvasLib component root no longer contains the current flat 40-file surface.
- The new folders reflect coherent topics such as calendar, workbench, graph interaction, graph overlays, and graph primitives.
- Build and browser routes prove that consumer modules still resolve and render the moved components.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- Browser proof on shared-canvas routes that render moved components
- Folder-density audit command comparing the old flat root versus the new grouped layout

## Browser Validation Logging

- Routes:
  - `/projects/{projectId}/structure`
  - `/prompt-factory`
  - `/projects/{projectId}/calendar`
- Viewports:
  - `1900x1200`
  - `1280x800`
- Required Playwright proof:
  - open the shared workbench shell
  - trigger at least one context menu or toolbar interaction
  - confirm calendar and prompt-factory routes still render with the moved components
- Screenshot review:
  - toolbar and shell chrome present
  - overlays visible
  - no broken or blank component surfaces

## Progression Gate

- Downstream work may continue only after the web build passes, component tests pass, browser route proof passes, and the folder-density audit shows the component root was actually reduced.

## Suggested Agent Prompt

```text
Implement only the CanvasLib component topology reorganization.
Move Razor components into coherent subfolders while keeping namespaces and behavior stable, then prove the shared routes still render.
```
