# SB05 Tests Build Startup Passing Transcript

- Invariant ID: `WEB-SB05-001`
- Test name: `DatabaseOptions_DisablesEntityFrameworkConsoleLogging_ByDefault`
- Test name: `DatabaseOptions_BindsEntityFrameworkConsoleLoggingSwitch`

Command:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~DatabaseConfigurationTests" --no-restore -v:minimal
```

ExitCode: 0

Output:

```text
Passed: 6
Failed: 0
```

Command:

```powershell
dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -v:minimal
```

ExitCode: 0

Output:

```text
Build succeeded.
23 Warning(s)
0 Error(s)
Warnings are existing MSB3277 EF Core relational version conflict warnings.
```

Command:

```powershell
dotnet run --no-build --no-launch-profile --project src\CanDoItAll.Web\CanDoItAll.Web.csproj with ASPNETCORE_ENVIRONMENT=Development, poll dev readiness endpoint, then count EF command log matches
```

ExitCode: 0

Output:

```text
Readiness summary: Ready
Started process id: 27608
Runtime process id: 40916
EF command-log match count: 0
Startup stdout log: repo://artifacts/web-runtime-hardening-startup.out.log
Startup stderr log: repo://artifacts/web-runtime-hardening-startup.err.log
```
