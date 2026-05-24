# Final validation commands

```powershell
git status --short
git diff --name-status development..HEAD

dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx -m:1 -v:minimal

dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build -v:minimal
dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Browser&Category!=LiveProcess" -v:minimal

.\codex\bundles\postgresql-only-main-runtime-followup-v1\scripts\sqlite_residue_audit.ps1
```
