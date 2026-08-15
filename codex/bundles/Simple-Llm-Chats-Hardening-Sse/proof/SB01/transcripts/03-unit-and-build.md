# Unit, build, and model proof

## Application ordering

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Debug --no-restore -p:UseLocalCanDoItAllLibraries=true --filter "FullyQualifiedName~LlmChatConversationApplicationServiceTests" /m:1 --logger "console;verbosity=minimal"
```

Result: exit code 0; 5 passed, 0 failed, 0 skipped; 51 ms.

The first sandbox attempt could not write sibling Components outputs. The exact command was rerun with
the required workspace permission; no source change was made for that environmental retry.

## Startup build

```powershell
dotnet build src\App\CanDoItAll.Web\CanDoItAll.Web.csproj --configuration Debug --no-restore -p:UseLocalCanDoItAllLibraries=true /m:1 --nologo --verbosity:minimal
```

Result: exit code 0; 0 warnings, 0 errors. This refreshed the startup migration assembly.

## EF model gate

```powershell
$env:UseLocalCanDoItAllLibraries='true'; dotnet ef migrations has-pending-model-changes --no-build --project src\Foundation\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project src\App\CanDoItAll.Web\CanDoItAll.Web.csproj --context AppDbContext --configuration Debug
```

Result: exit code 0; `No changes have been made to the model since the last migration.`

EF tools 10.0.3 reported a non-blocking version warning against runtime 10.0.4.
