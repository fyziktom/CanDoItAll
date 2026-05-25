# Final validation commands

Run from repository root.

```powershell
git status --short
git log --oneline --decorate -n 12

# Branch must be current with development before final proof.
git fetch origin
git merge-base --is-ancestor origin/development HEAD

powershell -ExecutionPolicy Bypass -File .\codex\bundles\db-postgres-runtime-unblock\scripts\audit_residue_and_bottlenecks.ps1

dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx -m:1 -v:minimal

dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter FullyQualifiedName~SettingsPageDataSourcesTests -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Quarantined" -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~DatabaseSwitchIntegrationTests|FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessSubprocessIntegrationTests|FullyQualifiedName~ProcessWorkflowExecutorIntegrationTests|FullyQualifiedName~ConnectorOutboxIntegrationTests|FullyQualifiedName~ProcessOutboxIntegrationTests|FullyQualifiedName~AutomationRuntimeIntegrationTests" -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter FullyQualifiedName~DatabaseSwitchWorkbenchPlaywrightTests -v:minimal

# EF drift proof: adjust startup project if needed.
dotnet ef migrations has-pending-model-changes --project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project .\src\CanDoItAll.Web\CanDoItAll.Web.csproj
```
