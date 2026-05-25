# Source-to-subbundle matrix

| Source path | Primary subbundle |
|---|---|
| `src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs` | SB01 |
| `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseDrivers.cs` | SB01/SB02 |
| `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs` | SB02 |
| `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs` | SB02 |
| `src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs` | SB07 |
| `src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | SB01/SB05 |
| `src/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj` | SB01 |
| `src/CanDoItAll.Migrations.Sqlite/` | SB01 |
| `src/CanDoItAll.Migrations.PostgreSql/` | SB08 |
| `src/CanDoItAll.Web/Infrastructure/RuntimeDatabaseSwitching.cs` | SB01/SB05 |
| `src/CanDoItAll.Web/Infrastructure/DatabaseMigrationBootstrap.cs` | SB01/SB05 |
| `src/CanDoItAll.Web/Program.cs` | SB03 |
| `src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor` | SB03 |
| `src/CanDoItAll.Modules.Workspace/DatabaseProfileWorkspaceService.cs` | SB02/SB03/SB07 |
| `tests/CanDoItAll.Tests.Support/*` | SB04 |
| `tests/CanDoItAll.Tests.Integration/*.csproj` | SB04 |
| `src/CanDoItAll.Modules.Processes/**` | SB06 |
| `src/CanDoItAll.Modules.Automation/**` | SB06 |
| `src/CanDoItAll.Modules.Plugins/**` | SB06 |
| `CanDoItAll.slnx` | SB01/SB08 |
