# Process workspace shell and tab containment

## Status

- `Completed`

## Objective

- Make the `/processes` workspace behave like a bounded workbench: summary tiles stay above the shell, the process-definition list scrolls inside its pane, and the detail tabs fill the remaining height and scroll within their own selected panel.

## Covered Inputs

- `Use components MCP and Chat page example for fit-to-window containment.`
- `Make Process definitions cards list scrollable inside.`
- `Same for content of tabs.`

## Prerequisites

- Bundle readiness gate passed for `cdi_process_workspace_containment_bundle`.
- Confirm the referenced Chat page pattern and BaseLib component usage are still valid in the repo.

## Exact Source References

- `C:\repositories\CanDoItAll.AgentFramework\src\CanDoItAll.AgentFramework.Sandbox\Components\Pages\Chat.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Lists\ListDetailShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- Processes workspace uses a fill-height page shell.
- Child content is arranged so `ListDetailShell` consumes the remaining viewport height.
- The definition list uses an internal scroll wrapper.
- The detail tabs are configured for fill-height panel overflow behavior.
- Targeted component or browser assertions cover the new containment contract.

## Dependency Impact

- Subbundle 02 depends on this phase because the modal should follow the same bounded-height containment model. If the page-shell contract is still wrong, later screenshots can hide the same regression in a different surface.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Enable the processes workspace page shell to fill the available height.
2. Wrap the body content in a remaining-height container so summary surfaces stay fixed above the shell.
3. Add an internal scroll wrapper for the process-definition list pane.
4. Configure the detail tabs for fill-height and panel overflow.
5. Extend targeted tests for the containment markup contract if the change is not already covered indirectly.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not replace `ListDetailShell` with a custom split layout.
- Do not redesign the process workspace information architecture.
- Do not move modal-specific changes into this phase.

## Acceptance Checklist

- `/processes` uses a bounded-height workspace shell.
- The definition list has an explicit internal scroll region.
- The selected detail tab panel is height-bounded and scrollable inside the pane.
- No new document-level overflow is introduced by the layout change.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`
- Browser pass on `/processes` at large-screen width with screenshot review of the open workspace before moving to modal work.
- Record at least one screenshot path under `output/playwright/process-workspace-containment/`.

## Browser Validation Logging

- Route: `/processes`
- Viewports: desktop first, then one narrower follow-up width if the shell changes affect layout wrap
- Required actions: navigate, create or load a definition, inspect the list pane, switch at least two tabs, capture screenshot
- Expected artifacts: `output/playwright/process-workspace-containment/01-processes-workspace-shell.png`
- Required review answers: list pane stays bounded, tab panel stays bounded, nothing overlaps or clips, space is used intentionally

## Progression Gate

- Downstream work may continue only after the `/processes` workspace visibly keeps the definition list and detail tabs inside the viewport-height shell in browser proof, and the targeted component test run passes.

## Suggested Agent Prompt

```text
Implement only the processes workspace shell and tab containment fix.
Keep the change inside ProcessWorkspace.razor and the smallest necessary supporting tests.
Use the AgentFramework sandbox Chat page as the containment reference pattern.
```
