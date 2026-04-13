# Process canvas integration and managed SQLite application

## Status

- `Completed`

## Objective

- Use the shared recomposition foundation to deliver a process-specific fishbone-aware recomposition flow, wire it into the process definition canvas, persist the results, and apply it to the managed SQLite workspace through the product path.

## Covered Inputs

- `N003` Remove overlapping process nodes.
- `N004` Distinct `Collisions`, `Add Space Around`, and smarter `Recomposition` behaviors.
- `N006` Reuse shared parts while keeping process semantics local.
- `N008` Apply the result to the managed SQLite workspace.

## Prerequisites

- `subbundles/02-shared-canvaslib-recomposition-engine-and-menu-contract` must be `Completed` and trusted.
- A real process definition with visible overlap or crowding must be identified in the managed SQLite workspace before closure proof starts.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolbarActions.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.Coordinates.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEditorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Publication.cs`
- `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db`

## Deliverables

- Process definition canvas support for the three recomposition commands.
- A smarter process recomposition strategy that respects mainline sequencing, roles, and branching.
- Persisted node coordinates written through the existing process workflow.
- Proof that the managed SQLite workspace definitions can be recomposed and reopened with clearer layouts.

## Dependency Impact

- `subbundles/04` depends on this phase for all meaningful browser and database closure proof.
- Weak proof here would leave the bundle with shared math but no validated user outcome.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Map process definition nodes into the shared recomposition input model.
2. Implement process-smart recomposition that preserves a readable mainline and lays out branches and roles without collisions.
3. Wire the three commands into the process definition toolbar and menu flow.
4. Persist planned moves through the existing process-definition update path.
5. Use the managed SQLite workspace to execute the recomposition commands on a real definition.
6. Reopen the affected definition and verify the clearer layout persists.

## Scope Exceptions

- Full migration of project-structure recomposition to the new shared contract is out of scope.

## Do Not Do

- Do not solve smart process recomposition by exploding nodes into arbitrary free space with no narrative structure.
- Do not update `candoitall.db` directly as the implementation mechanism.
- Do not claim success without reopening the persisted definition.

## Acceptance Checklist

- `Collisions` removes overlap with minimal movement.
- `Add Space Around` noticeably increases spacing between neighboring nodes.
- `Recomposition` produces a clearer fishbone-style process map with a readable mainline.
- Reopened definitions retain the recomposed coordinates.

## Proof Required

- Focused automated tests for process-specific recomposition behavior where practical.
- Browser before and after screenshots of the real definition canvas.
- Database verification showing persisted coordinates changed for the exercised definition.
- An execution-report note identifying the process definition used for proof.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `1600x900`
- Required Playwright actions:
  - navigate to `/processes`
  - open the managed SQLite-backed process definition that exhibits overlap
  - capture a baseline screenshot
  - execute `Collisions`
  - execute `Add Space Around`
  - execute `Recomposition`
  - capture after screenshots
  - reopen the definition if needed to confirm persistence
- Expected evidence paths:
  - `C:\repositories\CanDoItAll\output\playwright\process-recomposition\02-before-overlap.png`
  - `C:\repositories\CanDoItAll\output\playwright\process-recomposition\03-collisions.png`
  - `C:\repositories\CanDoItAll\output\playwright\process-recomposition\04-add-space-around.png`
  - `C:\repositories\CanDoItAll\output\playwright\process-recomposition\05-recomposition.png`
- Screenshot review questions:
  - Did collisions actually disappear?
  - Is `Add Space Around` visually distinct from `Collisions`?
  - Does the smarter recomposition preserve a readable process flow?

## Progression Gate

- `subbundles/04-browser-proof-database-verification-and-closure` may continue only after a real process definition in the managed SQLite workspace has been recomposed, reopened, and backed by database verification.

## Suggested Agent Prompt

```text
Implement this subbundle only. Use the shared recomposition contract to add process-specific collision removal, spacing expansion, and smarter fishbone-style recomposition to the definition canvas, persist the results through the existing product path, apply the feature to the managed SQLite workspace, and prove the layout survives reopen before closing the phase.
```
