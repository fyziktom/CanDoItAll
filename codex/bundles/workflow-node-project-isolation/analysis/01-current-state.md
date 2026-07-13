# Current State

## Summary

The user is right about the direction. Workflow behavior has typed models and some clean service interfaces, but the implementation is spread across `AgentFramework.Core`, `AgentFramework.Maf`, `Modules.AgentFramework`, `Modules.Workbench`, `Modules.Plugins`, plugin projects, Web API, templates, and tests. The biggest maintainability risk is that MAF owns both default executor registration and workflow compilation/backend behavior while host registration wires nearly every workflow runtime piece in one method.

## Existing Strengths

- Workflow models are strongly typed in `repo://src/CanDoItAll.AgentFramework.Models/Workflows`.
- Process architecture already demonstrates the target style with `Contracts`, `Abstractions`, `Core`, `Builder`, `Runtime`, `Templates`, `Drivers.Abstractions`, and `Drivers.Standard` projects.
- Previous capability isolation work added dedicated tools/skills/MCP projects, so the repo already accepts this architectural pattern.
- Plugins already expose workflow executor metadata and bundled plugin executors for Docker, Gmail, and Office365.
- Templates are already file-driven under `repo://Templates/Workflows`.
- Unit, integration, component, and Playwright tests exist for workflows, executors, templates, API, and workbench scenarios.

## MAF Coupling

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorServiceCollectionExtensions.cs:9` registers all built-in default executors from MAF.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs:16` owns built-in descriptor construction and settings schema reflection.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs` compiles workflow definitions into Microsoft Agents workflow graphs.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` owns in-process backend execution, event capture, artifacts, and resume behavior.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs` bridges LLM workflow components into MAF.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafHandoffWorkflowFactory.cs` is another workflow-specific MAF factory surface.

## Core Coupling

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs` contains executor interfaces, catalog, invoker, exceptions, policy limits, and side-effect checks in one file.
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs` is a large validator at 682 counted lines in the local scan.
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs` is a large runtime manager/store file at 523 counted lines.
- Core also owns catalog services, routing compiler, payload policy, event payloads, artifact content stores, external request runtime, preview simulation, and process executor bridge.

## Host Registration Coupling

`repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs:67` wires all of these in one host extension:

- `AddBuiltInWorkflowExecutors`
- `IWorkflowExecutorCatalog`
- `IWorkflowExecutorExecutionObserver`
- `IWorkflowExecutorApprovalGate`
- `IWorkflowExecutorInvoker`
- `IWorkflowLlmComponentInvoker`
- `IWorkflowDefinitionValidator`
- `IWorkflowRuntimeBackendCatalog`
- in-memory catalog/store services
- artifact content store
- checkpoint factory
- payload policy
- event sink
- MAF compiler and backend
- runtime manager
- process executor bridge
- workflow test runner

This is a composition smell. A new workflow hosting/composition project should own this wiring, then host can call one or two focused registration methods.

## Plugin Consequences

- `repo://src/CanDoItAll.Modules.Plugins/Services/PluginsModuleServiceCollectionExtensions.cs:21` registers `PluginWorkflowExecutorDescriptorSource` as an `IWorkflowExecutorDescriptorSource`.
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginWorkflowExecutorDescriptorSource.cs:11` projects plugin manifest executors into workflow executor descriptors and applies grant availability.
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs:1041` discovers installed package executor types assignable to `IWorkflowExecutor`, registers them, and wraps source metadata.
- Bundled plugins register `IWorkflowExecutor` implementations directly in their service collection extensions.
- Gmail and Office365 executors prove external read/write side effects, OAuth/secrets, network use, processed-marker receipts, idempotency keys, and deterministic Run Preview simulations.
- Docker executors prove host command grants, output bounds, deterministic preview, and guarded host-tool execution.

Plugin migration cannot be treated as a descriptor-only change. It must preserve runtime invocation and package-loading behavior.

## Template And UI Coupling

- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` is 716 counted lines and contains YAML DTOs, validation, graph construction, input parameter descriptors, runtime policy mapping, and template conversion in a Blazor module.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs` consumes catalog, executor catalog, backend catalog, and UI mapping directly.
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs` starts and tracks workflow nodes for project structure.
- `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` is an external compatibility surface and must stay stable.

## Existing Tests To Preserve

- `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowArchitectureBoundaryTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/PluginCatalogIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/EmailWorkflowSwitchScenarioTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureWorkflows.cs`
