# Current State

Prepared against main commit `355f1d621f4106e1ba2f8709fa5ae09499ddec46` and FileTools commit `bdfa4a307dbff3316e3c2699d7483f41ff1d91de`.

## Storage

- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs` defines `IStorageDriver` with connection-test, save, open-read, and delete only. It has no list/stat/search/paging contract.
- `StorageDriverRegistry` selects one `IStorageDriver` by `StorageProviderKind`; duplicate registrations silently choose the last implementation. New browse registration must reject duplicates rather than hide them.
- `FileSystemStorageDriver` is 184 lines, `IpfsStorageDriver` 217, and `FtpStorageDriver` 226. Adding all browse behavior directly would grow broad provider classes; focused browse adapters plus shared provider-specific path/transport collaborators are the target.
- `StorageProviderConfiguration` contains endpoint/provider settings but no typed browse/cache policy. `StorageCatalogRecord.ConfigJson` is the backward-compatible storage location; `MetadataJson` is not.
- Existing tests cover routing, catalog persistence, placement, transfer, storage JSON, and managed-file HTTP flows, but not bounded browsing, paging consistency, provider browse capability honesty, or cross-principal handles.

## Security And Effects

- `repo://src/App/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs` decodes `StorageJson.EncodeReferenceToken` output (unsigned base64url JSON), resolves the referenced driver, and streams content.
- `Program.cs` calls `MapCanDoItAllManagedFiles()` without `RequireAuthorization`. Authentication/authorization are conditional on API configuration and no fallback policy was found.
- `IWorkspacePathAccessGuard` provides workspace/managed-root containment but does not establish project, process, resource, actor, or operation authority.
- FileTools explicitly requires the host to re-resolve and authorize browser intent, mint an opaque `FileReference`, reauthorize save, and enforce expected revisions.

## Package Boundary

- Main `NuGet.Config` uses `repo://ExternalPackages`; no `CanDoItAll.FileTools.*` package is present.
- FileTools packages are version `0.1.0` and have a seven-package validation script. Integration must consume validated package artifacts, not sibling source project references.
- FileTools `global.json` pins SDK `10.0.301`; installed stable SDK is `10.0.300`. The FileTools CodeAnalytics snapshot loaded zero projects for this reason. SB01 must provision the declared SDK or stop; it must not silently rewrite the FileTools pin.

## Current Dependency Graph

CodeAnalytics snapshot `snap-20260713002602-7de53bec` loaded seven product projects and found no project-level cycle:

| Project | Relevant direct product references |
| --- | --- |
| Composition | Infrastructure, Processes module, Projects, Resources, Workbench |
| Infrastructure | none of the scoped product projects |
| Processes module | Infrastructure |
| Projects | Infrastructure |
| Resources | Infrastructure, Projects |
| Workbench | Infrastructure, Projects, Resources |
| Web | Composition, Infrastructure, Projects, Resources, Workbench |

One pre-existing module cycle exists inside Infrastructure between `CanDoItAll.Infrastructure.Persistence` and `CanDoItAll.Infrastructure.ControlPlane`. This bundle must not worsen it or use it to excuse new cycles.

## UI Hotspots

| Surface | Evidence | Required response |
| --- | --- | --- |
| Projects page | `ProjectsPage.razor` 773 lines; `ProjectsBoard.razor` 666; `ProjectModalHost.razor` 632 | Extract focused filter projection/scope and files pane/dialog; do not add browsing state to these owners |
| Project Structure | `ProjectStructurePage.razor` 2,584 lines plus 22 partial files; several partials exceed 700/1,000 lines | New top-level window/coordinator/scope types; no new page partial |
| Processes | `LiveProcessesDashboard.razor` 2,888 lines | Focused `ProcessRunFilesDialog` and coordinator; dashboard owns only open/close/run-id state |
| Resources | `ResourcesPage.razor` + code-behind 512 lines | Focused browse pane/source catalog/promotion command |
| Composition | `RuntimeHostServiceCollectionExtensions.cs` 1,027 lines | Declarative feature registration extension; no runtime logic or service location |

## FileTools Contracts Available

- Browser: `IFileBrowserProvider`, optional search/content/action facets, bounded `FileBrowserBrowseRequest`, opaque item/source IDs, source capabilities, `FileBrowserSession`, disabled/bounded retention, and host-only `ItemInvoked`/`ActionRequested`.
- Interaction: independent `IFileContentSource`, opaque `FileReference`, bounded content loading, deterministic profiles/renderers, View/Edit, awaited `SaveRequested`, revision conflicts, history, autosave, and debounced preview.
- Built-in interaction packages cover text, conservative raster image, browser-native PDF, inert SVG/fallback, and optional Markdown.

## Tool Reliability

- Main CodeAnalytics evidence is usable but has generated-type duplicate diagnostics and several test-project package-load warnings; exact `.csproj` and source reads remain authoritative.
- FileTools CodeAnalytics and Components MCP are not usable at preparation time. Their re-entry retries are mandatory and failure blocks phases that need their evidence.
