# Current State Analysis

## Workflow Executors

The workflow stack already has the right conceptual seam for executors:

- `IWorkflowExecutor`, `IWorkflowExecutorCatalog`, and `IWorkflowExecutorInvoker`;
- strongly typed `WorkflowExecutorId`;
- `WorkflowExecutorDescriptor` metadata;
- node-level `ExecutorId`, `ExecutorSettingsJson`, and `ExecutionPolicy`;
- built-in executors for workspace files, source ingestion, HTTP fetch, spreadsheet, project structure, and image generation;
- `/api/workflows/executor-catalog`;
- workflow UI support for executor nodes.

This is enough to start hardening toward plugin executors, but it is not enough for plugins as-is.

## Main Gaps

1. Executor descriptors lack plugin ownership/provenance/version/trust/availability metadata.
2. `ExecutorSettingsJson` validation checks syntax only, not schema or required fields.
3. Workflow settings UI is hard-coded by built-in executor id.
4. Planned/unimplemented executors are catalog-visible and require better availability semantics before plugin installed/enabled state is added.
5. There are multiple built-in executor DI registration paths with different lifetimes.
6. Plugin access to project structure would currently have to follow a concrete Workbench service leak.
7. Secret runtime resolver has purpose and allowed-secret checks, but consumer-bound authorization must be hardened for plugins.
8. Existing connector schema is reusable, but it lives in Workspace and should not become a second duplicated plugin settings schema.
9. Composition is static, which is acceptable for bundled MVP but not enough for remote dynamic plugin loading.

## Findings

| Id | Finding | Detail | Decision | Refs |
| --- | --- | --- | --- | --- |
| F001 | Workflow executor contracts exist and are useful | The current workflow layer already has IWorkflowExecutor, descriptors, catalog, invoker, policies, and node settings. This is a strong starting point for plugin executors. | Ready foundation | S001,S002,S003 |
| F002 | Descriptor metadata is too thin for plugins | WorkflowExecutorDescriptor lacks plugin id/source/version/trust/availability/connection metadata. Plugin executors need provenance and install/enabled state. | Pre-plugin refactor required | S002,S005 |
| F003 | Settings validation is only JSON syntax | WorkflowDefinitionValidator parses ExecutorSettingsJson but does not validate required fields, field types, or schema compatibility. | Pre-plugin refactor required | S004 |
| F004 | Workflow editor uses hard-coded executor settings UI | WorkflowCanvasEditor contains built-in-specific settings editors. Copying this pattern for plugins would cause duplication and drift. | Pre-plugin refactor required | S010,S011,S012 |
| F005 | Connector schema already solves part of plugin settings | ConnectorPluginManifest, ConnectorConfigurationSchema, ConnectorConfigState, and ConnectorConfigFieldEditor can be extracted/adapted as canonical settings schema/UI. | Reuse candidate | S025,S026,S027 |
| F006 | Secret vault is improved but not plugin-ready | ISecretVault and runtime resolver exist, but consumer type/id are not yet an enforced plugin authorization boundary. | Pre-plugin refactor required | S013,S014,S015,S016 |
| F007 | Non-Windows vault provider behavior needs explicit review | Auto provider may resolve to unsupported macOS/Linux provider stubs depending on host. Plugin deployments likely include non-Windows hosts. | Operational risk | S015,S016 |
| F008 | Workspace files have a useful scoped service | IWorkspaceFileService and workspace scope models are good foundations for plugin file capability wrappers. | Ready foundation | S020,S021,S023 |
| F009 | Storage drivers are lower-level than plugins should normally see | IStorageDriverRegistry/driver contracts are powerful and should be exposed only through explicit plugin capabilities. | Boundary risk | S019 |
| F010 | Project structure executor leaks Workbench implementation | ProjectStructureWorkflowExecutor resolves ProjectStructureAgentService through IServiceScopeFactory, which is not a stable plugin-facing boundary. | Pre-plugin refactor required | S024 |
| F011 | Composition is static | ModuleAssemblies and RuntimeHostServiceCollectionExtensions are hard-coded. This is acceptable for bundled plugin MVP but not enough for shop-installed dynamic code. | MVP constraint | S032,S033 |
| F012 | Existing plugin folder is not runtime plugin infrastructure | plugins/candoitall-components-mcp is a Codex/MCP plugin asset, not an application plugin module. | Naming collision risk | Repository inspection |
| F013 | Prior workflow executor bundle excluded full plugin runtime | The workflow executor bundle intentionally prepared contracts but kept full runtime/custom plugin loading/custom rendered setup out of scope. | Historical context | S038 |
| F014 | Planned executors appear in catalog | PlannedWorkflowExecutor descriptors are registered; validation should explicitly handle unavailable/planned executors before plugins add disabled states. | Pre-plugin refactor required | S005,S006,S007,S008 |
| F015 | There are two executor registration paths | AgentFramework.Hosting and Modules.AgentFramework register built-in executors with different lifetimes. Plugin registration should not add a third divergent path. | Canonicity risk | S006,S007,S008 |

## Near-Term Architectural Conclusion

Start with prerequisite hardening and architecture reviews. Do not add `CanDoItAll.Modules.Plugins` until the foundation gate has passed. When the module is added, keep it static/bundled and bridge plugin executors into the existing workflow executor catalog.
