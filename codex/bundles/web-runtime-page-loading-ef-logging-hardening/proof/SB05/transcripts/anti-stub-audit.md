# SB05 Anti-Stub Audit Transcript

- Invariant ID: `WEB-SB05-001`

Command:

```powershell
rg -n 'EnableEntityFrameworkConsoleLogging|AddFilter\("Microsoft\.EntityFrameworkCore|DatabaseOptions_DisablesEntityFrameworkConsoleLogging_ByDefault|DatabaseOptions_BindsEntityFrameworkConsoleLoggingSwitch' src\CanDoItAll.Infrastructure\Configuration\AppOptions.cs src\CanDoItAll.Web\Program.cs src\CanDoItAll.Web\appsettings.json src\CanDoItAll.Web\appsettings.Development.json tests\CanDoItAll.Tests.Unit\DatabaseConfigurationTests.cs -S
```

ExitCode: 0

Output:

```text
src\CanDoItAll.Web\appsettings.Development.json:15:    "EnableEntityFrameworkConsoleLogging": false
tests\CanDoItAll.Tests.Unit\DatabaseConfigurationTests.cs:15:    public void DatabaseOptions_DisablesEntityFrameworkConsoleLogging_ByDefault()
tests\CanDoItAll.Tests.Unit\DatabaseConfigurationTests.cs:23:    public void DatabaseOptions_BindsEntityFrameworkConsoleLoggingSwitch()
src\CanDoItAll.Web\Program.cs:46:if (!databaseOptions.EnableEntityFrameworkConsoleLogging)
src\CanDoItAll.Web\Program.cs:48:    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
src\CanDoItAll.Web\Program.cs:49:    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning);
src\CanDoItAll.Web\appsettings.json:9:    "EnableEntityFrameworkConsoleLogging": false
src\CanDoItAll.Infrastructure\Configuration\AppOptions.cs:11:    public bool EnableEntityFrameworkConsoleLogging { get; set; }
```

Audit conclusion: no stub-only proof; the option is strongly typed, configured in appsettings, applied by web startup, and covered by unit tests.
