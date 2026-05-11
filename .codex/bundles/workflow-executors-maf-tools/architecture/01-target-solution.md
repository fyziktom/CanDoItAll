# Target Solution

## Contract Shape

- Add `WorkflowExecutorId` as a strongly typed id.
- Add descriptor models such as `WorkflowExecutorDescriptor`, `WorkflowExecutorCategory`, `WorkflowExecutorSettingsSchema`, `WorkflowExecutorDefaultPolicy`, and `WorkflowExecutorSetupRenderer`.
- Extend `WorkflowNodeSettings` with optional executor id, settings JSON, and execution policy init properties so existing positional constructor call sites remain compatible.
- Keep executor settings serialized as JSON at the workflow-definition boundary, but validate them through descriptor-backed typed validators before execution.

## Runtime Shape

- Add `IWorkflowExecutorCatalog`, `IWorkflowExecutor`, and `IWorkflowExecutorInvoker` in AgentFramework Core.
- Built-in executors register through DI as catalog/implementation entries; future plugins can register the same contracts without changing workflow compiler code.
- `MafWorkflowCompiler` creates function executor bindings that call the invoker for `WorkflowNodeKind.Executor` or nodes with an executor id.
- Each invocation receives `WorkflowExecutorExecutionContext`, `WorkflowNodeInput`, node settings, policy, workflow run metadata, and cancellation.
- The invoker applies timeout and retry policy explicitly and returns `WorkflowNodeExecutionResult` with payload JSON and result shape. Exhausted retry or timeout produces a failed workflow event, not pass-through output.

## Built-In Executors

- Storage/file: list/stat/read/search/write/append/diff through existing workspace file services.
- Project structure: read project/tree/subtree/node and create asset nodes through existing project-structure agent service adapter.
- HTTP fetch: GET/POST/PUT/PATCH/DELETE over `http`/`https`, bounded response size, headers/body settings, timeout.
- AI image: prompt/settings to existing image-provider path, output as file/artifact reference when available.
- Spreadsheet: read workbook summary, read cell/range, write cell/range, save workbook, render Markdown table/report through `CanDoItAll.Tools.Documents`.
- Descriptor-only planned generic executors: JSON transform, Markdown render, delay/timer, approval/request, command/process execution with explicit follow-up if not implemented.

## Document Wrapper Boundary

- New project: `C:\repositories\CanDoItAll\src\CanDoItAll.Tools.Documents\CanDoItAll.Tools.Documents.csproj`.
- Direct dependency: `ClosedXML` version aligned with the reference repo, currently `0.105.0`.
- Public surface exposes app-owned models and service interfaces only. Consumers must not use ClosedXML types.
- Initial wrapper covers spreadsheet operations only; namespace leaves room for PDF/DOCX later.

## UI Shape

- Workflow canvas quick-create/right-click actions use second-level grouped executor menus through `CanvasWorkbenchAction.Children`.
- Workflow supporting pane uses a grouped searchable `OverlayComponentToolbox` for executor descriptors.
- Node inspector displays descriptor metadata and built-in setup fields. The descriptor includes `SetupRendererKey` so future plugin UI components can bind without changing saved workflow definitions.

## Artifact And Observability Shape

- Executor outputs that fit inline payload policy remain in event payload JSON.
- File/image/spreadsheet/project-structure asset outputs produce `WorkflowArtifactRecord` rows with content type, storage path, and node id.
- Execution failures include executor id, node id, attempt count, timeout, and sanitized settings summary.
