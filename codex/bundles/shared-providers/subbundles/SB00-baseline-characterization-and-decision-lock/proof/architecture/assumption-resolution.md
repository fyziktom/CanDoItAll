# SB00 persistence/runtime assumption resolution

Captured: 2026-08-24  
Evidence mode: static source characterization  
Product behavior changed by this artifact: **No**

Statuses used here are `Confirmed`, `Amended`, and `Blocked`. There are no blocked assumptions in
this persistence/usage evidence lane; cross-lane API, SDK, Compose, and host questions are owned by
their corresponding SB00 evidence artifacts.

| Prepared assumption | Status | Current evidence | Locked consequence |
| --- | --- | --- | --- |
| Workspace EF provider row is canonical master data. | Confirmed | `ProviderProfile` / `ProviderProfileConfiguration`, `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs:36,89`; both production registries write that row. | Publication/source/import entities remain Workspace-owned. No second provider master is introduced. |
| AgentFramework catalog is a projection, not authority. | Confirmed, with refinement | `WorkspaceBackedAgentProviderProfileRegistry.ListProvidersAsync/GetProviderAsync`, `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs:51,64`, read EF; sandbox catalog update occurs later at `:264`. | Treat the sandbox provider list as a compatibility shadow. Runtime authority is the canonical snapshot loaded from EF. |
| Provider commit observers project committed changes. | Confirmed | `IWorkspaceProviderProfileCommitObserver`; `AgentFrameworkProviderRuntimeSnapshotCommitObserver`, `ProviderRuntimeProfileSnapshotService.cs:959`; calls occur after `SaveChangesAsync`. | Reconciliation notifies observers only after the full transaction commits. Projection failures must be explicit and fail closed. |
| Workspace provider save has one canonical application path. | Amended | Agent UI uses `WorkspaceBackedAgentProviderProfileRegistry`; Settings still has direct `WorkspaceService.SaveProviderAsync`. | Shared-provider mutations use one cohesive Workspace application service; do not duplicate logic in the legacy provider editor/service path. |
| Provider save fully protects optimistic UI edits. | Amended | EF token is stamped by `AppDbContext`, but neither current editor returns an expected token. | New shared entities expose and validate expected concurrency tokens for edits where lost updates matter. |
| Existing provider deletion has reference checks suitable for publication/import cleanup. | Amended | `WorkspaceService.DeleteProviderAsync` and `WorkspaceBackedAgentProviderProfileRegistry.DeleteProviderAsync` hard-delete without a query; catalog service only nulls agent bindings afterward. | Add an explicit reference/deletion policy. Preserve historical invocation evidence and prevent orphaned publication/import rows. |
| Connector manifests are the production-configurable inventory. | Confirmed | Six adapters are registered in `WorkspaceModuleServiceCollectionExtensions.cs:19-24`; `ProviderRegistry.ListManifests` exposes their schemas. | Register `provider.candoitall-shared` here and keep its UI schema source/import managed. |
| Azure is only an inner MAF driver and cannot be configured in production. | Amended | Agent UI exposes `ProviderKind.AzureOpenAi`; `AgentFrameworkProviderMetadata` persists it using the OpenAI connector key; mapper restores it. | Record Azure as configurable through AgentFramework metadata, but with no distinct Workspace manifest. Do not claim a `provider.azure` connector exists. |
| Workspace-to-MAF projection can map a new connector from metadata alone. | Amended | `WorkspaceAgentProviderProfileMapper.ResolveMappedProviderKind` throws for an unknown connector before metadata override. | Add one explicit shared-origin mapping in the outer mapper; keep the effective inner kind OpenAI-compatible. |
| Secret values are stored on provider profiles. | Amended | Provider/source rows store GUID references; `SecretRuntimeResolver` obtains vault values only at runtime. | Persist only the source secret-record ID. Resolve per dispatch and never place values in snapshots, DTOs, audit, or logs. |
| Provider secret resolution always requires a persisted consumer binding. | Amended | Workspace calls can authorize via `AllowedSecretIds`; ordinary MAF resolver supplies only ID/purpose. `SecretReference` has no FK and secret deletion does not check provider references. | Define typed source consumer/purpose semantics and explicit missing-secret failure; do not assume current bindings prevent deletion or substitution. |
| Existing usage projection can receive another source. | Confirmed | `IProviderUsageProjectionSource`; additive registrations; `ProviderUsageQueryService` aggregation/deduplication. | Project durable relay invocation records through the existing usage direction; no second cost ledger. |
| Existing Agent/SimpleChat/Unknown workload values can truthfully identify relay calls. | Amended | `ProviderUsageWorkloadKind` has only `Unknown`, `Agent`, `SimpleChat`; Unknown is included only for `Both`. | Add dedicated relay workload/consumer and selection/UI semantics. Never label relay traffic Agent or SimpleChat merely to fit the enum. |
| Missing upstream usage may be represented as zero. | Amended | Aggregation counts only observed/legacy-known contributions; missing/unavailable observations do not add tokens or cost. | Relay finalization records explicit completeness. Missing usage remains unknown, not zero. |
| New Workspace entities require manual `DbSet` registration. | Amended | `AppDbContext` applies registered module `IEntityTypeConfiguration<>` implementations; Workspace assembly is already registered. | Add focused entity/configuration files and a PostgreSQL migration; do not add a parallel model registry. |
| Multi-row source/import reconciliation can follow current one-row provider save transaction behavior. | Amended | Current provider save has one implicit EF commit followed by non-transactional observers/projections. | Use one explicit transaction for source/import/profile mutations and notify observers after commit. Use serializable scoped locking only where the reconciliation invariant requires it. |

## Blocked assumptions

None in this evidence lane. Any later source finding that contradicts a row above is a reopen
trigger for SB00 and the owning architecture decision; it is not residual-risk prose.

