# Phase Plan

## Execution Order

1. Freeze evidence and create failing-first regression.
2. Resolve MAF 1.6 official versions and API surface.
3. Upgrade packages and pass restore/build.
4. Migrate agent factory/session/provider code.
5. Migrate tool approval, middleware, tracing, and finalizer capture.
6. Migrate handoff/A2A/workflow paths.
7. Refactor checkpoint A: MAF adapter boundary.
8. Diagnose process artifact validation failure from source.
9. Fix current-run binding and path normalization.
10. Fix content hash and lineage integrity.
11. Unify artifact satisfaction and final validation.
12. Fix recovery lifecycle and manager approval routing.
13. Expose diagnostics in API/UI.
14. Validate skills/tools/capabilities.
15. Rerun live Tetris process harness.
16. Run generic process/workflow regressions.
17. Refactor checkpoint B.
18. Final red-team closure.

## Required validation commands

```powershell
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Maf|FullyQualifiedName~Agent|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessTemplateGovernanceTests|FullyQualifiedName~ApiIntegrationTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~Process"
rg -n "Microsoft\.Agents\.AI.*Version=\"1\.3|1\.3\.0-preview" src tests -S
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests Templates codex -S
```
