# Source Map

Use these paths as primary references. Keep the map updated if files move.

| Id | Area | Path | Why it matters |
| --- | --- | --- | --- |
| S001 | Workflow executor contracts | src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs | Defines IWorkflowExecutorCatalog, IWorkflowExecutor, IWorkflowExecutorInvoker, execution context, catalog duplicate-id guard, retry/timeout invoker. |
| S002 | Workflow executor descriptor/settings models | src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs | Defines WorkflowExecutorDescriptor, WorkflowExecutorExecutionPolicy, executor setting records, executor categories. |
| S003 | Workflow graph and node models | src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs | Defines WorkflowNodeKind.Executor and WorkflowNodeSettings.ExecutorId/ExecutorSettingsJson/ExecutionPolicy. |
| S004 | Workflow definition validation | src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs | Validates executor references, policy ranges, and JSON syntax; does not validate settings against schema or availability. |
| S005 | Built-in executor descriptors | src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs | Creates built-in and planned executor descriptors with schema strings and default settings. |
| S006 | Built-in executor DI extension | src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorServiceCollectionExtensions.cs | Registers built-in executors as singletons for non-module host path. |
| S007 | Agent framework module DI | src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs | Registers workflow executors scoped in the application module path and wires workflow services. |
| S008 | Agent framework hosting DI | src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs | Registers built-in workflow executors and in-memory workflow services for host path. |
| S009 | Workflow executor catalog API | src/CanDoItAll.Web/Api/WorkflowsApi.cs | Exposes GET /api/workflows/executor-catalog and workflow API surface. |
| S010 | Workflow canvas editor UI | src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor | Contains hard-coded executor settings editors for current built-in executors plus raw JSON editing. |
| S011 | Workflow canvas editor code-behind | src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs | Owns editor state and serialization behavior for workflow canvas. |
| S012 | Workflow canvas catalog model builder | src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowExecutorCanvasCatalog.cs | Builds quick actions/default inputs for executor nodes with built-in-specific logic. |
| S013 | Secret models and service | src/CanDoItAll.Modules.Security/SecurityModels.cs | Defines SecretRecord metadata, SecretService, list/get/save/delete models. |
| S014 | Secret runtime resolver | src/CanDoItAll.Modules.Security/SecretRuntimeResolver.cs | Resolves secret values with purpose and allowed-secret checks; consumer type/id are present but not yet a full authorization boundary. |
| S015 | Secret vault implementations | src/CanDoItAll.Modules.Security/SecretVaults.cs | Defines ISecretVault, DPAPI, DataProtection file fallback, in-memory vault, and unsupported provider stubs. |
| S016 | Security module DI | src/CanDoItAll.Modules.Security/SecurityModuleServiceCollectionExtensions.cs | Selects vault provider and registers secret services/resolvers. |
| S017 | Storage secret resolver | src/CanDoItAll.Modules.Security/StorageSecretResolver.cs | Adapts storage credentials to secret runtime resolver. |
| S018 | BaseLib secret field | src/CanDoItAll.Components.BaseLib/Components/Forms/SecretField.razor | Reusable secret reveal/copy component. |
| S019 | Storage driver contracts | src/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs | Lower-level storage driver/catalog abstractions; plugin access should use facades unless the plugin is a storage provider. |
| S020 | Workspace file contracts | src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileContracts.cs | Safe-ish workspace file service abstraction already used by workflow executors. |
| S021 | Workspace file service | src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileService.cs | Path-policy-backed file access implementation. |
| S022 | Workspace path resolution | src/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathResolutionContracts.cs | Defines workspace path resolution services. |
| S023 | Workspace scope models | src/CanDoItAll.AgentFramework.Models/Workspace/WorkspaceScopeModels.cs | Defines Sandbox/Process/Project/Tenant/Organization workspace scopes. |
| S024 | Project structure executor | src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/ProjectStructureWorkflowExecutor.cs | Workflow executor currently resolves concrete Workbench service through IServiceScopeFactory; must be replaced by a canonical gateway before plugins use project structure. |
| S025 | Connector manifest/schema | src/CanDoItAll.Modules.Workspace/Connectors/ConnectorManifest.cs | Existing connector-plugin manifest and configuration schema concept that should be reused/extracted for plugin settings. |
| S026 | Connector config state | src/CanDoItAll.Modules.Workspace/Connectors/ConnectorConfigState.cs | Dictionary-backed configuration state with JSON serialization. |
| S027 | Connector config field editor | src/CanDoItAll.Modules.Workspace/Pages/Components/ConnectorConfigFieldEditor.razor | Existing schema-driven field renderer for text/url/number/bool/json/secret fields. |
| S028 | Provider execution registry | src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs | Provider adapters expose ConnectorPluginManifest and are aggregated as manifest sources. |
| S029 | Resource connector registry | src/CanDoItAll.Modules.Resources/ResourceConnectorPlugins.cs | Resource connectors expose manifests and capabilities. |
| S030 | Settings page | src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor | Current settings tabs include workspace/data sources/storage/secrets; plugin settings needs a new tab/page and shared renderer. |
| S031 | Settings page code-behind | src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs | Loads providers, storage, and secrets for settings surfaces. |
| S032 | Module assembly list | src/CanDoItAll.Composition/ModuleAssemblies.cs | Hard-coded module assembly inventory; plugin module must be added statically for MVP. |
| S033 | Runtime module composition | src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs | Hard-coded Add*Module sequence; plugin module registration belongs here after prerequisites. |
| S034 | Shell navigation | src/CanDoItAll.Web/Composition/ShellNavigation.cs | Hard-coded navigation; plugin catalog/settings route must be added deliberately. |
| S035 | Web program | src/CanDoItAll.Web/Program.cs | Web host composition entry point. |
| S036 | API endpoint route builder | src/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs | API endpoint extension registration; plugin API map belongs here. |
| S037 | Existing Codex bundle template | .codex/bundles/workflow-executors-maf-tools/templates/subbundle-readme-template.md | Subbundle formatting baseline used by this bundle. |
| S038 | Existing workflow executors bundle | .codex/bundles/workflow-executors-maf-tools/README.md | Prior workflow-executor contract bundle; explicitly kept full plugin runtime out of scope. |
| S039 | Existing vault bundle | .codex/bundles/secret-vault-storage/README.md | Prior vault migration bundle; useful for secret-boundary constraints. |
| S040 | Unit test project | tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj | Primary backend unit test surface for contract/facade/validator tests. |
| S041 | Component test project | tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj | BUnit/component proof surface for plugin settings renderer and catalog UI. |
| S042 | Integration test project | tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj | API/persistence integration proof surface. |
| S043 | Playwright test project | tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj | Browser proof surface for plugin catalog/settings/workflow executor selection. |
