# 02 Wire Selection Panel Launch Buttons And Tests

## Objective

Expose the runtime-launch actions in the selection panel and prove the final behavior with focused regression coverage.

## Covered Inputs

- `N001`
- `N002`
- `R001`
- `R003`
- `R005`
- `R006`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkspaceRuntimeProcessToolsTests.cs`

## Deliverables

- two selection-panel launch buttons for eligible nodes
- explicit success or failure feedback after launch attempts
- focused regression coverage for eligibility and command derivation

## Implementation Steps

1. Ask the runtime-launch service whether the selected node is launchable.
2. Render normal and elevated PowerShell actions in the existing `Node actions` area only for launchable nodes.
3. Surface explicit launch feedback without disturbing other inspector interactions.
4. Add focused tests that prove the buttons appear only when expected and that launch plans reflect node settings.

## Do Not Do

- do not introduce a second inspector action surface outside the existing node-actions card
- do not show disabled launch buttons on unsupported nodes with no explanation path
- do not break existing command buttons, graph actions, or attachment-local-open messaging

## Acceptance Checklist

- launchable runtime nodes show exactly two PowerShell actions in the selection panel
- unsupported nodes do not show the launch actions
- launch attempts surface actionable feedback
- focused tests cover both UI visibility and launch-plan behavior

## Proof Required

- focused automated test pass
- execution report updated with the command and result

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Wire the runtime-launch service into the selection panel so eligible nodes show normal and elevated PowerShell actions in the existing Node actions card. Preserve the rest of the inspector behavior and add focused automated coverage for button visibility and launch-plan behavior.
```
