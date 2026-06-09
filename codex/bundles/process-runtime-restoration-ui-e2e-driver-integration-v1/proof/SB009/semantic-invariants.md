# SB009 Semantic Invariants

- Invariant ID: `SB009_INV_001`
- Source raw note: Web application startup must be proven on the current branch with current composition and DI.
- Expected behavior: The current web composition starts a `WebApplication`, validates the service provider, initializes the test database, reports ready health over HTTP, exposes process templates over the process API, and resolves core process runtime services from DI.
- Disallowed shallow implementation: a test that only builds a `ServiceProvider`, only checks non-empty output, skips startup, bypasses endpoint mapping, omits database bootstrap, or avoids process runtime service resolution.
- Passing test: `bundle://proof/SB009/transcripts/focused-app-startup-smoke-test.txt`
- Build proof: `bundle://proof/SB009/transcripts/web-project-build.txt`
- Source assertion proof: `bundle://proof/SB007/transcripts/startup-composition-inventory.txt`, `bundle://proof/SB009/transcripts/startup-smoke-source-assertions.txt`, and `bundle://proof/SB009/transcripts/semantic-positive-source-audit.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ApplicationStartupIntegrationTests.cs`
- Production assertions: Web startup composition registers process module services through `AddCanDoItAllRuntimeModules`, maps process API routes, maps the Blazor app root with module assemblies, and exposes the process template catalog through HTTP.
- Red-team negative case: `bundle://proof/SB009/transcripts/forbidden-drift-scan.txt` and `bundle://proof/SB009/transcripts/anti-stub-audit-startup-test.txt`
- Downstream dependency check: SB010-SB018 may rely on app startup, process module DI, process API template visibility, and health readiness being source-backed before UI route/catalog and process-start proof begins.
