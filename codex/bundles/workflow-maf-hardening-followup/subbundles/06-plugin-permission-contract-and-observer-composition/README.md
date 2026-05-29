# 06-plugin-permission-contract-and-observer-composition

## Objective

Make plugin workflow executor governance deterministic and internally consistent.

## Current problem

Plugin descriptors now expose permission policy, but manifest validation does not yet verify policy/capability consistency. Plugin workflow executor audit observer registration uses `TryAddScoped`, while AgentFramework also registers a null observer with `TryAddScoped`; effective observer can become module-order dependent.

## Exact source references

- `src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs`
- `src/CanDoItAll.Plugins.Abstractions/PluginManifestValidation.cs`
- `src/CanDoItAll.Modules.Plugins/Services/PluginsModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.Plugins/Catalog/PluginLogServices.cs`
- `src/plugins/CanDoItAll.Plugin.Gmail/*`
- `src/plugins/CanDoItAll.Plugin.Office365/*`
- `src/plugins/CanDoItAll.Plugin.Docker/*`

## Implementation steps

1. Replace single observer registration with deterministic composition:
   - introduce `IWorkflowExecutorExecutionObserverSink` or register multiple observers with `IEnumerable<>`,
   - create `CompositeWorkflowExecutorExecutionObserver`,
   - use `NullWorkflowExecutorExecutionObserver` only if no real observer is registered.
2. Add DI tests for module registration in both orders:
   - AgentFramework then Plugins,
   - Plugins then AgentFramework.
3. Extend `PluginManifestValidator`:
   - `RunsHostCommand` requires `PluginCapabilityKind.HostCommand`,
   - `UsesSecrets` requires secret/OAuth/capability path,
   - `UsesNetwork` requires network-capable plugin metadata,
   - `WritesExternalData` requires approval policy not weaker than configured product default,
   - workflow executor descriptors with deterministic test mode must provide simulation or fake path.
4. Consolidate bundled plugin executor descriptors:
   - avoid drift between `PluginWorkflowExecutorDescriptor` and runtime `WorkflowExecutorDescriptor`,
   - use shared factories where practical.
5. Add fake-mode integration tests for Gmail, Office365, and Docker.
6. Ensure Docker arguments are validated against recipe allowlists and never pass arbitrary host shell commands.

## Do not do

- Do not execute live external calls in default tests.
- Do not make plugin observers optional by registration order.
- Do not hide missing permission/capability mismatches as warnings only.

## Acceptance checklist

- Plugin audit records are persisted regardless of module registration order.
- Manifest validation fails inconsistent plugin permission policies.
- Gmail/O365/Docker fake-mode tests pass without secrets.
- Host-command governance remains strict.

## Proof required

- DI composition tests.
- Manifest validation tests.
- Plugin fake-mode tests.
