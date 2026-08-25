# Decision lock

These decisions override historical placement and any contradictory statement in the original shared-provider bundle.

## D01 — Canonical owner

Provider configuration, provider administration, provider runtime projection, and the shared-provider control plane are owned by a dedicated non-Razor project in the AgentFramework module family:

`CanDoItAll.Modules.AgentFramework.ProviderManagement`

The exact internal folder names may change, but the compile boundary and responsibility may not.

## D02 — Workspace is a consumer, not the provider host

Workspace owns workspace data and preferences. It must not own provider profiles, provider adapters, provider execution, provider runtime gateways, shared-provider entities, shared-provider relay/discovery, provider secrets, provider pricing/health services, or provider database transfer.

Allowed Workspace residue:

- `DefaultProviderProfileId` or equivalent as an opaque workspace preference.
- A compatibility redirect from an old settings route to `/agents?tab=providers`.
- Generic connector/file/project abstractions that are not provider-specific.

## D03 — No reverse dependency

The new ProviderManagement project must not reference:

- `CanDoItAll.Modules.Workspace`
- Workspace namespaces
- Workspace services or entities
- Workspace Razor components

The outer AgentFramework Razor module may still reference Workspace for legitimate agent tools and workspace integration, but its provider-specific source folders must not.

## D04 — One inference runtime

Production inference must flow through the AgentFramework/MAF provider-driver runtime. The older Workspace direct HTTP stack (`IProviderAdapter`, `ProviderRegistry`, `ProviderExecutionService`, provider-specific `SendAsync` adapters, and `LegacyProviderRuntimeGateway`) is transitional debt and must be eliminated by BR04.

Health checks, model discovery, and pricing metadata may use dedicated administration clients where the MAF runtime has no suitable port, but those clients must not become a second inference route.

## D05 — Inner MAF purity

Do not move EF Core, application persistence, ASP.NET endpoint logic, UI, Workspace, or host-specific security implementation into:

- `CanDoItAll.AgentFramework.Core`
- `CanDoItAll.AgentFramework.Models`
- `CanDoItAll.AgentFramework.Providers`
- other inner MAF projects

Only narrow runtime contracts that genuinely belong to the framework may be added there.

## D06 — Public protocol stability

Keep the existing shared-provider HTTP routes and DTO/wire contracts compatible. `CanDoItAll.SharedProviders.Abstractions` remains the protocol boundary. Endpoint mapping stays in the Web host.

A route rename or DTO break requires an explicit compatibility layer and is outside this bundle by default.

## D07 — Data compatibility

Keep existing provider IDs, shared-provider IDs, secret IDs, revisions, audit records, and foreign-key relationships readable.

The following physical table names remain unchanged in this bundle:

- `Workspace_ProviderProfiles`
- `Workspace_ProviderSharePublications`
- `Workspace_SharedProviderServiceIdentity`
- `Workspace_SharedProviderSources`
- `Workspace_SharedProviderInvocations`
- `Workspace_SharedProviderImports`

The `Workspace_` prefix is acknowledged historical debt. CLR ownership and physical names are independent. Do not drop, rename, copy, or recreate these tables in this bundle.

## D08 — Shared AppDbContext remains valid

The use of the shared `AppDbContext` does not make entities Workspace-owned. Register ProviderManagement EF configurations through the existing module assembly discovery. Do not introduce a provider-specific DbContext merely to express ownership.

## D09 — Authoritative UI

`/agents?tab=providers` is the authoritative provider administration UI. It must consume ProviderManagement application ports and must not inject `WorkspaceService` or alias Workspace provider types.

Workspace settings must not retain a second provider editor implementation.

## D10 — API composition

The Web project owns route mapping and HTTP concerns. Application behavior behind shared-provider catalog, synchronization, administration, and relay endpoints belongs to ProviderManagement. Web endpoint files must not import Workspace for provider operations.

## D11 — Provider transfer ownership

Provider profiles, provider secrets, provider metadata, and shared-provider state are transferred by ProviderManagement. Workspace transfers only workspace-owned preferences, including an optional default provider ID. Cross-module export/import orchestration belongs in Composition or another outer host layer.

## D12 — Preserve correct shared-provider behavior

The following behavior must survive the refactor:

- local publication eligibility and enable/disable semantics
- public catalog projection without secret leakage
- remote discovery and source synchronization
- deterministic import reconciliation and deletion policy
- personal/shared/hybrid materialization
- runtime effective-revision snapshots
- fail-closed handling for missing secrets, invalid sources, or incompatible capabilities
- relay authentication and authorization
- rate limiting
- invocation audit and recovery semantics
- image provider target resolution
- connector manifest integration

## D13 — No feature expansion

Do not implement unfinished SB07 feature/UI/integration scope. Do not add new provider types, protocol versions, authentication schemes, pricing systems, or tenant models.

## D14 — Historical documents are not target architecture

The following original statements are superseded:

- “Workspace EF is canonical.”
- “Workspace owns the five shared-provider entities.”
- “AgentFramework is only a projection over Workspace provider rows.”

Do not edit every historical document to erase them. BR08 adds one explicit supersession note.

## D15 — Semantic conflicts are stop conditions

When current code, an old bundle instruction, or a test conflicts with this decision lock, do not silently preserve the old architecture. Identify the conflict in the current `RESULT.md`, implement the decision-locked direction where safe, and stop only when preserving compatibility requires a user decision.

## D16 — No infrastructure loops

Docker and Podman are prohibited in BR00-BR08. A test that requires them is recorded as deferred to the original SB07 continuation. Do not spend repeated attempts on lifecycle infrastructure.

## D17 — Buildable checkpoints

Every committed subbundle must build its affected projects and pass its targeted tests. Temporary compile-time dependency inversions may exist only inside an uncommitted edit sequence, never at a checkpoint.

## D18 — Source comments

All new or edited source-code comments are in English.
