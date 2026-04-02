
# Implementation File Plan

This is a suggested file-level landing zone so Codex does not have to guess where the work belongs.

| Area | Suggested work | Phase |
| --- | --- | --- |
| Infrastructure/Storage models | Add `Models/` for provider kind, capability flags, usage purpose, selection context, recommendation, access descriptor, object reference, transfer manifest/result. | Phase 01 |
| Infrastructure/Storage abstractions | Add `Abstractions/` for driver, registry, catalog, routing, access, transfer, and tester interfaces. | Phase 01 |
| Infrastructure/Persistence | Add entity/configuration files for storage catalog and routing rules; update `AppDbContext` and both migration projects. | Phase 01 |
| Infrastructure/Storage compatibility | Keep/adapter-wrap `WorkspaceStorage.cs` abstractions so legacy callers continue to compile. | Phase 01/02 |
| Infrastructure/Drivers/FileSystem | Refactor current local filesystem logic behind the new provider contract. | Phase 02 |
| Infrastructure/Drivers/Ipfs | Create IPFS driver using node API wrapper and capability reporting. | Phase 02 |
| Infrastructure/Drivers/Ftp | Create FTP driver using an approved client library or wrapper with retries and directory support. | Phase 02 |
| Infrastructure/Access | Add unified storage access service and web endpoint(s) for preview/download. | Phase 02 |
| Infrastructure/Transfers | Add batch transfer pipeline and snapshot/migration reusable helpers. | Phase 02 |
| Workspace UI | Add storage settings tab + components + wizard orchestration. | Phase 04 |
| Components.BaseLib | Add reusable storage presentation components. | Phase 04 |
| Workbench | Adopt storage placement/access in uploads, previews, exports, and storage nodes. | Phase 04 |
| Factory | Adopt storage placement/access in prompt attachments and exports. | Phase 04 |
| Tests.Unit | Add contract/routing/capability tests. | Phase 03 |
| Tests.Integration | Add access-route, IPFS, batch transfer, and honest FTP proof paths. | Phase 03 |
| Tests.Playwright | Add storage settings/workbench/factory browser tests and screenshot capture. | Phase 03 |

## Existing files that should be updated instead of bypassed

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureImportService.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.Pack.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs`

## Design guardrails

- Do not fork a second parallel upload/preview path for remote providers; converge on the new storage access service.
- Do not hide storage provider configuration in random JSON blobs when a typed entity or value object is warranted.
- Do not couple UI components directly to provider-specific classes.
- Do not update only SQLite migrations and forget PostgreSQL.

