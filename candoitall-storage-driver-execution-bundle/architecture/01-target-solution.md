
# Target Solution

## 1. Architectural intent

The target solution is a storage platform, not a renamed `WorkspaceStorage.cs`. The platform should let any module ask for **placement**, **access**, and **capabilities** without caring whether the backing provider is a local folder, an IPFS node, an FTP server, or a future provider.

## 2. Proposed layers

### 2.1 Storage domain and contracts

Place the core abstractions under `src/CanDoItAll.Infrastructure/Storage/` in clearly separated folders such as:

- `Models/`
- `Abstractions/`
- `Drivers/`
- `Persistence/`
- `Access/`
- `Routing/`
- `Transfers/`

Core concepts to introduce:

- `StorageProviderKind`
- `StorageCapability` (flags or equivalent)
- `StorageUsagePurpose`
- `StorageSelectionContext`
- `StorageRecommendation`
- `StorageCatalogRecord` / `StorageConnectionRecord`
- `StorageRoutingRule`
- `StorageObjectReference`
- `StorageAccessDescriptor`
- `StorageTransferManifest`
- `StorageTransferItem`
- `StorageConnectionTestResult`

Core interfaces to introduce:

- `IStorageDriver`
- `IStorageDriverFactory`
- `IStorageDriverRegistry`
- `IStorageCatalogService`
- `IStorageRoutingService`
- `IStorageRecommendationService`
- `IStorageAccessService`
- `IStorageTransferPipeline`
- `IStorageConnectionTester`
- `IStorageCompatibilityFileStoreAdapter` (or equivalent adapter strategy)

### 2.2 Compatibility seam

Do **not** delete `IFileStore` / `IManagedArtifactStore` in the first pass. Instead:

- keep them as compatibility adapters over the new storage services
- migrate real call sites to richer contracts phase-by-phase
- only consider removing or shrinking the adapters after the touchpoint inventory is fully closed

This keeps the codebase buildable while the cross-module adoption is still in progress.

### 2.3 Provider drivers

Implement provider-specific folders such as:

- `Drivers/FileSystem/`
- `Drivers/Ipfs/`
- `Drivers/Ftp/`

Each driver should own provider-specific configuration binding, health testing, read/write/list/delete behavior, and capability reporting.

No module should import these folders directly; only the registry/factory should.

## 3. Persistence model

### 3.1 Storage catalog

Persist storage definitions in application data, not just app settings, because the UI must manage them and projects must reference them.

Recommended persisted fields:

- `Id`
- `Name`
- `ProviderKind`
- `IsEnabled`
- `IsSystemDefault`
- `IsReadOnly`
- `DisplayOrder`
- `ConnectionMode` (local/remote or provider-specific equivalent)
- `EndpointOrRoot`
- `ConfigJson` for provider-specific details
- `CapabilityJson` or equivalent cached/advisory capability snapshot
- `HealthStatus`
- `LastTestedAtUtc`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### 3.2 Secret linkage

Do not store provider passwords/tokens in the storage catalog entity. Instead:

- link to `SecretRecord` ids or an explicit join/reference model
- keep log output redacted
- allow wizard steps to select existing secrets or create a new secret before saving the storage record

### 3.3 Routing rules

Persist routing/default rules separately from storage definitions so the system can express:

- workspace-wide defaults
- module-specific defaults
- project-level defaults
- node-level overrides
- purpose-specific rules
- file-subtype or MIME-based preference rules
- explicit fallback ordering

### 3.4 Storage object references

Current `MediaRelativePath` fields are not expressive enough for remote providers. The target model should introduce a real storage-object reference that can represent:

- local relative path plus provider id
- IPFS CID plus gateway/api metadata
- FTP remote path plus provider id
- future provider locator payloads

A practical rollout path is:

1. keep current media fields temporarily for compatibility/display
2. add a new storage-object-reference payload (JSON column or linked entity)
3. migrate UI and services to use the new reference as source of truth
4. treat old relative-path fields as compatibility-only where still needed

### 3.5 Project-structure storage linking

The simplest high-signal approach in the current schema is to use existing project-object linking primitives:

- model storage nodes as `ProjectObjectType.Infrastructure` with a dedicated storage subtype (preferred) or a justified new type if needed
- use `ExternalArtifactKind` / `ExternalArtifactId` to point to the storage catalog record
- keep node-specific path prefix / subtree-default behavior in metadata JSON or a dedicated link record if queryability becomes important

