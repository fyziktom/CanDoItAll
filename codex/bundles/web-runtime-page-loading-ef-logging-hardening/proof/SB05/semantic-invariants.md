# SB05 Semantic Invariants

- Invariant ID: `WEB-SB05-001`
- Source raw note: `REQ-EF-001`.
- Expected behavior: EF command/infrastructure console noise is opt-in and disabled by default through strongly typed database configuration.
- Disallowed shallow implementation: Removing EF logging globally, relying on environment-only behavior, or adding an untested magic string setting.
- Failing-first test: N/A process because this is configuration hardening; `bundle://proof/SB05/transcripts/negative-probe.md` guards against direct sensitive/log-to console paths in the web/infrastructure startup area.
- Passing test: `DatabaseOptions_DisablesEntityFrameworkConsoleLogging_ByDefault` and `DatabaseOptions_BindsEntityFrameworkConsoleLoggingSwitch` prove the default and binding behavior.
- Changed source files: `repo://src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`, `repo://src/CanDoItAll.Web/Program.cs`, `repo://src/CanDoItAll.Web/appsettings.json`, and `repo://tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs`.
- Production assertions: The web host applies EF category filters only when the option is false, while general application logging remains active.
- Red-team negative case: Startup log inspection reported zero EF command-log matches while other host and application logs still appeared.
- Downstream dependency check: SB05 is the final closure gate for SB02, SB03, and SB04; targeted component tests, unit tests, build, and startup all passed.
