# 02 Generic Project Storage Skip Preview

## Status

- `Completed`

## Objective

Expose and execute generic project-structure write skip simulation from the Project Structure workflow start dialog.

## Covered Inputs

- `N002`
- `N003`
- `N004`
- Requirements `R003`, `R004`, `R005`, `R006`

## Prerequisites

- `01-oauth-connection-defaults` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowNodeService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.WorkflowNodes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.OverlayStates.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\ProjectStructureWorkflowExecutor.cs`
- `C:\repositories\CanDoItAll\Templates\Workflows\workflows\default-workflows.yaml`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\EmailWorkflowSwitchScenarioTests.cs`

## Deliverables

- Start dialog state includes skippable project-structure write options.
- Start request carries selected simulated node ids.
- Start service builds a `WorkflowPreviewSimulationPlan` for selected `CreateAsset` and `CreateTaskNodes` nodes.
- Project-structure executor falls back to standard Project Structure workflow payload fields when top-level aliases are absent.

## Dependency Impact

- This is the final user-visible subbundle. Weak proof here invalidates the requested start-dialog behavior and the inventory of similar workflow cases.

## Validation Depth

- Critical UI/runtime foundation.

## Implementation Steps

1. Add contract/state fields for simulation options and selected simulated node ids.
2. Analyze workflow definitions generically for project-structure write nodes.
3. Render checkbox controls in the Project Structure workflow start dialog.
4. Pass selected node ids into `ProjectStructureWorkflowNodeStartInput`.
5. Build the runtime `WorkflowPreviewSimulationPlan`.
6. Relax missing configured project/node JSON path handling to allow standard project-structure input fallback.
7. Add tests and browser proof.

## Scope Exceptions

- No new workflow-template-specific switches. Generic executor/operation detection is required.

## Do Not Do

- Do not hard-code Office365, Gmail, or default workflow keys.
- Do not skip project-structure writes unless the user selects the preview simulation option.

## Acceptance Checklist

- Office365 summary workflow exposes a skip option for `store-office365-summary`.
- Gmail summary workflow remains covered by the same generic mechanism.
- `email-task-creation-router` style `CreateTaskNodes` writes are also skippable.
- Selected skip options reach runtime and prevent the real project-structure executor from running for those nodes.

## Proof Required

- Targeted component/integration tests.
- Browser proof on Project Structure start dialog showing the skip option.
- Build after code changes.

## Browser Validation Logging

- Route: Project Structure page for a project with a workflow definition node.
- Viewports: large desktop; narrower follow-up only if dialog layout changes materially.
- Actions: open start workflow dialog, verify simulation checkbox text, toggle, start or inspect request path where possible.
- Screenshots: record under `codex/bundles/oauth-email-workflow-defaults/evidence/` if captured.
- Review questions: option readable, no clipping, no overlap, action buttons still accessible.

## Progression Gate

- Bundle may close only after test and browser evidence prove generic skip options are visible and selected options affect runtime.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add generic project-structure write simulation options to the Project Structure start dialog and runtime start request, preserve existing workflow behavior when no option is selected, and validate with targeted tests plus browser proof.
```
