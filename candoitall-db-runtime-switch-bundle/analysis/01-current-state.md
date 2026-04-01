# Current State

## Executive Summary

- The repository already contains **partial provider wiring** for SQLite, PostgreSQL, and in-memory EF Core, but the choice is fixed at startup through configuration and environment variables.
- The app **does not** currently model databases as user-selectable profiles, does not persist an app-level active-profile catalog, and does not support runtime switching.
- Startup schema creation is based on `EnsureCreatedAsync()` plus a set of **SQLite-only** raw SQL schema initializer classes; there are no committed EF Core migrations in the repo.
- Storage, managed-file serving, and browser workbench restore are all currently **single-root / single-key** constructs, which would leak or break under runtime database switching.
- The codebase is structurally close to runtime switching because most services already create a fresh `AppDbContext` per operation, but the missing control plane, schema strategy, storage strategy, and circuit reload strategy make the feature unsafe today.

## Current Provider Configuration Path

### Infrastructure registration

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` binds `DatabaseOptions`, `StorageOptions`, `WorkbenchOptions`, and `DevelopmentManagerOptions`.
- The same file registers `AddDbContextFactory<AppDbContext>((sp, options) => ConfigureDb(...))`.
- `ConfigureDb(...)` currently supports:
  - `InMemory` / `Memory`
  - `Postgres` / `PostgreSql` through `UseNpgsql(...)`
  - `Sqlite` fallback through `UseSqlite(...)`
- Default SQLite location is `ContentRoot/.artifacts/workspace/candoitall.db`.
- `AddDataProtection().SetApplicationName("CanDoItAll")` is present, but there is **no key-ring persistence** configuration.

### Options/defaults

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs` currently defaults `DatabaseOptions.Provider` to `Sqlite`.
- `StorageOptions.WorkspaceRoot` defaults to `.artifacts/workspace`.
- `WorkbenchOptions.BrowserStorageKey` defaults to `candoitall.workbench.session`.

### Design-time factory

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Persistence/AppDbContextFactory.cs` creates `AppDbContext` for design-time operations based on `CANDOITALL_DATABASE_PROVIDER` and `CANDOITALL_DATABASE_CONNECTION`.
- The design-time factory does **not** configure `AppDbContextModelRegistry` with the module assemblies listed in `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Composition/ModuleAssemblies.cs`, so migrations generated there today would not see the full modular model.

## Current Startup Behavior

- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs` builds the app, resolves `IDbContextFactory<AppDbContext>`, creates a context, and executes:
  - `dbContext.Database.EnsureCreatedAsync()`
  - `WorkspaceSchemaInitializer.EnsureAsync(...)`
  - `ProjectsSchemaInitializer.EnsureAsync(...)`
  - `PromptFactorySchemaInitializer.EnsureAsync(...)`
  - `ProjectWorkbenchSchemaInitializer.EnsureAsync(...)`
  - `ProjectStructureAgentSchemaInitializer.EnsureAsync(...)`
- The same startup file resolves `IWorkspacePathResolver` once and binds `/managed-files` to a **fixed** `PhysicalFileProvider(workspaceResolver.ResolveManagedFilesRoot())`.

## Current Schema Strategy

### EF model

- `AppDbContext` itself is minimal and relies on modular `IEntityTypeConfiguration<>` discovery through the model registry.
- The repo currently defines **39 mapped tables** across workspace, projects, prompts, factory, validation, test lab, security, activity, background jobs, search, and workbench domains.
- A table-by-table inventory is captured in `inventories/02-db-table-inventory.md`.

### Bootstrap path

- No committed EF Core migration files exist in the repository.
- The current schema authority for many module tables is a set of raw SQL initializer classes:
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/WorkspaceSchemaInitializer.cs`
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Projects/ProjectsSchemaInitializer.cs`
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactorySchemaInitializer.cs`
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs`
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureAgentSchemaInitializer.cs`
- Those initializers contain `if (!dbContext.Database.IsSqlite()) return;`, which means:
  - they do nothing for PostgreSQL
  - they assume SQLite is the dominant normal-path provider
  - provider parity is currently unproven
- Because `EnsureCreatedAsync()` bypasses EF migration history, legacy SQLite databases created by the current app will not contain `__EFMigrationsHistory`.

## Current Storage And File-Serving State

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs` resolves one workspace root relative to `ContentRootPath` and then derives:
  - managed files
  - exports
  - evidence
  - manager artifacts
