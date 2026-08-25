# SB00 canonical provider persistence and secret lifecycle

Captured: 2026-08-24  
Evidence mode: static source characterization  
Product behavior changed by this artifact: **No**

## Decision

`CanDoItAll.Modules.Workspace.ProviderProfile` in the application database is the canonical
provider master. AgentFramework provider records in the sandbox workspace catalog are a shadow
projection, and the canonical runtime profile source is an immutable in-process snapshot rebuilt
from Workspace EF rows.

## Canonical persistence anchors

| Concern | Current symbol and source anchor | Finding |
| --- | --- | --- |
| Entity | `ProviderProfile`, `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:36` | Stores local identity, connector key/schema, endpoint, secret-record ID, model, timeout, enabled/capability flags, health, extra settings, and concurrency token. |
| EF mapping | `ProviderProfileConfiguration.Configure`, `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:89` | Maps the entity to `Workspace_ProviderProfiles`; `ConcurrencyToken` is an EF concurrency token. |
| Workspace save | `WorkspaceService.SaveProviderAsync`, `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:333` | Validates the manifest/schema, required secret reference, timeout, prices, and connector-specific capabilities; commits EF before notifying observers. |
| Workspace delete | `WorkspaceService.DeleteProviderAsync`, `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:510` | Hard-deletes the row, commits EF, and then notifies observers. It performs no provider-reference check. |
| AgentFramework save | `WorkspaceBackedAgentProviderProfileRegistry.SaveProviderAsync`, `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs:92` | Normalizes the AgentFramework editor, writes the Workspace EF row, notifies observers, then updates the sandbox catalog shadow. |
| AgentFramework delete | `WorkspaceBackedAgentProviderProfileRegistry.DeleteProviderAsync`, `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs:218` | Deletes the canonical row, notifies observers, then removes the shadow-catalog provider. |
| Runtime map | `WorkspaceAgentProviderProfileMapper.Map`, `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceAgentProviderProfileMapper.cs:22` | Maps the Workspace row to the internal AgentFramework runtime profile, including effective kind, transport, purpose, tags, models, and a secret reference rather than a secret value. |

## Two current write paths

The application contains two provider mutation surfaces with different behavior:

1. `AgentProviderProfilesPanel.SaveProviderAsync` calls `IAgentFrameworkWorkspaceService` and
   ultimately `WorkspaceBackedAgentProviderProfileRegistry`. Anchors:
   `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs:122`,
   `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs:302`,
   and
   `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.ProvidersAndCapabilities.cs:24`.
2. `SettingsPage.SaveProviderAsync` calls `WorkspaceService` directly at
   `src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs:167`.

Selecting the Settings navigation item redirects to `/agents?tab=providers` at
`SettingsPage.razor.cs:267`, but `/settings?tab=providers` still selects and renders the older
Workspace editor (`SettingsPage.razor.cs:297`). The older path does not update the sandbox
catalog/directory projection and cannot safely round-trip every AgentFramework metadata field.
It must not become a second implementation path for shared-provider source/import behavior.

## Commit observer and projection order

`IWorkspaceProviderProfileCommitObserver` is declared in
`src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceProviderProfileCommitObserver.cs:3`.
Both canonical save paths call observers only after `SaveChangesAsync`, using
`CancellationToken.None` for the post-commit notification.

`AgentFrameworkProviderRuntimeSnapshotCommitObserver` is registered in
`src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs:230`
and implemented in
`src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs:959`.
It reloads and upserts the committed profile, removes a deleted profile, and faults the runtime
snapshot rather than serving stale data if a committed row cannot be projected.

`WorkspaceBackedAgentProviderProfileRegistry.ProjectCatalogAsync` at
`WorkspaceBackedAgentProviderProfileRegistry.cs:310` updates the sandbox shadow only after the
database and runtime-snapshot notification. A failure throws
`ProviderCatalogProjectionException` with `CanonicalCommitSucceeded = true`; it cannot roll back
the canonical row. `ListProvidersAsync` and `GetProviderAsync` in that registry read EF directly,
so a stale shadow entry is not resurrected.

## Concurrency and transaction conventions

`AppDbContext.StampApplicationManagedConcurrencyTokens` at
`src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs:46` creates a token on add
and replaces it on modification. Current provider editors do not carry an expected token back
to the application service. Consequently, EF detects only a narrow concurrent write between the
service's row load and save; a stale editor opened earlier is otherwise last-write-wins.

