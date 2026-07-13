# C# Boundary Map

## Responsibility Movement

| Responsibility | Current owner | Target owner | Old owner after change |
| --- | --- | --- | --- |
| Native browse contract/registry | missing | Infrastructure Storage `Browse` area | `IStorageDriver` unchanged |
| Filesystem path normalization/confinement | embedded in `FileSystemStorageDriver`/guards | focused provider path policy reused by read/write/browse | existing driver delegates; no duplicate algorithms |
| IPFS/FTP browse transport/mapping | missing/mixed into drivers | focused provider browse adapter and narrow transport collaborator | read/write driver remains cohesive |
| Storage -> FileTools mapping | missing | Integration project adapter | Infrastructure remains FileTools-free |
| Semantic file scope/access contract | missing | Integration.Abstractions | modules depend on stable typed contract |
| Handle/content/save policy | unsigned token/endpoints | Integration security/effect services | endpoints become thin authorized adapters |
| Project filter/hierarchy closure | page-local | Projects-owned pure projection/resolver | page delegates |
| Canvas file action/window | missing | Workbench top-level coordinator/window/scope resolver | no new page partial |
| Run roots | spread across launch/workbench concerns | Processes-owned run-root policy/scope provider | Workbench consumes, never owns process policy |
| Resource promotion | page/connector flow | Resources command/service | page delegates |
| Known-file interaction | several dialogs/branches, including Project Structure asset double-click/dialog | host interaction coordinator + FileInteraction renderers, with no FileBrowser dependency | legacy paths removed only after replacement proof; image/PDF dialog semantics preserved |

## Target Top-Level Types

Names are contracts for intent; adjust namespace suffixes only if current conventions demand it and record the mapping before editing.

- Infrastructure: `IStorageBrowseDriver`, `IStorageBrowseDriverRegistry`, `StorageBrowseDriverRegistry`, `StorageBrowseCapabilities`, `StorageBrowseRequest`, `StorageBrowsePage`, `StorageBrowseEntry`, `StorageBrowseCursor`, `StorageBrowseCacheSettings`, provider-specific browse drivers and focused path/transport collaborators.
- Integration.Abstractions: `FileScopeId`, `FileAccessActorId`, `FileAccessOperation`, `FileAccessContext`, `FileHandleId`, `AuthorizedFileBrowseScope`, typed known-file and collection-browse requests, `IProjectFileScopeProvider`, `IProjectStructureNodeFileScopeProvider`, `IProcessRunFileScopeProvider`, `IResourceFileSourceProvider`, `IFileBrowserSessionFactory`, `IFileInteractionHost`.
- Integration: storage FileTools provider adapter/catalog, authorization coordinator, bounded handle registry, content source, save target, cache decorator/policy resolver, file-catalog revision service/change sink, DI registration extension.
- Modules: `ProjectFileFilterProjection`, `ProjectFilesPane`, `ProjectFilesDialog`, `ProjectStructureFileScopeResolver`, `ProjectStructureFileBrowserWindow`, `ProjectStructureFileActionCoordinator`, direct Project Structure file-interaction dialog binding, `ProcessRunFileScopeProvider`, `ProcessRunFilesDialog`, resource file source catalog/promotion command, file interaction dialog/coordinator.

## Composition Root

Composition knows implementations and registers them declaratively. It may reference both new integration projects and module implementations. Runtime/domain services never inject `IServiceProvider`, call `BuildServiceProvider`, or resolve by string.

## Old-Class Policy

- No new `ProjectStructurePage.*.cs` partial.
- Large pages/components retain rendering and minimal open/close/current-id state only.
- `RuntimeHostServiceCollectionExtensions` gets at most one focused registration call; the feature extension owns registration detail.
- Source assertions and before/after member/line/responsibility tables are mandatory at each cleanup gate.

## Temporary Bridges

The only allowed bridge is a thin compatibility adapter from the existing managed-file preview/download route to the new authorization/handle service while legacy callers migrate. It has an explicit removal/deprecation decision in SB16/SB18. It may not accept unsigned token authority.
