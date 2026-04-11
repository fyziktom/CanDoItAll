# Current State

## Live Repo Findings

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs` already contains branch-related authoring fields such as `DependsOnBranchOutcomeId` and `DecisionRoleRequirementId`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs` and `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs` already track selected branch outcomes at runtime and enforce branch selection on completion.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs` already has branch-aware authoring helpers like `AddRoutedStepFromSelectedStepAsync(Guid? branchOutcomeId)`, but the canvas surface still hides those semantics behind plain links.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs` currently collapses process dependencies into `CanvasWorkbenchLink` records with only `SourceId` and `TargetId`, so branch outcomes only appear as labels or chips instead of real connectable ports.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchNode.cs` and `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchSurface.cs` currently model nodes and links too simply for named input and output ports.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs` and the runtime renderer scripts under `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench` still resolve links around whole-node anchor points, not per-port geometry.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.Scenarios.cs` currently contains useful software-delivery scenarios, but they are still mostly linear and do not yet demonstrate the requested review and QA loops.

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

- The process domain already understands branch outcomes.
- The user-visible process canvas does not.
- The shared workbench model needs an additive multi-port node and link contract before process-specific branch-node authoring can behave as requested.
