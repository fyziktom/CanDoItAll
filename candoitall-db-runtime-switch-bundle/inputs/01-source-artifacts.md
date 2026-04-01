# Source Artifacts

## Primary Artifacts

- `/mnt/data/CanDoItAll-toolbox-repair.zip` — uploaded repository archive supplied by the user.
- `C:\repositories\CanDoItAll` — extracted working copy used for static repo analysis.
- `C:\repositories\CanDoItAll/README.md` — repo-level behavior notes, defaults, and current database/storage description.
- `C:\repositories\CanDoItAll/docker-compose.yml` — existing PostgreSQL container definition and credentials.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` — current provider registration and `AddDbContextFactory` setup.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs` — startup bootstrapping, schema initialization, and managed-files middleware.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs` — workspace-root and managed-files path resolution.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/BrowserWorkspaceStateStore.cs` — browser local-storage persistence for workbench state.
- `C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/WorkbenchTabState.cs` — workbench session/tab snapshot contracts.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/WorkbenchTabState.cs` — workbench restore and persistence behavior.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor` — current settings UI where the database profile UX should land.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor` — current global shell where the active database indicator and startup modal should land.

## Current Schema/Bootstrap Artifacts

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Persistence/AppDbContextFactory.cs` — design-time factory currently missing module-assembly composition.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/WorkspaceSchemaInitializer.cs` — SQLite-only manual SQL bootstrap.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Projects/ProjectsSchemaInitializer.cs` — SQLite-only manual SQL bootstrap.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactorySchemaInitializer.cs` — SQLite-only manual SQL bootstrap.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs` — SQLite-only manual SQL bootstrap.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureAgentSchemaInitializer.cs` — SQLite-only manual SQL bootstrap.

## Existing Test Harness Artifacts

- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs` — current provider-configuration unit coverage.
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkbenchStateServiceTests.cs` — current workbench session restore coverage.
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/TestApplication.cs` — integration test application setup, currently SQLite + `EnsureCreated`.
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Components/ComponentTestHarness.cs` — component-test DI/bootstrap harness, currently SQLite + `EnsureCreated`.
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs` — browser-test fixture, currently environment-variable-driven SQLite startup.

## Environment Limitations During Bundle Preparation

- Static code analysis was completed in this environment.
- Runtime build, `dotnet test`, migration generation, and Playwright execution were **not** run because the container does not have the .NET SDK installed.
- The bundle therefore defines mandatory proof commands and stop-the-line rules for the execution agent instead of pretending that runtime validation already happened.
