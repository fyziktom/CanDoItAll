# Phase Plan

## Order

1. Current head and proof audit.
2. MAF 1.6 feature adoption truth table v2.
3. Runtime symbol contract tests.
4. Agent tool-loop/context/finalizer E2E.
5. Session/stream-error persistence proof.
6. Tool approval and MCP policy proof.
7. A2A/handoff/workflow capability proof.
8. OpenTelemetry trace proof.
9. Refactor checkpoint A.
10. Artifact validation status model expansion.
11. Read-model/finalizer parity for all statuses.
12. API/UI/recovery visibility.
13. Artifact dedupe/content hash race hardening.
14. Recovery/operator approval final proof.
15. Step0 live smoke preflight harness.
16. Generic process regression.
17. Refactor checkpoint B.
18. Final go/no-go report.

## Commands

```powershell
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~Maf|FullyQualifiedName~Agent|FullyQualifiedName~ToolInvocation|FullyQualifiedName~ProcessArtifact|FullyQualifiedName~RuntimeSymbol"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Maf|FullyQualifiedName~Agent|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessTemplateGovernanceTests|FullyQualifiedName~ApiIntegrationTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~Process"
rg -n "Microsoft\.Agents\.AI.*Version=\"1\.3|1\.3\.0-preview" src tests -S
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests Templates codex -S
```
