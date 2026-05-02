# Storage And File Tool Defaults

## Status

- `Completed`

## Objective

Expose storage-driver-backed tools as an internal agent tool family controlled by agent settings, and make standard browse/search/read file tools available through the same policy surface.

## Covered Inputs

- `NOTE-03`
- `NOTE-04`

## Prerequisites

- `01-external-workspace-selection` completed and guard proof passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Abstractions\StorageContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Drivers\FileSystemStorageDriver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Persistence\StoragePersistenceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Storage\WorkspaceService.Storage.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Storage\WorkspaceStorageModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workspace\MafAgentRuntime.WorkspaceRuntimePlugin.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\StorageAccessServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\LocalFileStorageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\MafAgentRuntimeTests.cs`

## Deliverables

- Agent settings for storage read/write and allowed storage catalogs.
- Internal storage runtime plugin with catalog list, text read, text write, and delete.
- Runtime tool composition that attaches storage tools when the selected agent settings allow storage access.
- Guard tests for disabled storage, read-only storage, driver capability mismatch, and per-agent denied storage catalog.
- File browse/search/read tools remain available through existing capabilities and/or new settings without shell workarounds.

## Dependency Impact

- Critical foundation for storage-backed agent work.
- Closure depends on proving policy enforcement, not just tool exposure.

## Validation Depth

- Critical foundation.
- Unit and integration tests.

## Implementation Steps

1. Extend the workspace/file access settings model with storage access fields.
2. Add agent editor controls for storage read/write and allowed storage catalogs.
3. Implement a storage runtime plugin backed by `IStorageCatalogService` and `IStorageDriverRegistry`.
4. Attach storage tools during runtime composition when settings allow read or write.
5. Enforce agent settings, catalog state, read-only flags, and driver capability masks inside the plugin.
6. Add tests for catalog list, text read, text write, delete denial, and tool attachment.

## Scope Exceptions

- Directory listing in arbitrary storage providers is out of scope because `IStorageDriver` does not expose a list API.
- Remote IPFS/FTP live tests are out of scope.

## Do Not Do

- Do not invent a second storage-driver abstraction.
- Do not expose storage writes when either the agent or storage catalog is read-only.
- Do not make storage tools depend on project-structure permissions.

## Acceptance Checklist

- Agents can be granted read-only storage access.
- Agents can be granted write storage access.
- Storage tools are not attached for agents with no storage access.
- Read/write/delete operations enforce both agent settings and storage capabilities.
- File search/list/read tools remain available to agents configured for workspace file access.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter CreateCapabilityState_attaches_configured --no-restore -m:1` passed.
- Runtime composition attaches storage catalog list and storage text read tools when agent storage read access is enabled.
- Runtime composition wraps storage text write and delete tools in approval wrappers when agent storage write access is enabled.
- Storage operations enforce agent read/write settings, catalog allowlist, disabled catalogs, read-only catalogs, and driver capability masks in the runtime plugin.
- Provider-independent storage directory listing remains out of scope because `IStorageDriver` exposes save, open-read, delete, and connection test, but not list/stat.

## Browser Validation Logging

- N/A unless the editor UI layout changes in a way that needs visual proof.

## Progression Gate

- Downstream closure may continue only after storage read/write policy and file-tool attachment are proven.

## Suggested Agent Prompt

```text
Implement subbundle 03 only: expose storage-driver tools through agent settings, enforce read/write/catalog policy, and prove storage and standard file tool behavior with focused tests.
```
