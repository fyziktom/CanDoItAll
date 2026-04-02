
# Storage Touchpoint Scan

The table below is the human-readable companion to `inventories/04-storage-driver-touchpoints.xlsx`.

| ID | Module | Surface | Source file or route | Scope status | Owning phase | Owning workstream(s) |
| --- | --- | --- | --- | --- | --- | --- |
| TP-001 | Infrastructure | Baseline storage abstraction | C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs | In scope | Phase 01 / 02 | P1-WS01, P1-WS04, P2-WS01, P2-WS02 |
| TP-002 | Infrastructure | Storage configuration defaults | C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs | In scope | Phase 01 | P1-WS02 |
| TP-003 | Infrastructure | DI registrations | C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs | In scope | Phase 02 | P2-WS01 |
| TP-004 | Web | Managed files endpoint | C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs | In scope | Phase 02 | P2-WS04 |
| TP-005 | Web | Program bootstrap/dev seed endpoint | C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs | In scope | Phase 04 | P4-WS04 |
| TP-006 | Workbench | Project node media save | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs | In scope | Phase 04 | P4-WS02 |
| TP-007 | Workbench | Project workbench file subtype policy | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs | In scope | Phase 01 / 04 | P1-WS03, P4-WS02 |
| TP-008 | Workbench | Project structure create request composer | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs | In scope | Phase 04 | P4-WS02 |
| TP-009 | Workbench | Project structure import service | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureImportService.cs | In scope | Phase 04 | P4-WS02 |
| TP-010 | Workbench | Project workbench export/capture workflows | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Workflows.cs | In scope | Phase 04 | P4-WS02 |
| TP-011 | Workbench | Selection panel previews | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor | In scope | Phase 04 | P4-WS02 |
| TP-012 | Workbench | Inline document preview | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor | In scope | Phase 04 | P4-WS02 |
| TP-013 | Workbench | Preview dialog overlay | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor | In scope | Phase 04 | P4-WS02 |
| TP-014 | Workbench | Local file opener | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureLocalFileOpener.cs | In scope | Phase 02 / 04 | P2-WS04, P4-WS02 |
| TP-015 | Workbench | Runtime launcher path trust | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureRuntimeLauncher.cs | Adjacent/in scope for safety | Phase 02 / 04 | P2-WS04, P4-WS04 |
| TP-016 | Factory | Attachment preparation | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.Pack.cs | In scope | Phase 04 | P4-WS03 |
| TP-017 | Factory | Prompt export | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.cs | In scope | Phase 04 | P4-WS03 |
| TP-018 | Factory | Attachment preview nodes | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptSessionAttachmentNode.cs | In scope | Phase 04 | P4-WS03 |
| TP-019 | Infrastructure | Database snapshots | C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs | In scope | Phase 02 / 04 | P2-WS03, P2-WS05, P4-WS04 |
| TP-020 | Workspace UI | Settings shell | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor | In scope | Phase 04 | P4-WS01 |
| TP-021 | Workspace UI | Database source settings patterns | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor | In scope | Phase 04 | P4-WS01 |
| TP-022 | Resources UI/Domain | FTP resource metadata | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Resources/ResourceModels.cs | Adjacent/in scope | Phase 01 / 04 | P1-WS02, P4-WS01 |
| TP-023 | Resources UI | Resources page FTP editor | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor | Adjacent | Phase 04 | P4-WS01 |
| TP-024 | Security | Secret service | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs | In scope | Phase 01 | P1-WS02 |
| TP-025 | Shared Model | Project object types | C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs | In scope | Phase 01 / 04 | P1-WS02, P4-WS02 |
| TP-026 | Workbench | Infrastructure catalog definitions | C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs | In scope | Phase 04 | P4-WS02 |
| TP-027 | Playwright | Artifact browser tests | C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/ProjectStructureArtifactBrowserTests.cs | In scope | Phase 03 | P3-WS03 |
| TP-028 | Playwright | App fixture | C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs | In scope | Phase 03 | P3-WS03 |
| TP-029 | Unit Tests | Current local storage unit tests | C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/LocalFileStorageTests.cs | In scope | Phase 03 | P3-WS01 |
| TP-030 | Unit Tests | Path guard tests | C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkspacePathResolverGuardTests.cs | In scope | Phase 03 | P3-WS01 |
| TP-031 | Integration Tests | Managed files storage integration tests | C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ManagedFilesStorageIntegrationTests.cs | In scope | Phase 03 | P3-WS02 |
| TP-032 | Integration Tests | Profile harness integration tests | C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProfileHarnessIntegrationTests.cs | In scope | Phase 03 | P3-WS02 |
| TP-033 | Test Support | Fake IPFS server | C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs | In scope | Phase 03 | P3-WS02 |
| TP-034 | Support Pattern | SFTP transport implementation | C:\repositories\CanDoItAll/src/CanDoItAll.Mcp.SshOps/Transport/SshNetTransport.cs | Adjacent | Phase 02 | P2-WS03 |
| TP-035 | Web UI | Tuning attachments in MainLayout | C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor | Adjacent / document only | Phase 04 | P4-WS04 |
| TP-036 | Migrations | SQLite model snapshot | C:\repositories\CanDoItAll/src/CanDoItAll.Migrations.Sqlite/Migrations/AppDbContextModelSnapshot.cs | In scope | Phase 01 | P1-WS02 |
| TP-037 | Migrations | PostgreSQL model snapshot | C:\repositories\CanDoItAll/src/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs | In scope | Phase 01 | P1-WS02 |

## Notes

- Rows marked `Adjacent / document only` or similar still need explicit closure in Phase 04; they cannot silently disappear from the audit.
- The workbook adds proof routes and checklist coverage columns so Codex can validate completion against the same inventory.

