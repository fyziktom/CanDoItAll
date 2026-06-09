# SB007 UI Inventory

## Status
Completed.

## Scope
SB007 inventories the global `/processes` route and proves that the existing UI can reach template selection/import, launch-plan creation, ready launch execution, and run selection.

## Source-Backed Route Map
| Surface | Source | Behavior |
| --- | --- | --- |
| Global route | `repo://src/CanDoItAll.Modules.Processes/Pages/ProcessesPage.razor` | Maps `@page "/processes"` directly to `ProcessWorkspace`. |
| Project route | `repo://src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor` | Maps project-scoped process workspace through `ProcessWorkspace ProjectId`. |
| Shell navigation | `repo://src/CanDoItAll.Web/Composition/ShellNavigation.cs` | Exposes `Processes` at `/processes` and `Live Processes` at `/processes/live`. |
| Query parameters | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs` and `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Launch.cs` | Supports `processId`, `runId`, and `launchPlanId` route state. |
| Template entry point | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` | Opens `ProcessTemplateLibraryDialog` through `processes-templates-button`. |
| Template import | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.TemplateLibrary.cs` | `AddProcessTemplateAsync` creates a process import envelope and calls `ProcessesService.ImportAsync`. |
| Runs tab | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor` | Hosts the launch, activity, control, execution, graph, coordination, and evidence subviews. |
| Launch planning UI | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsLaunchSection.razor` | Creates launch plans and exposes approval, provisioning, and ready-launch execution actions. |
| Launch presenter | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RunsPresenter.cs` | Forwards UI actions to `ProcessWorkspace` methods. |
| Launch service calls | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Launch.cs` | Calls `CreateLaunchPlanAsync`, `SubmitLaunchPlanForApprovalAsync`, `ProvisionLaunchPlanAsync`, and `ExecuteLaunchPlanAsync`. |
| Runtime execution path | `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.cs` | Executes a ready launch plan by delegating to the normal process run start path. |

## Browser Proof
Focused Playwright proof used a 1900 x 1200 viewport and navigated to `/processes`.

| Step | Evidence |
| --- | --- |
| Template selected | `bundle://proof/SB007/screenshots/01-template-selected-large-desktop.png` |
| Runs tab before launch | `bundle://proof/SB007/screenshots/02-runs-tab-before-launch-large-desktop.png` |
| Launch plan created | `bundle://proof/SB007/screenshots/02-launch-plan-created-large-desktop.png` |
| Run selected | `bundle://proof/SB007/screenshots/03-run-selected-large-desktop.png` |

## Validation
- Playwright test passed: `bundle://proof/SB007/transcripts/global-processes-ui-playwright.txt`
- Source assertions passed: `bundle://proof/SB007/transcripts/global-processes-ui-source-assertions.txt`
- Anti-stub/runtime-host drift scan passed: `bundle://proof/SB007/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No transient bundle path scan passed: `bundle://proof/SB007/transcripts/no-transient-bundle-path-scan.txt`
- No unexpected UI/media source drift scan passed: `bundle://proof/SB007/transcripts/no-unexpected-ui-media-drift-scan.txt`

## Changed Files
SB007 made no production source changes and no long-lived test source changes. It added only proof artifacts under `bundle://proof/SB007` and updated bundle execution documentation.

## Risk
The browser proof exercises the global route only, as scoped by SB007. Project-scoped process launch proof is deferred to SB010-SB012.
