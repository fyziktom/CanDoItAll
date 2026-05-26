# Phase Plan

## Execution order

1. SB01 verify phase8 fixes and build gate.
2. SB02 separate live Tetris run profile from seeded baseline.
3. SB03 harden Blazor WASM PWA template step contracts.
4. SB04 build role/agent capability-skill-tool matrix.
5. SB05 add/upgrade Blazor/PWA/browser/project-structure skills.
6. SB06 refactor checkpoint A.
7. SB07 UI import/start preflight.
8. SB08 agent assignment and tool profile validation.
9. SB09 work briefs that expose agent limitations.
10. SB10 artifact contracts, lineage, and current-run proof.
11. SB11 project-structure writeback proof.
12. SB12 runtime health debuggability.
13. SB13 generic template regression, including non-software and agent-training process patterns.
14. SB14 real UI test Playwright harness preparation.
15. SB15 refactor checkpoint B.
16. SB16 final red-team closure and live-test runbook.

## Required validation commands

```powershell
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessDefinitionLinterTests|FullyQualifiedName~ApiIntegrationTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~ProcessStepEditorFormTests|FullyQualifiedName~ProcessTemplate"
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests Templates codex -S
```

## Template audit commands

```powershell
rg -n '"AllowedOperations"|"OperationTargetScope"' Templates/Processes/processes -S
rg -n '"MutateProductTarget"|"ExternalProductTargetMutable"' Templates/Processes/processes/blazor-* -S
rg -n 'baseline-blazor-wasm-pwa-tetris|Tetris|WASM PWA' Templates/Processes -S
rg -n 'project_structure_asset_create|project_structure_node_create|project_structure_' src Templates codex -S
```
