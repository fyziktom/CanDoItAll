# Suggested validation commands

```powershell
git fetch origin
git merge-base --is-ancestor origin/development HEAD
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx -m:1 -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build -v:minimal
dotnet ef migrations has-pending-model-changes --project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project .\src\CanDoItAll.Web\CanDoItAll.Web.csproj --context AppDbContext
rg -n -i "sqlite|UseSqlite|Migrations.Sqlite|BeginSwitchAsync|WaitForDrainAsync|AcquireContextLeaseAsync|DatabaseSwitchSession|SqliteWriteCoordination" src tests CanDoItAll.slnx
```
