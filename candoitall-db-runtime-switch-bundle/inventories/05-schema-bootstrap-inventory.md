# Schema Bootstrap And Test Harness Inventory

## Current Startup/Test Bootstrap Call Sites

| Source | Current Behavior | Change Required |
| --- | --- | --- |
| `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Program.cs` | `EnsureCreatedAsync()` + five schema initializers | Replace with migration/bootstrap service and profile-aware startup |
| `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Integration/TestApplication.cs` | SQLite test DB + `EnsureCreatedAsync()` + initializers | Upgrade harness for runtime profile selection and migration path |
| `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Components/ComponentTestHarness.cs` | SQLite test DB + `EnsureCreatedAsync()` + subset of initializers | Upgrade for profile-aware component testing |
| `tests/CanDoItAll.Tests.Integration/ProjectStructureAgentApiTestHost.cs` | `EnsureCreatedAsync()` in dedicated host | Migrate to the shared bootstrap path |
| `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs` | In-memory provider proof with `EnsureCreatedAsync()` | Extend to profile runtime override behavior |
| `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs` | Starts app with environment-driven SQLite provider | Extend fixture to seed and switch multiple profiles |

## Current SQLite-Only Initializers

- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workspace/WorkspaceSchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Projects/ProjectsSchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Factory/PromptFactorySchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/ProjectStructureAgentSchemaInitializer.cs`

## Migration-Planning Notes

- Provider-specific migration assemblies are recommended because the current design-time factory does not compose the full modular model.
- Legacy SQLite onboarding must account for DBs that already contain tables but no EF migration history.
- The final implementation should centralize schema bootstrap so production, integration, component, and browser harnesses all use the same path.
