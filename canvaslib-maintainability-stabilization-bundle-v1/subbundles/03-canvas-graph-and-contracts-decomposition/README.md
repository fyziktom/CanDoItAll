# Canvas graph and contracts decomposition

## Status

- `Completed`

## Objective

- Reorganize CanvasLib graph classes into topic folders and split workbench contract models into smaller coherent files without changing serialization defaults, event contracts, or runtime behavior.

## Covered Inputs

- `N003 too large files, too many files in one folder are not ok`
- `N005 organize Canvas.Graph folder`
- `N006 split CanvasWorkbenchContracts.cs`
- `R004 Canvas Graph Folder Reorganization`
- `R005 Large File Decomposition`

## Prerequisites

- `subbundles/01-asset-ownership-and-duplicate-retirement` must be closed and trusted
- `subbundles/02-canvaslib-component-topology-reorganization` should be reviewed for any consumer-path assumptions before closing this phase

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Graph`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Calendar`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Calendar\CanvasCalendarContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\CanvasAdapters\PromptFactorySessionGraphAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- Topic-based `Canvas\Graph` subfolders
- Split workbench contract and event files with preserved namespace and semantics
- Updated graph and contract consumer compile surface

## Dependency Impact

- Final closure depends on this phase because graph classes and workbench contracts back the shared browser behaviors. Weak proof here would invalidate any later UI pass that appears green by accident.

## Validation Depth

- `Critical behavioral foundation`

## Implementation Steps

1. Define a graph-class folder taxonomy that separates primitives, overlays, interaction, and state or support services.
2. Move the graph classes into those folders with minimal consumer edits.
3. Split `CanvasWorkbenchContracts.cs` into coherent files grouped by surface models, chrome or actions, state models, and event records.
4. Preserve existing namespaces, defaults, and JSON behavior.
5. Re-run builds, tests, and browser behaviors that exercise the split contracts.

## Scope Exceptions

- If a nearby file is large but unrelated to graph or workbench contracts, record it for follow-up instead of broadening scope.

## Do Not Do

- Do not change the meaning of serialized properties, default values, or record shapes.
- Do not redesign consumer APIs.
- Do not take unrelated module-level large-file cleanup into this phase.

## Acceptance Checklist

- `Canvas\Graph` no longer has the current flat 29-file root.
- `CanvasWorkbenchContracts.cs` is replaced by smaller coherent files.
- CanvasLib compiles and consumer modules still build against the moved types.
- Shared browser flows using workbench state and action contracts still work.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- Targeted Playwright proof on the structure and prompt-factory routes
- File-size and folder-density audit for the graph and contract surface

## Browser Validation Logging

- Routes:
  - `/projects/{projectId}/structure`
  - `/prompt-factory`
- Viewports:
  - `1900x1200`
  - `1600x900`
- Required Playwright proof:
  - load the workbench shell
  - exercise selection or context action behavior that depends on workbench models
  - confirm prompt-factory canvas still renders its graph-backed surface
- Screenshot review:
  - no blank node cards
  - no broken overlays
  - no obvious state-loss or render failure

## Progression Gate

- Final closure may continue only after the build and component tests pass, browser proof passes, and the file split audit confirms the graph root and workbench contract file were actually decomposed.

## Suggested Agent Prompt

```text
Implement only the Canvas graph and contracts decomposition phase.
Reorganize graph classes into coherent folders, split CanvasWorkbenchContracts.cs without changing behavior, and prove the shared workbench routes still behave correctly.
```
