# Current State

## Existing Workflow Path

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs` defines workflow ids, node kinds, value shapes, runtime policies, artifacts, external requests, and current node settings.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowContracts.cs` defines validation, runtime manager, backend, run store, artifact store, and test runner boundaries.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs` validates graph shape, LLM component references, runtime policy, modality, and shape compatibility.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\MafWorkflowCompiler.cs` validates and creates MAF `ExecutorBinding`s, but every node currently binds to a local pass-through function that returns the input payload and node result shape.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\MafInProcessWorkflowExecutionBackend.cs` compiles a workflow and runs it through `InProcessExecution.RunAsync`, then maps MAF events into product workflow event records.

## Existing UI Path

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor` renders the workflow canvas, inspector, simple toolbox, LLM component list, and preview controls.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs` manages draft nodes, edges, save, validation, preview run, and basic create actions.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasModels.cs` maps workflow nodes into canvas workbench nodes and quick-create actions.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolboxWindow.razor` and `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolboxWindow.razor` provide the grouped `OverlayComponentToolbox` pattern requested for workflows.

## Existing Tool Surfaces

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileContracts.cs` and `WorkspaceFileService` expose bounded workspace file list/read/write/search/stat/diff operations with receipts.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Artifacts\WorkspaceArtifactToolContracts.cs` exposes document conversion and spreadsheet inspection.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs` already maps project-structure operations into AI tools, including project read, node catalog, node create/update, asset create/get/revision, dependency query, and import.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ImageGenerationTools.cs` is the likely source for existing image provider behavior.

## MAF Source Review

- The Microsoft blog states executors are typed workflow units and the workflow builder enforces compatible typed edges at compile time.
- The blog states DurableTask hosting maps each executor to a durable activity with checkpointing and retry/fault tolerance in the durable runtime.
- Local MAF source confirms function executors can be bound with `Func<TInput,IWorkflowContext,CancellationToken,ValueTask<TOutput>>`, which is the right hook for product executor invocation.

## Excel Reference Review

- `C:\programovani\Aqualectra\pve-invoicing-connector\PVEInvoicing\PVEInvoicing\Import\ExcelImportService.cs` and export services use ClosedXML `XLWorkbook`, worksheet cell access, ranges, validations, and `AdjustToContents`.
- The reference repo uses `ClosedXML` version `0.105.0`.
- CanDoItAll currently has no central package management file, so the new document wrapper should include a direct package reference unless CPM is added later as a separate task.