Provider save/delete currently relies on the implicit transaction around one `SaveChanges`.
Observer notification, activity recording, sandbox projection, and directory synchronization are
post-commit side effects. New multi-row publication/source/import reconciliation must use one
explicit EF transaction. `SerializableMutationScope` in
`src/Foundation/CanDoItAll.Infrastructure/Persistence/SerializableMutationScope.cs:8` is the
existing repository mechanism when a serializable mutation plus PostgreSQL advisory scope locks
is required. `EfLlmChatUnitOfWork` in
`src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Repositories/EfLlmChatUnitOfWork.cs:8`
is the existing example for callbacks that run only after a successful transaction commit.

## EF model and migration registration

- `AppDbContext.OnModelCreating` applies Infrastructure configurations and every assembly in
  `AppDbContextModelRegistry.Assemblies` at
  `src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs:12`.
- `AppDbContextModelRegistry.ConfigureAssemblies` filters assemblies containing
  `IEntityTypeConfiguration<>`, merges them, and updates the EF model-cache key at
  `src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs:35`.
- `WorkspaceModuleAssemblyMarker` is already in `ModuleAssemblies.All` at
  `src/App/CanDoItAll.Composition/ModuleAssemblies.cs:24`.
- PostgreSQL design-time creation configures the same assembly list in
  `src/Foundation/CanDoItAll.Migrations.PostgreSql/PostgreSqlAppDbContextFactory.cs:14`.

New Workspace-owned shared-provider entities therefore belong in focused Workspace source files
with `IEntityTypeConfiguration<T>` configurations. No `DbSet` or new model registry mechanism is
needed. PostgreSQL migrations and the snapshot remain owned by
`CanDoItAll.Migrations.PostgreSql`.

## Secret-reference and value lifecycle

The Workspace provider row stores only `ApiKeySecretId`. The runtime mapper converts it to the
internal `secret:{guid}` reference in
`WorkspaceAgentProviderProfileMapper.ResolveSecretReference`,
`src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceAgentProviderProfileMapper.cs:305`.

`SecretRuntimeRequest` and `ISecretRuntimeResolver` are defined in
`src/Foundation/CanDoItAll.Security.Abstractions/SecretRuntimeContracts.cs:70`.
`SecretRuntimeResolver.ResolveValueAsync` at
`src/Modules/CanDoItAll.Modules.Security/SecretRuntimeResolver.cs:19`:

1. validates purpose, optional consumer identity, and optional allowed IDs;
2. reads the `SecretRecord`;
3. applies binding authorization;
4. resolves a vault reference or the legacy protected payload;
5. returns `null` for a missing record/empty payload and throws a sanitized exception for vault
   or unprotect failures.

The legacy Workspace provider path passes the provider ID, consumer identity, and a one-item
allowed-secret set at
`src/Modules/CanDoItAll.Modules.Workspace/Providers/ProviderRuntimeGateway.cs:104` and
`src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:544`.

Ordinary MAF execution uses `SecretStoreAgentProviderCredentialResolver` at
`src/Modules/CanDoItAll.Modules.AgentFramework/Providers/Credentials/SecretStoreAgentProviderCredentialResolver.cs:10`.
Its prepared dispatch scope resolves each provider once, keys the resolution by provider ID plus
configuration fingerprint, exposes it through `AsyncLocal`, rejects a changed fingerprint, and
clears the dictionary on disposal (`:30`, `:174`, `:274`). Its core resolver currently supplies
only secret ID and purpose (`:96`), not a consumer identity or allowlist.

`SecretReferenceConfiguration` has no FK to `SecretRecord`, and `SecretService.DeleteAsync`
does not query provider/source references before deleting the record and vault value:
`src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs:66` and `:332`. Missing references
therefore fail at later resolution rather than at deletion.

## Locked implementation consequences

- Workspace EF remains the only master; sandbox catalog/runtime data is projection or cache.
- Shared source rows store one secret-record ID, never a secret value or Authorization header.
- Source catalog/inference operations resolve the token for one dispatch lifecycle and do not
  store it in provider/import snapshots.
- Add typed source consumer ID/purpose constants if persisted source binding is required; do not
  use unscoped string literals.
- Notify provider commit observers only after the complete reconciliation transaction commits.
- Do not implement shared-provider mutations independently in both provider editors.

