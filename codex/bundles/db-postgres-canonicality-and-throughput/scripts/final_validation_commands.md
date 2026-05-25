# Final validation commands

```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx -m:1 -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Quarantined" -v:minimal
dotnet ef migrations has-pending-model-changes --project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project .\src\CanDoItAll.Web\CanDoItAll.Web.csproj
pwsh .\codex\bundles\db-postgres-canonicality-throughput-followup-v3\scripts\audit_residue_and_bottlenecks.ps1
```
