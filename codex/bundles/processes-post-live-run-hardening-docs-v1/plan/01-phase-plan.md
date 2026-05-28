# Phase Plan

## Execution Order

1. Audit current successful live run and proof debt.
2. Map architecture and refactor boundaries.
3. Harden artifact validation/status/read-model semantics.
4. Harden artifact storage/lineage/dedupe/retention.
5. Refactor output grounding/final delivery.
6. Harden project-structure folder projection.
7. Harden manager chat.
8. Close MAF/tool/skill proof debt.
9. Update templates/live-run profiles.
10. Build skill/tool matrix.
11. Update API/tool parity.
12. Update docs/skills.
13. Improve observability.
14. Protect generic process scenarios.
15. Refactor test taxonomy and proof harness.
16. Runtime service refactor checkpoint.
17. Docs/template parity checkpoint.
18. Final red-team and release readiness.

## Required command groups

```powershell
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~Process|FullyQualifiedName~Maf|FullyQualifiedName~Agent|FullyQualifiedName~Tool"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProcessTemplateGovernanceTests|FullyQualifiedName~ApiIntegrationTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~Process"
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests Templates codex -S
```

## Proof strategy

Do not rely on a single broad timeout-prone command. Split into named suites:

- artifact validation status matrix
- artifact storage/lineage/dedupe
- output grounding
- manager chat resolver
- project-structure projection
- process API/tool surface
- template pack governance
- MAF/tool policy
- live-run smoke
