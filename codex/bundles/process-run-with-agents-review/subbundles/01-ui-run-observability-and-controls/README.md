# 01 UI Run Observability And Controls

## Status

- `Implemented and validated`

## Objective

Make Process Workspace a reliable operator console for agent-backed process runs by exposing run health, step health, execution attempts, recovery state, and available actions in the UI.

## Covered Inputs

- REQ-001: UI launch path remains visible and usable.
- REQ-002: UI exposes actionable run states.
- REQ-003: UI exposes execution attempts and governed/raw state.
- REQ-012: Existing component patterns are preserved.

## Prerequisites

- `process-run-with-agents-fix` implementation is present.
- Current merge state is understood before implementation begins.
- Existing Process Workspace component tests are runnable.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsLaunchSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsActiveSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsLifecycleSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsExecutionSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsArtifactsSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsCanvasSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasSelectionPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeViewModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProjectProcessesPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- A run health summary that combines process status, active execution count, latest attempt status, waiting approvals, blocked/failed steps, and recovery/dead-letter flags supplied by later subbundles.
- A step attempt ledger in the selected run view showing latest and historical technical executions per step.
- Clear UI distinction between raw AgentFramework success and governed process blocked/failed state.
- UI actions remain explicit: manual status transition controls stay, but do not masquerade as agent retry controls.
- Focused component tests for rendering active, blocked, failed, retrying, waiting approval, and no-execution states.

## Dependency Impact

- Unlocks subbundles 02, 03, and 04 because those need a stable operator read model.
- Incorrect health aggregation will mislead the browser E2E proof in subbundle 05.

## Validation Depth

- Component tests for Process Workspace run tabs and runtime canvas selection.
- Focused service/read-model tests if new view models are added.
- No browser proof required in this subbundle unless implementation changes layout substantially.

## Implementation Steps

1. Inventory existing run, step, execution, artifact, and active-agent view models.
2. Add minimal read-model fields needed for operator health without adding retry/outbox/artifact-policy behavior yet.
3. Update Activity and Execution UI to display attempt counts, latest attempt status, governed state, and actionable reason.
4. Update runtime canvas selected-step panel to show the same health summary.
5. Add component tests for each status family.
6. Update this README and execution report with proof.

## Do Not Do

- Do not implement missing artifact recovery in this subbundle.
- Do not implement manual agent rerun in this subbundle.
- Do not add a separate process operations page.
- Do not weaken `ProcessExecutionRunDisplayProjector` governed status behavior.

## Acceptance Checklist

- Active run UI shows active agents and latest attempt status.
- Blocked/failed step UI shows the process reason and raw execution status when available.
- A selected step displays historical attempts, not only the latest execution.
- Runtime canvas selection and run tab agree on selected step health.
- Existing launch plan workflow still works.

## Proof Required

- Focused component tests under `tests\CanDoItAll.Tests.Components`.
- Focused runtime read-model tests if applicable.
- Screenshots optional unless layout changes are broad.

## Closure Proof

- Added run and step health summaries, attempt ledgers, raw/governed status context, and selected-step runtime health in Process Workspace.
- Passed `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessCanvasSelectionPanelTests"` with 5 tests.
- Passed `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"` with 3 tests.

## Browser Validation Logging

- Not required for closure of this subbundle.
- If layout changes are substantial, capture `/processes` and `/projects/{projectId}/processes` desktop screenshots.

## Progression Gate

- Subbundles 02, 03, and 04 may proceed only after operator health state is visible in Process Workspace and covered by component/read-model tests.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Add the smallest UI/read-model changes needed for Process Workspace to show agent-backed run health, latest and historical execution attempts, governed-vs-raw status, and actionable blocked/failed reasons. Do not implement artifact recovery, outbox operations, or rerun commands yet.
```
