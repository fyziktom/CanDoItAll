# Validation Commands

```powershell
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessTemplateGovernanceTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ApiIntegrationTests.Api_openapi_exposes_focused_control_plane_routes"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.SeedBaselineAsync_supports_global_then_project_scoped_baselines_without_slug_collisions"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --no-build --filter "FullyQualifiedName~ProcessWorkspaceTests.Run_steps_dialog_SB15_INV_001_exposes_contract_branch_and_recovery_diagnostics_for_ui_preflight"
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests Templates codex -S
rg -n -i 'tetris|tetromino|falling block|gameplay|simple game loop' Templates/Processes codex/skills/candoitall-api-processes src tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs -S
rg -n 'project_structure_asset_create|project_structure_node_create|project_structure_' src Templates codex tests -S
```

The topic-specific search is a red-team assertion: production process templates, process skills, runtime code, and tests should not depend on a demo topic.
