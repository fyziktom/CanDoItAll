# Workspace and canvas decomposition

## Status

- `Ready`

## Objective

- Break the oversized Process workspace and related canvas orchestration into smaller components and clearer state ownership without pushing domain rules into Razor code.

## Covered Inputs

- `U003` Long-file, maintainability, and modularity concerns.
- `BRQ-013` Workspace and canvas decomposition.
- `F009` Workspace monolith risk.

## Prerequisites

- `12-template-subsystem-and-cross-module-shared-infrastructure-consolidation` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Links.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Actions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.DefinitionCrud.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RuntimeOperations.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessStepEditorFormTests.cs

## Deliverables

- Smaller workspace/canvas components or presenters with clearer responsibility boundaries.
- A clearer state holder, presenter, or equivalent orchestration layer for workspace state.
- Preserved UI behavior with updated component tests and browser proof.

## Dependency Impact

- Schema hygiene and final closure depend on the workspace being materially easier to reason about.
- Gate D will inspect whether this was a true decomposition rather than a partial-file shuffle.

## Validation Depth

- `High with mandatory browser proof`

## Implementation Steps

1. Identify the largest responsibility clusters inside `ProcessWorkspace` and split them into smaller components or presenters/state holders.
2. Keep domain rules in services/state holders rather than in markup event handlers.
3. Update component tests to cover the extracted surfaces.
4. Run large-screen and narrower-width browser proof on `/processes` and review the screenshots explicitly.

## Scope Exceptions

- This phase does not redesign the product UI from scratch.
- Minor markup churn is allowed only when it directly supports the decomposition or fixes obvious layout regressions.

## Do Not Do

- Do not move domain logic into Razor to make files shorter.
- Do not replace one big component with one big state manager.
- Do not skip browser proof because component tests pass.

## Acceptance Checklist

- The workspace/canvas surface is materially easier to navigate and reason about.
- Extracted components or presenters have coherent ownership.
- Existing workspace/canvas behaviors remain covered by tests.
- Browser proof shows the UI still reads and behaves coherently on large and narrow viewports.

## Proof Required

- Focused component tests for the extracted workspace/canvas behavior.
- Real browser proof on `/processes` with large-screen and narrower-width screenshots.
- Execution-report screenshot review notes answering readability, clipping, spacing, alignment, and hierarchy questions.

## Browser Validation Logging

- Route: `/processes`.
- Viewports: at minimum `1600x900` and `430x932`.
- Actions: open the definition/steps surface, exercise the touched workspace areas, and if runtime UI changed, inspect the runs surface too.
- Screenshots: capture both viewports and review them explicitly in the execution report.

## Progression Gate

- The workspace is materially decomposed, the UI remains coherent under browser proof, and domain logic ownership is clearer rather than more diffuse.

## Suggested Agent Prompt

```text
Implement only subbundle 13. Decompose the oversized Process workspace and canvas surfaces into smaller components or state/presenter layers, keep domain logic out of Razor code, update component tests, capture real browser proof on `/processes`, and stop before schema cleanup or final closure.
```