- `LocalFileStore` and `ManagedArtifactStore` both use the single global workspace root.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` and `src/CanDoItAll.Modules.Factory/PromptFactoryService.Pack.cs` save user-visible media beneath `managed-files/...`.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureLocalFileOpener.cs` and `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureRuntimeLauncher.cs` resolve file paths from the active workspace root, which means switching storage roots must also update these host integrations.
- Because `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs` binds a startup-time `PhysicalFileProvider`, per-profile storage would currently serve the **wrong** file root after a database switch.

## Current Browser State And Workbench Restore

- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/BrowserWorkspaceStateStore.cs` hardcodes one browser key: `candoitall.workbench.session`.
- `C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/WorkbenchTabState.cs` defines `WorkbenchSessionSnapshot`, but it does **not** include an active database profile identifier or database fingerprint.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/WorkbenchTabState.cs` restores and persists tab state without any database-profile isolation.
- `README.md` explicitly documents that workbench tab state lives in local storage under the same global key.
- Result: if a runtime switch were introduced today, the app would attempt to restore tabs/routes/artifact state from the wrong database.

## Current Runtime Switch Blockers

### No control plane

- There is no app-level catalog of known databases, no persisted active-profile record, and no runtime switch coordinator.
- The app only understands provider/connection information from startup configuration.

### Circular secret dependency

- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs` stores encrypted secret records **inside the selected application database**.
- A database-profile catalog therefore cannot live in that same database because PostgreSQL credentials or SQLite source metadata would become unreadable until after the database choice had already been made.
- `IDataProtection` keys are not persisted today, which would also make cross-restart decryption of control-plane secrets unreliable.

### Stale route hazards

- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` uses `FirstAsync(item => item.Id == projectId)` in `GetStructureAsync(...)` and `GetCalendarAsync(...)`.
- If a user is on `/projects/{id}/structure` or `/projects/{id}/calendar` and switches to a database where that project does not exist, the current implementation path will throw instead of falling back safely.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` and `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor` do not currently implement a cross-database stale-route recovery flow.

### Cross-circuit runtime behavior

- The active database choice is implicitly app-wide because the current DI graph uses singleton path resolution and singleton/shared infrastructure services.
- A safe implementation therefore must handle **all open browser tabs/circuits**, not just the current page, when the active database changes.

## Current Service Inventory

The following source files directly inject `IDbContextFactory<AppDbContext>` and will be affected by runtime switching:

- `src/CanDoItAll.Modules.Activity/ActivityModels.cs`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs`
- `src/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `src/CanDoItAll.Modules.Prompts/PromptModels.cs`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Security/SecurityModels.cs`
- `src/CanDoItAll.Modules.TestLab/TestLabModels.cs`
- `src/CanDoItAll.Modules.Validation/ValidationModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAnalyticsService.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureLeaseService.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workspace/ProjectStructureAgentAdministrationService.cs`
- `src/CanDoItAll.Modules.Workspace/ProviderExecution.cs`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`
- `src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs`
- `src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs`

Positive note:

- Most of these services create/dispose a fresh `AppDbContext` per call, which makes them good candidates for a switchable factory once runtime resolution exists.

## Current UI Surfaces Affected

The current routed pages that load database-backed content include:

- `/` and `/dashboard` via `src/CanDoItAll.Web/Components/Pages/Home.razor`
- `/settings` via `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
- `/activity`
- `/automation`
- `/projects`
- `/prompt-gallery`
- `/resources`
- `/validation`
- `/test-lab`
- `/prompt-factory`
- `/projects/{projectId}/structure`
- `/projects/{projectId}/calendar`

Global shell state lives primarily in `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor`, which is the correct place for:

- the active-database badge
- the global switch entry point
- the startup continue/switch modal
- database-switch notifications that force a safe reload

## Current Test Baseline

### Existing coverage

- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs` already proves that provider selection can switch to `InMemory`.
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkbenchStateServiceTests.cs` already proves restore logic and snapshot compatibility markers.
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/TestApplication.cs`, `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Components/ComponentTestHarness.cs`, and `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs` give reusable test entry points.

### Current gaps

- No tests currently prove runtime provider switching.
- No tests currently prove PostgreSQL bootstrap or PostgreSQL schema parity.
- No tests currently prove browser-side reloading after database change.
- No tests currently prove profile-specific workbench restore keys.
- No tests currently prove clone/snapshot behavior or managed-file continuity across switches.

## Bottom Line

- The repo is **not** PostgreSQL-only; it already contains a startup-time SQLite path.
- The real problem is that database choice is not elevated into a runtime-managed concept.
- The feature needs a new control plane, a switchable runtime factory, migration-based schema management, profile-scoped storage, stale-route handling, UI flows for selection/creation/switching, and a much stronger test matrix.
