# Target boundary

## New project

Create:

```text
src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/
  CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj
  ProviderManagementModuleAssemblyMarker.cs
  Services/ProviderManagementServiceCollectionExtensions.cs
  Contracts/
  Domain/
  Persistence/
  Administration/
  RuntimeProjection/
  SharedProviders/
  DatabaseTransfer/
```

Namespace root:

`CanDoItAll.Modules.AgentFramework.ProviderManagement`

This is an outer application/infrastructure module in the AgentFramework family. It is not an inner MAF project and not a Razor component library.

## Allowed project dependencies

ProviderManagement may reference, as actually needed:

- `CanDoItAll.AgentFramework.Models`
- `CanDoItAll.AgentFramework.Core`
- `CanDoItAll.AgentFramework.Providers`
- `CanDoItAll.Infrastructure`
- `CanDoItAll.Security` or narrower security abstractions
- `CanDoItAll.SharedProviders.Abstractions`
- `CanDoItAll.SharedProviders.Http`
- `CanDoItAll.SharedKernel`

ProviderManagement must not reference:

- `CanDoItAll.Modules.Workspace`
- `CanDoItAll.Modules.AgentFramework` Razor project
- Web host projects
- Workbench
- Projects or other feature modules unless a neutral lower-level contract is extracted

When an allowed dependency would create a cycle, extract the smallest stable contract downward rather than adding an upward reference.

## Ownership matrix

| Concern | Canonical owner | Notes |
|---|---|---|
| Persisted provider profile | ProviderManagement | Use a distinct CLR name such as `ProviderProfileRecord` if it avoids collision with MAF runtime models. |
| Provider CRUD, validation, secret mutation | ProviderManagement | Expose application ports to UI/API. |
| Provider manifest, capability, health, pricing administration | ProviderManagement | Must not become a second inference runtime. |
| Runtime profile projection and revision snapshot | ProviderManagement | Projects persisted/imported rows into MAF runtime types. |
| Provider drivers and execution primitives | Existing AgentFramework/MAF provider projects | Single inference runtime. |
| Shared publication/source/import/invocation entities | ProviderManagement | Keep current IDs and physical table mappings. |
| Shared discovery/reconciliation/relay/audit/rate limiting | ProviderManagement | Preserve behavior. |
| Provider management UI | `CanDoItAll.Modules.AgentFramework` | `/agents?tab=providers`; consumes ProviderManagement ports. |
| Shared-provider HTTP routes | Web host | Mapping/auth/HTTP only; calls ProviderManagement ports. |
| Workspace default provider preference | Workspace | Opaque ID only. No provider entity/service ownership. |
| Provider data transfer | ProviderManagement | Host composition coordinates with Workspace preference transfer. |
| Shared `AppDbContext` | Infrastructure | Applies configurations from module marker assemblies. |

## Recommended application ports

Use existing contracts where they already express the right boundary. Otherwise introduce narrow equivalents of:

```csharp
public interface IProviderAdministrationService
{
    Task<IReadOnlyList<ProviderSummary>> ListAsync(CancellationToken cancellationToken);
    Task<ProviderEditorModel> GetEditorAsync(Guid providerId, CancellationToken cancellationToken);
    Task<ProviderSaveResult> SaveAsync(ProviderEditorModel model, CancellationToken cancellationToken);
    Task<ProviderDeleteResult> DeleteAsync(Guid providerId, CancellationToken cancellationToken);
}

public interface IProviderPromptExecutionService
{
    Task<ProviderPromptExecutionResult> ExecuteAsync(
        ProviderPromptExecutionRequest request,
        CancellationToken cancellationToken);
}
```

These names are recommendations, not an excuse to duplicate an already suitable interface. The semantics are locked:

- UI/API do not access `AppDbContext` directly.
- Workbench does not know Workspace provider types.
- Runtime execution delegates to MAF drivers.
- ProviderManagement does not know Workspace.

## Source relocation map

Move and rename semantically, preserving behavior:

| Current location | Target responsibility |
|---|---|
| `Modules.Workspace/SharedProviders/**` | `Modules.AgentFramework.ProviderManagement/SharedProviders/**` |
| provider entity/configuration inside `WorkspaceModels.cs` | `ProviderManagement/Persistence` |
| `Modules.Workspace/Providers/**` | General administration/runtime projection in ProviderManagement; legacy direct execution removed in BR04 |
| provider CRUD in `WorkspaceService` | ProviderManagement administration service |
| `WorkspaceBackedAgentProviderProfileRegistry` | Database/canonical registry without Workspace naming or dependency |
| `WorkspaceAgentProviderProfileMapper` | Persisted profile mapper without Workspace naming or dependency |
| `ProviderRuntimeProfileSnapshotService` | ProviderManagement runtime projection |
| `AiProvidersDatabaseTransferHandler` | ProviderManagement database transfer |
| Web shared-provider endpoint service dependencies | ProviderManagement ports |
| Agent provider panel Workspace aliases/services | ProviderManagement contracts |
| Workbench `ProviderExecutionService` use | MAF-backed `IProviderPromptExecutionService` |

## Runtime convergence

At final acceptance, the following old production abstractions/types must not exist as an inference path:

- `IProviderAdapter`
- `ProviderRegistry` from Workspace provider execution
- `ProviderExecutionService`
- `ProviderExecutionRequest` / `ProviderExecutionResponse`
- `OpenAiProviderAdapter`
- `OllamaProviderAdapter`
- `ComfyUiProviderAdapter`
- `LegacyProviderRuntimeGateway`

Do not mechanically delete useful validation, health, model discovery, price calculation, or manifest logic. Split those responsibilities from direct inference and keep them behind administration-focused services where required.

The shared relay and Workbench must invoke a MAF-backed execution port. They must not choose a raw HTTP adapter.

## Dependency graph at final acceptance

```text
Web ------------------------------> ProviderManagement
AgentFramework Razor module ------> ProviderManagement
AgentFramework Razor module ------> Workspace          (only legitimate agent/workspace integration)
Workbench ------------------------> AgentFramework Core/provider execution port
ProviderManagement ---------------> AgentFramework Core/Models/Providers
ProviderManagement ---------------> Infrastructure/Security/SharedProvider abstractions
Workspace ------------------------> no ProviderManagement ownership dependency
ProviderManagement ---------------> no Workspace dependency
```

Composition may reference both Workspace and ProviderManagement to register modules and coordinate cross-module import/export. Neither module should reference the other solely for orchestration.

## EF Core compatibility

- Add `ProviderManagementModuleAssemblyMarker` to `ModuleAssemblies.All`.
- Move EF configurations with the entities.
- Retain the exact existing `ToTable(...)` names.
- Retain keys, indexes, lengths, concurrency/revision semantics, and delete behavior.
- Keep the existing migration history.
- If the model snapshot changes only because CLR types moved, generate and inspect a metadata migration. Its `Up` and `Down` methods must be empty.
- Any proposed `RenameTable`, `DropTable`, `CreateTable` for an existing provider table, data copy, or FK recreation is a hard failure for this bundle.

## UI compatibility

- Existing links and bookmarks to the provider tab continue to work.
- Workspace settings may redirect to `/agents?tab=providers` but contains no duplicate editor state or save/delete logic.
- User-facing labels no longer say “Workspace-owned”, “workspace-backed provider”, or equivalent.

## API compatibility

- Keep current route templates and request/response contracts.
- Preserve authentication/authorization behavior.
- Preserve public catalog secret redaction.
- Preserve stable external provider IDs and revisions.
- Endpoint files may depend on ProviderManagement interfaces or Web-local DTO mapping, never Workspace provider services.
