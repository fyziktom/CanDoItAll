# Phase Plan

## Execution order

1. SB01 compile/build integrity.
2. SB02 API/tool/OpenAPI parity.
3. SB03 template inventory matrix.
4. SB04 Blazor template boundary corrections.
5. SB05 Tetris WASM PWA readiness.
6. SB06 refactor checkpoint A.
7. SB07 project-structure tool policy.
8. SB08 non-software template migration.
9. SB09 workflow/subprocess output mapping hardening.
10. SB10 unified artifact validation for API transitions.
11. SB11 refactor checkpoint B.
12. SB12 block/recovery health readiness.
13. SB13 process skill and documentation update.
14. SB14 baseline scenarios and seed pack.
15. SB15 UI test preflight.
16. SB16 final red-team and closure.

## Refactor checkpoints

- SB06: operation contract normalization, template projection, API schema helpers.
- SB11: artifact validation, transition validation, lineage mapping, runtime health.
- SB16: final polish and bundle proof closure.

## Minimum validation commands

```powershell
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessDefinitionLinterTests|FullyQualifiedName~ApiIntegrationTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~ProcessStepEditorFormTests"
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests Templates codex -S
```

## Template-specific audit commands

```powershell
rg -n '"AllowedOperations"|"OperationTargetScope"' Templates/Processes/processes -S
rg -n '"MutateProductTarget"|"ExternalProductTargetMutable"' Templates/Processes/processes/blazor-* -S
rg -n 'project_structure_asset_create|project_structure_node_create|project_structure_' src Templates codex -S
```
