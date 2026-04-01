# Scope Inventory

## Affected Code Surfaces

| Surface | Current State | Why It Matters |
| --- | --- | --- |
| Database provider registration | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` binds provider/connection at startup only | Must become runtime-resolved per active profile |
| Startup bootstrap | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Program.cs` calls `EnsureCreatedAsync()` and SQLite-only initializers | Must move to migration/bootstrap service and profile-aware startup |
| Design-time migrations | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Persistence/AppDbContextFactory.cs` lacks module assembly composition | Must be fixed or replaced for real migrations |
| Control-plane persistence | Not present today | Required to remember/select/switch profiles and store credentials safely |
| DataProtection | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` sets app name only | Control-plane and cross-restart secret decryption require persisted keys |
| Storage resolver | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs` is single-root and content-root relative | Must become profile-scoped |
| Managed-file serving | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Program.cs` binds one `PhysicalFileProvider` at startup | Incompatible with profile switching |
| Browser workbench state | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Infrastructure/BrowserWorkspaceStateStore.cs` uses one global local-storage key | Must be profile-isolated |
| Workbench model | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.SharedKernel/WorkbenchTabState.cs` snapshot has no profile marker | Must include profile metadata and stale-route recovery |
| Artifact routes | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` and `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor` assume project exists | Must not crash after switch |
| Settings UI | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor` has no data-source management tab | Must expose list/create/test/open/activate flows |
| Main layout | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Components/Layout/MainLayout.razor` has no active DB indicator or startup modal | Must surface runtime database state globally |
| Security secrets | `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Security/SecurityModels.cs` stores secrets in selected DB | Cannot host database-profile secrets there |
| Test harnesses | Integration/component/playwright harnesses currently bootstrap fixed SQLite DBs | Must support multi-profile and PostgreSQL proof |

## Affected Test Layers

- Unit: provider resolution, catalog, encryption, switch coordination, workbench keying, storage path rules.
- Integration: schema bootstrap, runtime switch, provider parity, clone/snapshot, legacy upgrade.
- Component: startup modal, settings data sources, stale-artifact fallback.
- Playwright: active database UX, multi-tab switch reload, managed-file continuity, clone/snapshot UX.
