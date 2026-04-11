# Current State

## Live Repo Findings

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs` already contains branch-related authoring fields such as `DependsOnBranchOutcomeId` and `DecisionRoleRequirementId`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs` and `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs` already track selected branch outcomes at runtime and enforce branch selection on completion.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs` already has branch-aware authoring helpers like `AddRoutedStepFromSelectedStepAsync(Guid? branchOutcomeId)`, but the current connection-authoring flow in the live UI still expects right-click draft initiation and still overwrites a single upstream dependency on the target step.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs` now projects router and role nodes, but the live screenshot shows that some rendered connector circles still follow generic edge placement instead of the actual badge positions and one router badge is missing its circle.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchNode.cs`, `CanvasWorkbenchAnchorPorts.cs`, and the runtime renderer scripts under `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench` now support additive named ports, but the latest feedback reopens interaction and geometry correctness rather than the existence of the port contract itself.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs` and `ProcessWorkspace.Canvas.cs` store manual positions for derived nodes in in-memory canvas UI state, not obviously in canonical persisted process data, which is consistent with the reported snap-back after later interactions.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs` still models only one `DependsOnStepId` plus one optional `DependsOnBranchOutcomeId` per step, which is likely insufficient for the user’s requested many-to-many join behavior.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.Scenarios.cs` now contains a realistic branching review scenario, but it still needs explicit coverage for join-style inputs and persisted-position proof.

## Existing Browser Surface

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor` exposes the main browser route at `/processes`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProjectProcessesPage.razor` exposes a project-scoped route at `/projects/{ProjectId:guid}/processes`.
- The managed dotnetwatch session already reports a healthy app host, so this bundle can execute real Playwright validation without first repairing the local startup environment.

## Relevant Tests Already Present

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSelectionPanelTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessStepEditorFormTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ConnectorAnchorOverlayTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ConnectorPathPrimitiveTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Gap Summary

- The shared workbench now understands named ports, but the user-visible authoring flow still does not satisfy the requested left-click gesture or exact badge-anchor alignment.
- The process domain understands simple fan-out routing, but it does not yet clearly support many-to-many joins or aggregated inputs.
- Derived node placement appears to be authorable in the browser without being canonically persisted, which makes current browser proof insufficient for closure.

## Working Assumptions

- If many-to-many joins can be added without a broad rewrite, they should be modeled as explicit strongly typed dependency edges rather than more single-value shortcut fields.
- If persisted layout for derived nodes already exists elsewhere in the module, the current snap-back behavior is a synchronization bug rather than a missing schema.
- `/processes` remains the correct primary validation route for both interaction proof and persistence proof.

## Critical Path Risks

- Changing connector initiation from right click to left click may conflict with node selection, drag, or editor affordances if the shared pointer router is not adjusted carefully.
- True many-to-many joins may require a canonical process-model change that reaches beyond the canvas adapter and into runtime evaluation.
- Persisted role or router placement may require touching module data contracts or save flows rather than only canvas runtime code.

## Validation Risks

- A screenshot can make circles appear aligned even when the DOM geometry is still off by several pixels, so exact element-position checks may be needed alongside visual review.
- A one-time move proof is weak; the reported snap-back requires validation after a second interaction such as double-click, editor open, or surface rebuild.
- Many-to-many support is not proven by drawing two curves into one port unless the persisted data and reloaded surface preserve both edges.

## Reopen Triggers

- If the current process model cannot store more than one upstream dependency for the same logical input without hacks, reopen the architecture and record the exact blocker before claiming join support.
- If left-click connector authoring causes accidental drag or selection regressions on normal nodes, reopen the shared interaction subbundle.
- If moved role, router, or other derived nodes still reset after editor interactions or refresh, reopen the persistence subbundle and keep the workflow open.
