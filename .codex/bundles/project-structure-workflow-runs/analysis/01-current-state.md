# Current State

## Platform And App Model

- `C:\repositories\CanDoItAll\global.json` pins SDK `10.0.200`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj` targets `net10.0` and hosts the Blazor/web API app.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanDoItAll.Modules.Workbench.csproj` targets `net10.0` and contains project-structure canvas UI.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj` targets `net10.0` and contains workflow runtime contracts.

## Existing Project-Structure Canvas Path

- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\Projects\ProjectObjectContracts.cs` defines `ProjectObjectType`; it currently includes `ProcessDefinition` and `ProcessRun`, but no explicit workflow object type.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.cs` owns create catalog groups/actions for project-structure nodes.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs` builds right-click/context actions. It adds `start-process` for `ProjectObjectType.ProcessDefinition` and `add-process` for eligible non-process nodes.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs` builds inspector actions and dispatches `add-process` and `start-process` to process-specific dialog methods.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.OverlayStates.cs` defines process add/start dialog state records and is the natural location for workflow add/start dialog state if not extracted.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` wires the project-structure dialogs, action execution, and selection panel.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructureSelectionPanel.razor` already shows progress, marker, and inline feedback for selected nodes; it needs workflow-specific status detail.

## Existing Process-From-Project-Structure Pattern

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Processes.cs` implements the UI pattern to link an existing process definition, confirm start, create launch plan, review staffing, start the process, and link the started run.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureProcessNodeService.cs` implements the API/service side for starting a process node under a project-structure mutation lease.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureProcessNodeKeys.cs` centralizes strongly typed process node key prefixes.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs` exposes `/api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureProcessRunSyncBridge.cs` projects process run state to project-structure node progress/status and parent rollups.

## Existing Workflow Runtime Path

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowContracts.cs` defines workflow runtime manager, execution backend, run store, event store, artifact store, and external-request contracts.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowRuntimeManager.cs` starts workflows, persists snapshots/events/artifacts, handles waiting-for-input and cancellation, and fails explicitly when a backend is not registered.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs` exposes workflow definition, validation, run start, run detail, event, artifact, pending request, and analytics endpoints.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\PersistentWorkflowStores.cs` persists workflow definitions, components, settings, runs, events, external requests, and artifacts.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs` seeds example workflow definitions and currently builds generic cases such as email task routing, invoice workbook risk switch, internet research capture, support SLA escalation, sales lead qualification, and meeting notes action extraction.
- `C:\repositories\CanDoItAll\.codex\bundles\workflow-executors-maf-tools\README.md` records that generic workflow executor support and scenario proof were previously completed, including project-structure executors and PostgreSQL scenario proof.
- `C:\repositories\CanDoItAll\.codex\bundles\ai-workflows-maf-integration\README.md` records the earlier MAF workflow integration and the explicit caveat that production DurableTask/DTS hosting remained a separate concern.

## Current Gaps For This Request

- There is no explicit project-structure `WorkflowDefinition` or `WorkflowRun` object type in `ProjectObjectType`.
- The project-structure action catalog has add/start process actions but no add/start workflow actions.
- The backend has workflow run start APIs, but no project-structure-specific workflow-node start service that composes project/parent input, applies mutation leases, links run ownership, or projects status back to nodes.
- Workflow run persistence stores summaries/artifacts, but there is no project-structure execution summary projection that includes created node ids, asset ids, and file paths.
- The existing workflow example seed has useful generic workflows, but it does not obviously cover Mouser XLS/PDF reconciliation or SEAMARK folder summarization.

## Test Data Inventory

- `C:\programovani\testdata\testworkflows\mouser-order\Cart_Mar30_1059AM.xls`
- `C:\programovani\testdata\testworkflows\mouser-order\MOUSER_Receipt_89566550.pdf`
- `C:\programovani\testdata\testworkflows\IoTFactory rozpočet-v1.xlsx`
- `C:\programovani\testdata\testworkflows\SEAMARK\2018-7 Seamark ZM catalogue.pdf`
- `C:\programovani\testdata\testworkflows\SEAMARK\X Ray Machine Agent Quotation List2018.pdf`
- `C:\programovani\testdata\testworkflows\SEAMARK\X-5600 Xray Inspection system Specification.pdf`
- `C:\programovani\testdata\testworkflows\SEAMARK\X-6600 X ray Inspection system Specification201809.pdf`
- `C:\programovani\testdata\testworkflows\SEAMARK\X-6600A Xray Inspection system Specification.pdf`
- `C:\programovani\testdata\testworkflows\SEAMARK\X-ray inspection system Presentation.pdf`

## Existing Test Surfaces

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureProcessAssignmentDialogTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessWorkflowExecutorIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureProcesses.cs`
