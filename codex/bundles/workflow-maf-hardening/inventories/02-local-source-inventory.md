# Local Source Inventory

SB01 inventory date: 2026-05-28.

Proof transcripts:

- `bundle://proof/SB01/transcripts/git-baseline.txt`
- `bundle://proof/SB01/transcripts/source-scan.txt`
- `bundle://proof/SB01/transcripts/restore-build.txt`

Environment baseline:

- Branch: `processes-hardening`
- Commit: `5a431c2a7e02c2d8fde65b092c6fd4a2d058b572`
- .NET SDK: `10.0.204` from `global.json`
- Restore: passed
- Build: passed with existing MSB3277 Entity Framework Core assembly version conflict warnings

| Path | Responsibility | Current MAF usage level | Risk | Suggested subbundle owner |
| --- | --- | --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Models` | Strongly typed workflow definitions, nodes, edges, routing, runtime policy, event, artifact, and executor DTOs. | model-only | high | SB02, SB03, SB05, SB06 |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs` | Validates names, graph shape, route metadata, LLM component references, executor IDs/settings, policy limits, connectivity, shape compatibility, and cycles. | adapter | critical | SB02 |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs` | Executor catalog, invoker, retry/timeout handling, audit records, redaction, payload policy, and executor exceptions. | adapter | critical | SB03, SB04 |
| `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs` | Runtime backend selection, human-input waiting path, run/event/request/artifact persistence, cancellation, external request responses. | runtime | critical | SB05 |
| `repo://src/CanDoItAll.AgentFramework.Persistence` | Persistent workflow definition/run/event/artifact/settings stores and database mapping. | model-only | high | SB05, SB06 |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs` | Native MAF workflow adapter using `WorkflowBuilder`, `BindAsExecutor`, edges, switches, fan-out, status/event mapping, preview simulation hooks, LLM/executor invokers. | runtime | critical | SB03 |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` | In-process MAF backend using `InProcessExecution`, event capture, completion/error state mapping, and configured file artifact records. | runtime | critical | SB03, SB05 |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs` | Built-in descriptor catalog for file, source ingestion, HTTP, spreadsheet, project structure, image generation, and planned executors. | adapter | high | SB04, SB06 |
| `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/*WorkflowExecutor.cs` | Built-in workflow executor implementations with typed settings and JSON payload outputs. | adapter | high | SB04, SB05 |
| `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` | File-backed workflow template pack loader and YAML-to-model mapper for `Templates/Workflows`. | model-only | critical | SB02 |
| `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs` | Managed workflow seed refresh, managed marker/version checks, default runtime settings, sample workspace assets. | adapter | critical | SB02, SB06 |
| `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs` | Workflow list/detail/preview/run UI orchestration and service consumption. | adapter | high | SB06 |
| `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs` | Workflow graph authoring UI model, node/edge editing, and executor canvas actions. | model-only | high | SB06 |
| `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowExecutorCanvasCatalog.cs` | UI grouping/action model for executor quick-create actions. | model-only | medium | SB06 |
| `repo://Templates/Workflows/manifest.yaml` | Repository-owned workflow pack manifest, default runtime policy, executor policies, and component defaults. | model-only | critical | SB02, SB06 |
| `repo://Templates/Workflows/workflows` | External YAML workflow definitions. | model-only | critical | SB02 |
| `repo://src/CanDoItAll.Modules.Plugins` | Plugin catalog, permissions, OAuth, runtime logs, and execution observer bridging plugin executor audit records. | adapter | critical | SB04, SB05 |
| `repo://src/CanDoItAll.Plugins.Abstractions` | Plugin manifest, service registry, workflow executor descriptor, connection, package, and validation contracts. | model-only | critical | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Gmail` | Bundled Gmail plugin descriptors, OAuth availability checks, workflow executors, and simulation descriptors. | adapter | high | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Office365` | Bundled Office365 plugin descriptors, OAuth availability checks, workflow executors, and simulation descriptors. | adapter | high | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Email` | Email workflow payload resolver and bundled plugin support. | adapter | medium | SB04 |
| `repo://src/plugins/CanDoItAll.Plugin.Docker` | Docker bundled plugin descriptors and host-command workflow executor surface. | adapter | critical | SB04 |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs` | Unit coverage for validation, runtime manager, durable policy, and in-process workflow basics. | test | high | SB02, SB05 |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs` | Unit coverage for executor catalog, invoker, routing, retry, timeout, payload, redaction, and built-in executor scenarios. | test | critical | SB03, SB04 |
| `repo://tests/CanDoItAll.Tests.Unit/WorkflowPreviewSimulationTests.cs` | Unit proof that preview simulation avoids executor invocation when configured. | test | medium | SB03, SB05 |
| `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs` | HTTP workflow API coverage for definition save/publish/import/start and runtime backend selection. | test | high | SB05, SB06 |
| `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs` | Component tests for workflow page behaviors, executor nodes, and runtime event rendering. | test | high | SB06 |
| `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs` | Browser-level project-structure workflow smoke proof. | test | medium | SB06, SB07 |

SB01 findings:

- The runtime is not a blank slate. It already has a native MAF adapter boundary through `MafWorkflowCompiler` and an in-process MAF backend.
- The compiler currently uses function bindings via `BindAsExecutor`; it does not use source-generated partial `Executor` classes with `[MessageHandler]`.
- Domain validation is already centralized in `WorkflowDefinitionValidator`, but template loading itself still maps YAML into models and relies on downstream validation at catalog save/runtime boundaries.
- Plugin executors are present as `IWorkflowExecutor` implementations in bundled plugin projects; `IPluginWorkflowExecutor` exists in plugin abstractions but bundled plugins do not currently register through that contract.
- Runtime events are persisted, but MAF event-to-record mapping currently loses node identity for native workflow events and only records configured file artifacts after completion.
- Build passes, so implementation phases can proceed without an SDK blocker.
