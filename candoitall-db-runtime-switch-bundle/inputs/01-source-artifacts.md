# Source Artifacts

## Primary Artifacts

- `/mnt/data/CanDoItAll-toolbox-repair.zip` — uploaded repository archive supplied by the user.
- `/mnt/data/work/CanDoItAll-toolbox-repair` — extracted working copy used for static repo analysis.
- `/mnt/data/work/CanDoItAll-toolbox-repair/README.md` — repo-level behavior notes, defaults, and current database/storage description.
- `/mnt/data/work/CanDoItAll-toolbox-repair/docker-compose.yml` — existing PostgreSQL container definition and credentials.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` — current provider registration and `AddDbContextFactory` setup.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Program.cs` — startup bootstrapping, schema initialization, and managed-files middleware.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs` — workspace-root and managed-files path resolution.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Infrastructure/BrowserWorkspaceStateStore.cs` — browser local-storage persistence for workbench state.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.SharedKernel/WorkbenchTabState.cs` — workbench session/tab snapshot contracts.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/WorkbenchTabState.cs` — workbench restore and persistence behavior.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor` — current settings UI where the database profile UX should land.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Components/Layout/MainLayout.razor` — current global shell where the active database indicator and startup modal should land.

## Current Schema/Bootstrap Artifacts

- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Persistence/AppDbContextFactory.cs` — design-time factory currently missing module-assembly composition.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workspace/WorkspaceSchemaInitializer.cs` — SQLite-only manual SQL bootstrap.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Projects/ProjectsSchemaInitializer.cs` — SQLite-only manual SQL bootstrap.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Factory/PromptFactorySchemaInitializer.cs` — SQLite-only manual SQL bootstrap.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` — SQLite-only manual SQL bootstrap.
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/ProjectStructureAgentSchemaInitializer.cs` — SQLite-only manual SQL bootstrap.

## Existing Test Harness Artifacts

- `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs` — current provider-configuration unit coverage.
- `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Unit/WorkbenchStateServiceTests.cs` — current workbench session restore coverage.
- `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Integration/TestApplication.cs` — integration test application setup, currently SQLite + `EnsureCreated`.
- `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Components/ComponentTestHarness.cs` — component-test DI/bootstrap harness, currently SQLite + `EnsureCreated`.
- `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs` — browser-test fixture, currently environment-variable-driven SQLite startup.

## Environment Limitations During Bundle Preparation

- Static code analysis was completed in this environment.
- Runtime build, `dotnet test`, migration generation, and Playwright execution were **not** run because the container does not have the .NET SDK installed.
- The bundle therefore defines mandatory proof commands and stop-the-line rules for the execution agent instead of pretending that runtime validation already happened.
