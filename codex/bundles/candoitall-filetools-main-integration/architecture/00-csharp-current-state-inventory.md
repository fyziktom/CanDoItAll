# C# Current-State Inventory

## Evidence

- Main CodeAnalytics snapshot: `snap-20260713002602-7de53bec`.
- Exact `.slnx`, `.csproj`, storage, endpoint, module UI, tests, and FileTools public-contract files were read directly.
- FileTools CodeAnalytics is a known SB01 gap because SDK `10.0.301` is unavailable; do not treat its empty snapshot as source evidence.

## Storage Responsibility Inventory

| Source/type | Current responsibility | Dependencies | Risk / target seam |
| --- | --- | --- | --- |
| `StorageContracts.cs` / `IStorageDriver` | test, save, open-read, delete | storage records | Preserve; do not force browse/search onto all drivers |
| `StorageDriverRegistry` | provider selection | all `IStorageDriver` registrations | Last registration wins silently; new browse registry must reject duplicates |
| `FileSystemStorageDriver` | local save/read/delete/path confinement | `IWorkspacePathResolver` | Extract/reuse focused path policy; separate browse implementation |
| `IpfsStorageDriver` | HTTP API save/read and access descriptors | logger, secret resolver, ad-hoc `HttpClient` | Isolate browse transport/mapping; distinguish immutable vs mutable paths |
| `FtpStorageDriver` | connection/save/read/delete, URI construction | secret resolver, obsolete `FtpWebRequest` | Add a narrow testable transport seam before browse; no broad manager |
| `StorageProviderConfiguration` | mixed provider settings | JSON | Add nested typed browse/cache settings in `ConfigJson`; validate centrally |
| `InfrastructureServiceCollectionExtensions` | all Infrastructure registration | 37+ services | Keep declarations only; move feature registration to a focused extension if it grows materially |
| `ManagedFilesEndpointRoutes` | preview/download by unsigned token, managed path read | storage/access/driver/path guard | Existing token is not authority; new handle boundary and endpoint hardening required |

Direct construction to remove or constrain: `StoragePlacementService` constructs `FileSystemStorageDriver` directly for a static root. The browse work must not add more direct construction; decide whether the shared filesystem path collaborator lets this path remain simple without a service locator.

## Large/Partial Owners

| Owner | Evidence | Integration policy |
| --- | --- | --- |
| `RuntimeHostServiceCollectionExtensions` | 1,027 lines, 65-member hotspot | One declarative `AddCanDoItAllFileToolsIntegration` call; no runtime behavior |
| `ProjectsPage.razor` | 773 lines | No browser/session/cache logic; focused child and coordinator |
| `ProjectsBoard.razor` | 666 lines | Shared filter projection extraction before Files tab; no duplicated filters |
| `ProjectModalHost.razor` | 632 lines | New focused files dialog, not another modal-host responsibility |
| `ProjectStructurePage.razor` | 2,584 lines plus 22 partial files | No new partial; top-level window/coordinator/scope owner |
| `LiveProcessesDashboard.razor` | 2,888 lines | Only dialog open/close/run-id state may be added |
| `ResourcesPage` | 270-line Razor + 242-line code-behind | Focused browse pane/catalog/promotion command |

`ProjectStructurePage` partial use is already an architecture hotspot. This bundle does not refactor the entire page, but every new file concern must be outside the partial cluster and directly testable.

## Current Tests

- Unit: `StorageJsonTests`, `StorageCatalogServiceTests`, `StorageAccessServiceTests`, `StoragePlacementServiceTests`, `StorageTransferPipelineTests`, `LocalFileStorageTests`, workspace path guards, project structure policies.
- Integration: `ManagedFilesStorageIntegrationTests`, project/workbench/process integration suites.
- Components: Projects and project-structure component tests.
- Playwright: project structure, process shell, storage-driver artifact, database-switch, and other app flows. Existing storage artifact tests do not prove FileBrowser listing/search/FileInteraction.

## Missing Proof

- Native browse contracts, duplicate registration, paging consistency, capability validation.
- Filesystem traversal/reparse/current-read browse tests in the main provider.
- IPFS CID/MFS and FTP list/stat behavior.
- Package provenance/static assets in main.
- Actor/runtime-bound handles and endpoint authorization.
- Disabled/cache/revision semantics.
- Browser-to-interaction handoff and save conflicts.
- Large-screen UI proof for all new surfaces.

## Risk Notes

- Current unsigned endpoint behavior is an active compatibility/security concern, not a FileTools contract.
- A full solution architecture snapshot contains 339 mostly size/complexity findings and generated diagnostics; targeted before/after evidence is required per phase.
- Infrastructure has a pre-existing Persistence/ControlPlane module cycle. No new work may add to or depend on that cycle.
