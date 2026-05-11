# Follow-up Current State

## Reopened Scope

- `WorkflowsPage` is still a single long page with summary cards, catalog/detail/test-run/runs/artifacts/pending panels, followed by the canvas editor.
- `WorkflowCanvasEditor` exposes toolbox and node selection in the supporting panel, not as floating windows inside the canvas.
- Node settings are mostly edited in the stage inspector. `CanvasWorkbench.NodeOpened` exists but is not wired by the workflow editor.
- `CanvasWorkbench.OpenCreateDialogAsync` can open the existing canvas create composer when an action has `RequiresInput = true`, but workflow create actions currently create nodes directly.
- The project-structure canvas already uses `CanvasFloatingWindow` and `OverlayComponentToolbox` through `ProjectStructureToolboxWindow`, plus toolbar toggles for floating windows.
- The workflow API supports settings, definitions, validation, components, test runs, runs, events, artifacts, pending requests, and external-request responses. Compared with process APIs, workflow APIs are missing executor catalog, backend catalog, analytics/dashboard summary, templates/examples, explicit run start/cancel routes, and direct artifact lookup/filter helpers.
- Workflow catalog/run storage is currently in-memory in the module service registration. PostgreSQL can store the wider app/project/process data, but workflow definitions and runs are not yet durable across app restarts unless a workflow persistence layer is added or the testing instance is seeded after startup through APIs.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolboxWindow.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\SwitchableAppDbContextFactory.cs`

## Reopen Triggers

- If workflow definitions remain only in-memory, the PostgreSQL scenario proof must explicitly document that the workflow seed is an API-level test-instance seed rather than a durable database seed.
- If Playwright cannot open and verify floating windows and modals, the UI subbundle cannot close.
- If the workflow API cannot support observer-style scenario control without UI-only clicks, the API subbundle must stay open.
- If project-structure executor live mutation cannot be proven against seeded projects, the scenario subbundle must stay open or document the exact host-service blocker.
