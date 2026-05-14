# Service Capability Map

## Secret Capability

Current source:

- `CanDoItAll.Modules.Security`
- `ISecretVault`
- `ISecretRuntimeResolver`
- `SecretService`
- `SecretField`

Plugin-facing shape:

- `IPluginSecretBroker`
- plugin connection secret bindings;
- purpose-scoped and consumer-bound resolution;
- redacted summaries only.

Do not expose:

- raw vault implementation;
- raw secret values to UI;
- unrestricted `SecretService.GetAsync` for plugin runtime.

## Workspace File Capability

Current source:

- `IWorkspaceFileService`
- `WorkspaceFileService`
- `WorkspaceScopeDescriptor`
- `WorkspacePathResolutionService`

Plugin-facing shape:

- `IPluginWorkspaceFiles` with operation-specific policy checks and run/plugin/connection context.

Do not expose:

- file-system root;
- raw absolute paths;
- unrestricted path resolution.

## Storage Capability

Current source:

- `IStorageDriver`
- `IStorageDriverRegistry`
- storage connection/catalog models.

Plugin-facing shape:

- for normal plugins: scoped storage access adapter;
- for storage-provider plugins: separate reviewed capability and registration contract.

Do not expose:

- raw driver registry by default;
- secret credentials outside `IPluginSecretBroker`.

## Project Structure Capability

Current source:

- project/workbench modules;
- `ProjectStructureWorkflowExecutor` currently reaches concrete Workbench service.

Plugin-facing shape:

- `IPluginProjectStructureGateway` or `IProjectStructureRuntimeGateway`;
- stable DTOs for list/read/create operations;
- registered implementation from Workbench/Projects module.

Do not expose:

- `ProjectStructureAgentService` concrete service;
- `IServiceScopeFactory` lookup pattern.

## HTTP Capability

Current source:

- .NET `HttpClient`/`IHttpClientFactory` patterns are available in modules.

Plugin-facing shape:

- `IPluginHttpClientFactory`;
- named client per plugin;
- policy headers, timeout, user-agent, outbound restrictions if needed.

Do not expose:

- unrestricted long-running HTTP calls outside workflow policy.

## OAuth2 Capability

Current source:

- not yet implemented.

Plugin-facing shape:

- `IPluginOAuth2Broker`;
- provider registration and authorization start/callback models;
- token lease for execution;
- encrypted token persistence through vault or protected storage.

Do not expose:

- refresh/access token storage primitives to plugins.