This avoids inventing a disconnected node catalog.

## 4. Access and preview model

### 4.1 Unified access service

All UI preview/open/download flows should go through `IStorageAccessService` (or equivalent) that returns a `StorageAccessDescriptor`.

Suggested descriptor fields:

- `PreviewUrl`
- `DownloadUrl`
- `DirectUrl` (optional)
- `SupportsInlinePreview`
- `SupportsDownload`
- `SupportsOpenLocally`
- `DisplayFileName`
- `ContentType`
- `ContentLength`
- `ReasonWhenUnavailable`

### 4.2 Endpoint strategy

Current `/managed-files/{**path}` is not sufficient as the universal access route. The target solution should introduce a provider-aware access endpoint such as:

- `/storage/objects/{id}`
- `/storage/objects/{id}/download`
- `/storage/objects/{id}/preview`

Keep `/managed-files/...` only as a compatibility path for existing filesystem-managed assets or as a thin redirect/proxy when the object reference still maps to a local provider.

### 4.3 Local-open safety

Only the filesystem provider should expose local-open by default, and only when the provider root is trusted for the current host.

Do **not** add “download to temp and open” for remote providers in the first implementation phase without an explicit security design and user requirement.

## 5. Routing and recommendation model

Use `ProjectFileSubtype.InferFileSubtype(...)` as a core input but not the only one. The routing engine should consider:

- file subtype
- MIME type
- intended usage purpose
- edit intent
- preview requirement
- publish/deploy intent
- project-level or node-level override
- provider health and enabled state
- capability requirements
- size or streaming constraints when relevant

The engine should return:

- primary recommendation
- alternative providers
- reason text for UI display
- warnings when the preferred provider is unhealthy or incompatible

## 6. Batch transfer model

### 6.1 Why a shared pipeline is needed

`DatabaseSnapshots.cs` already demonstrates a real bulk-copy requirement. The same pattern will recur for:

- folder migration to IPFS
- publish/deploy to FTP
- bulk import/export
- future sync/mirror flows

### 6.2 Pipeline design

Implement a manifest-driven transfer pipeline with:

- bounded concurrency
- cancellation tokens
- retry policy hooks
- progress callback/event reporting
- checksum/hash verification hooks
- provider capability checks before work starts
- structured transfer results per item

Do not hard-wire the first implementation to background jobs unless a real requirement appears. A well-factored in-process pipeline is enough to unblock the current feature set.

## 7. UI architecture

### 7.1 Reusable presentation components

Put reusable presentational pieces in a shared component location such as `src/CanDoItAll.Components.BaseLib/Components/Storage/`:

- storage badge / health badge
- storage capability pill group
- storage summary card
- storage selector/dropdown
- storage recommendation banner
- wizard step header / progress component

### 7.2 Module orchestration components

Put page-specific orchestration under the workspace/workbench/factory modules, for example:

- `src/CanDoItAll.Modules.Workspace/Pages/Components/Storage/`
- `src/CanDoItAll.Modules.Workbench/Pages/Components/Storage/`
- `src/CanDoItAll.Modules.Factory/Pages/Components/Storage/`

This keeps shared components reusable without forcing page/service logic into the base library.

### 7.3 Storage settings wizard

Recommended wizard step order:

1. choose provider type
2. name + purpose metadata
3. connection/root/gateway details
4. auth/secrets selection
5. capability/policy settings
6. connection test
7. defaults and participation in recommendation rules
8. review and save

## 8. Cross-module adoption strategy

Adopt the new platform in this order:

1. foundation services and compatibility layer
2. filesystem provider behind compatibility
3. unified access service
4. Workbench upload/export/preview flows
5. Prompt Factory attachment/export flows
6. snapshot/bulk transfer flows
7. settings UI and storage nodes
8. final closure audit

This preserves a working local-provider story while remote-provider support is brought online.

## 9. Non-goals for the first execution pass

- rewriting every internal repo-local file read into storage-driver traffic
- building background-job orchestration unless the pipeline proves insufficient
- pretending unsupported provider actions exist when they do not
- auto-opening remote files on the host without an approved security design

## 10. Acceptance-oriented architecture rules

- provider-specific branching belongs in the registry, driver factory, or drivers—not scattered through modules
- every browser-visible action must be capability-driven
- every persisted provider credential must flow through secrets
- every in-scope touchpoint from the workbook must have an owner and proof path
- FTP proof must remain blocked if no real protocol-backed validation path exists
