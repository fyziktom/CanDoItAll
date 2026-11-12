# Plugins browser fixture

Test-only executable hosting the real Web entry point through WebApplicationFactory
and Kestrel on an owned loopback port. The parent leases PostgreSQL databases and
isolates package paths with `CanDoItAllTestEnvironment`.

Standard-input commands arm a single EF connection read after a real connection
commit, either holding it or throwing a controlled read error. The original
Plugins factory options, owners, SQL writes and services remain real. The fixture
reports actual application stopping so restart proof needs no arbitrary sleep.
No probe endpoint or test branch is added to production code.

The parent browser intercepts only `window.open` for OAuth; it validates the
provider host and new-tab isolation options without opening an external account.
See [PluginsBrowserTests](../CanDoItAll.Tests.Playwright/PluginsBrowserTests.cs).
