# Current State

## Implemented Plugin Surface

- `CanDoItAll.Plugins.Abstractions` defines plugin descriptors, manifest validation, workflow executor descriptors, connections, and capability interfaces.
- `CanDoItAll.Modules.Plugins` provides a bundled catalog source, installation store, catalog service, EF installation record, and a Blazor plugins page.
- `CanDoItAll.Web.Api.PluginsApi` exposes catalog, install, enable, and disable endpoints.
- Composition wires the plugins module into service registration, module assembly loading, and navigation.
- PostgreSQL and SQLite migrations add the plugin installation table.
- Tests cover manifest validation, catalog persistence, secret broker basics, capability facade guardrails, and architecture guardrails.

## Implemented Workflow Executor Surface

- Workflow executors are described through `WorkflowExecutorDescriptor` and executed through `IWorkflowExecutorInvoker`.
- The invoker checks descriptor availability, policy limits, retries, timeouts, redacted settings summaries, and payload size after execution.
- `WorkflowExecutorPayloadPolicy.MaxPluginOutputPayloadCharacters` limits plugin-owned workflow output to 262144 characters.
- Built-in descriptors include planned command process capability, but plugin workflow execution is not yet a completed bridge.

## Implemented Host Command Surface

- `WorkspaceCommandExecutionService` exposes reviewed methods such as Git, dotnet, Python, PowerShell script file execution, document conversion, spreadsheet preview, skill scripts, and local MCP launch preparation.
- `PowerShellRunScript` only accepts a script path and routes through `WorkspaceCommandPlanBuilder`.
- `LocalWorkspaceProcessHost` provides timeout, capped stdout/stderr, and best-effort tree-kill, but explicitly reports `PolicyOnlyLocal` with `IsEnforcedByHost: false`.
- `WorkspaceCommandEnvironmentPolicy` allowlists common environment variables and prefixes, including `OPENAI_API_KEY` and `OPENAI_`.

## Weak Points Found

- Installation and enablement are state, not consent. There is no persisted runtime grant model.
- `PluginCapabilityKind` has static declarations only. It does not model granted, denied, requested, expired, source-limited, or workflow-scoped access.
- `IPluginCapabilityContext` exposes full capability properties and does not define how missing grants are denied.
- There is no host-command, PowerShell, Docker, or reviewed-recipe plugin capability.
- No workflow bridge currently proves that plugin executor nodes are rejected when a plugin is not installed, not enabled, lacks connection settings, or lacks grants.
- The plugin settings UI is read-only and has no permission controls, connection editor, health check, install, enable, or disable actions.
- `PluginCatalogService` accepts caller-supplied actor strings through request DTOs; permission and audit work must use authenticated or trusted system actors instead.
- Secret contracts are duplicated between abstraction and security modules, increasing drift risk.
- Host-command environment policy would leak OpenAI and other build/runtime secrets into plugin processes unless narrowed for plugin host tools.
- Docker logs and process output can exceed workflow payload and database comfort unless captured as bounded artifacts.

## Performance Scan Summary

- Scope scanned: plugin abstractions, plugin module, plugin API, workflow executor contracts/observability, host command service, environment policy, and process host.
- Observed pattern counts in scoped files: 14 materializations, 4 explicit list/dictionary allocations, 3 compiled regex definitions, 6 `JsonSerializer` uses, 3 EF `SingleOrDefaultAsync` uses, 2 `AsNoTracking` uses, and 1 `byte[]` payload contract.
- No current high-risk LINQ hot path was found in the small bundled plugin catalog flow.
- The performance risk is future-facing: Docker logs, host command output, storage placement using `byte[]`, repeated grant checks during workflow runs, and catalog/search growth.

## EF Core Query Review

- `PluginInstallationStore.ListAsync` and `FindAsync` use `AsNoTracking`, which is correct for read-only catalog operations.
- Update paths use tracked `SingleOrDefaultAsync`, which is appropriate for single-record mutation.
- The current catalog loads all descriptors and installations, then joins in memory. This is acceptable for the current bundled catalog but must not become the shop/search or workflow-hot-path design.
- Future grants, connections, and execution audit lookup APIs need projection DTOs, stable ordering, paging, unique indexes, and concurrency tokens.
- Docker logs must not be stored as EF text/JSON payloads. Store bounded metadata in EF and put log content in workflow artifact/storage infrastructure.

## Docker Use Case Pressure Test

- A Docker plugin that lists containers, pulls images, starts containers, and reads logs needs host process access.
- Direct PowerShell access would be too broad. The safer design is a generic host-tool capability with reviewed recipes and typed arguments.
- Docker-specific recipes should validate allowed registries, image references, container names, log time ranges, tail limits, timeouts, and forbidden flags.
- Workflow integration should pass bounded log text or an artifact reference to a normal LLM workflow node for summarization. The plugin should not receive LLM credentials or raw model execution authority unless separately granted.
