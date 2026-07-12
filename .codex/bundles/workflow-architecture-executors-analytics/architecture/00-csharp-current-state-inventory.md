# C# Current-State Inventory

## Projects And Ownership

| Area | Current owner | Problem |
|---|---|---|
| Workflow service contracts | Common Core, Runtime, unused Workflows.Abstractions | Duplicate contracts and inverted references |
| Catalog/validation/application services | Workflows.Core and Common Core | Broad services and unclear boundary |
| Runtime lifecycle | Workflows.Runtime plus persistence in Modules.AgentFramework | Completion-first persistence and UI-module infrastructure ownership |
| Executor contracts/policy | WorkflowExecutors.Abstractions/Core | Sound policy pipeline, dual catalog/invoker truth |
| Standard executors | Standard category projects | Repeated registration and partial-class source ingestion |
| Plugin executors | Plugin manifests, host adapter, bundled module implementations | Descriptor/default/simulation drift and outward SDK references |
| Workspace/document tools | Common Core, Tools.Documents, Modules.AgentFramework | Good service adapters except duplicated source extraction/image orchestration |
| Workflow UI | Modules.AgentFramework plus shared renderer host in Modules.Workspace | Hard-coded create flow, inert custom renderers, large components |
| Analytics | Provider models/events/persistence/API/UI | Producer exists; typed persistence/query/consumer missing |

## Hotspots

- `WorkflowCanvasEditor.razor.cs` — 3609 lines.
- `WorkflowsPage.razor.cs` — 1701 lines.
- `PersistentWorkflowStores.cs` — 1600 lines.
- `WorkflowExecutorModels.cs` — 898 lines.
- `WorkflowModels.cs` — 748 lines.
- `WorkflowCatalogServices.cs` — 715 lines.
- `WorkflowDefinitionValidator.cs` — 643 lines.
- `WorkflowExecutorCanvasCatalog.cs` — 582 lines.
- `MafInProcessWorkflowExecutionBackend.cs` — 451 lines.
- `MafWorkflowCompiler.cs` — 417 lines.

These are signals, not automatic refactor scope. Extract only responsibilities required by this initiative and give each extraction a direct consumer and direct tests.

## Existing Partial-Class Policy Violations

- `SourceIngestionWorkflowExecutor` uses partial files for candidate discovery, paths, extraction, and execution. Those are separate responsibilities and must become collaborators.
- UI partial files may remain where they partition one Blazor component's presentation orchestration, but new domain/application behavior must not be added to them.

## Baseline Dependency Findings

- Main snapshot: `snap-20260712155251-9c6f7b5e`.
- No cycles in the focused workflow/executor graph.
- Two pre-existing cycles in `CanDoItAll.Modules.AgentFramework` are outside this initiative; final proof must show no new cycles.
- `IWorkflowCatalogService`, `IWorkflowDefinitionValidator`, `IWorkflowRuntimeBackendCatalog`, and `IWorkflowRuntimeManager` in Workflows.Abstractions currently have zero references.

## Existing Tests That Must Change

- `WorkflowExecutorCategoryIsolationTests` explicitly requires partial executor files.
- `WorkflowExecutorHardeningCheckpointTests` primarily enforces a file-length threshold.
- `WorkflowFoundationHardeningCheckpointTests` freezes Core-to-Runtime/Common.Core references.

Replace these with active-contract ownership, dependency direction, contribution parity, no-partial-executor, and collaborator behavior assertions.

## SB04 Process Workflow Driver Checkpoint

- Scoped CodeAnalytics snapshot: `snap-20260712202635-d4d98642` across process contracts/application/runtime/persistence/drivers, workflow abstractions/core/runtime, and Modules.AgentFramework.
- `AgentFrameworkProcessLaunchExecutorResolver` owns agent resolution and currently rejects workflow executor kind; it must gain only the workflow binding resolution branch, not workflow execution.
- `AgentFrameworkProcessStepExecutor` is already a broad adapter; workflow behavior must live in a separate top-level driver and leave only one typed delegation branch in the adapter.
- `WorkflowProcessExecutorBridge` is an unused one-line direct runtime-start wrapper. It bypasses launch policy and is a removal target, not an extension point.
- `ProcessRuntimeStepAssignment` and its EF store are the durable assignment boundary. They need one optional typed workflow binding persisted as two nullable GUID columns.
- Missing tests at red gate: explicit selection/no-any-active resolver behavior, typed-origin recovery/no-duplicate launch, process input/output mapping, unsupported artifact behavior, and persistence round trip.
