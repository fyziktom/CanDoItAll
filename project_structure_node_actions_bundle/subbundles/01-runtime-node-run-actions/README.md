# runtime-node-run-actions

## Status

- `Completed`

## Objective

Expose normal and administrator runtime run actions for runtime-capable project-structure nodes in both the double-click quick-action modal and the right-click canvas context menu.

## Covered Inputs

- `N001`
- `N002`
- `REQ-RUN-001`
- `REQ-RUN-002`
- `REQ-RUN-003`
- `REQ-RUN-004`

## Prerequisites

- Bundle preparation gate passes.
- Existing runtime launcher behavior remains trusted.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeQuickActions.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeEditing.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.RuntimeLaunch.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureMenuComposition.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureActionShortcuts.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/ProjectStructureRuntimeLauncherTests.cs`

## Deliverables

- Quick-action modal supports more than one non-edit primary action when runtime launch resolves.
- Runtime nodes show two actions: run normally and run as administrator.
- Right-click node context menu includes both runtime actions for runtime-capable nodes.
- Context action dispatch executes through `LaunchRuntimeAsync(node, false)` and `LaunchRuntimeAsync(node, true)`.
- Tests cover action visibility and dispatch-safe behavior.

## Dependency Impact

- Subbundle 02 reuses the same multi-action quick-action and context-menu pattern for local/IPFS file actions.
- Subbundle 03 depends on the final runtime capability semantics to expose correct MCP/internal-agent metadata.

## Validation Depth

- Critical UI and host-action foundation.

## Implementation Steps

1. Adjust quick-action dialog state/models so runtime nodes can show edit plus normal/admin run choices.
2. Keep `RuntimeLauncher.Resolve(node)` as the runtime-capable test.
3. Add runtime normal/admin actions to `ProjectStructureActionCatalogAdapter.BuildNodeContextActions`.
4. Update context-action dispatch if the new action ids are not already covered by inspector dispatch.
5. Prioritize one runtime action in context menu ordering without hiding the second action.
6. Add or update tests for runtime context-menu action ids and quick-action modal state.

## Scope Exceptions

- No remote agent/MCP host launch in this subbundle.

## Do Not Do

- Do not bypass `ProjectStructureRuntimeLauncher`.
- Do not remove the edit action from the double-click modal.
- Do not hard-code only `Script` and `Environment` if `RuntimeLauncher.Resolve` already answers capability.

## Acceptance Checklist

- Runtime-capable node quick-action modal includes normal run and administrator run.
- Runtime-capable node right-click menu includes normal run and administrator run.
- Non-runtime nodes do not show runtime actions.
- Existing UAC cancellation handling remains unchanged.
- Tests cover the action list.

## Completion Evidence

- Quick-action dialog state now supports secondary actions and runtime nodes receive `Run normally` plus `Run as administrator`.
- Canvas context actions now receive runtime capability flags and expose `runtime:open` and `runtime:admin`.
- Canvas action dispatch routes the two runtime actions through `LaunchRuntimeAsync(node, false)` and `LaunchRuntimeAsync(node, true)`.
- `ProjectStructureActionCatalogAdapterTests` covers runtime action visibility and absence for non-runtime nodes.

## Proof Required

- Targeted component/unit tests for action catalog and quick-action state.
- At least one build or targeted test command covering Workbench compile.
- Browser proof opening the quick-action modal and context menu with both runtime actions visible.
- Host validation note for PowerShell/UAC behavior.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`.
- Viewports: large desktop first; narrower pass only if the dialog/menu layout changes.
- Actions/assertions: navigate, select or double-click a runtime-capable node, assert normal/admin run labels; open right-click menu, assert normal/admin run labels.
- Screenshots: record quick-action modal and context menu paths in `reviews/01-execution-report.md`.
- Review questions: readability, clipping, lateral overflow, z-order, alignment, and no text overlap.

## Progression Gate

- Subbundle 02 may start only after runtime actions are proven in the modal and context menu or the missing proof is documented as a blocker.

## Suggested Agent Prompt

```text
Implement only subbundle 01. Preserve the existing runtime launcher safety model. Add normal/admin runtime actions to the double-click quick-action modal and right-click context menu, then update tests and execution-report proof.
```
