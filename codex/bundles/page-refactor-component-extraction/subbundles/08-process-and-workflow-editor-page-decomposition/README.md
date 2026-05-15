# Process And Workflow Editor Page Decomposition

## Status

- `Ready`

## Objective

- Decompose large process and workflow editor surfaces into focused components where the inventory shows page-sized markup.

## Covered Inputs

- `N001`
- `N002`
- `R009`

## Prerequisites

- `03-prompt-factory-canvas-helpers` completed if shared canvas helper assumptions are reused.
- Components MCP retried, or local component guidance fallback recorded.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- Split markup-only editor regions into focused components with typed parameters.
- Preserve workflow canvas and process workspace callbacks.
- Avoid duplicating shared canvas logic.

## Dependency Impact

- Final route proof depends on this phase for workflows and process workspace routes.

## Validation Depth

- UI decomposition with component-test and browser-proof.

## Implementation Steps

1. Identify workbook rows assigned to this subbundle.
2. Extract one workflow/process region at a time.
3. Preserve callbacks, selected items, and test ids.
4. Run process/workflow component tests.
5. Capture route browser proof.

## Scope Exceptions

- Do not change workflow execution semantics.
- Do not refactor process services.

## Do Not Do

- Do not add one-off CSS/layout wrappers when BaseLib/CanvasLib components fit.

## Acceptance Checklist

- Large editor components are smaller and more focused.
- Process/workflow component tests pass.
- Routes render without visual regressions.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProcessWorkspace`
- Browser proof on `/agents/workflows` and `/processes` where edited.
- Screenshots at `1600x900`.

## Browser Validation Logging

- Routes: `/agents/workflows`, `/processes`.
- Viewport: `1600x900`.
- Required actions: navigate, inspect editor/workspace regions, trigger changed callbacks where feasible, screenshot.

## Progression Gate

- Edited workflow/process surfaces pass tests and browser proof before final closure.

## Suggested Agent Prompt

```text
Implement subbundle 08 only. Decompose process/workflow editor markup into focused components, preserve callback wiring and test ids, run targeted tests plus browser proof, and update gate rows.
```
