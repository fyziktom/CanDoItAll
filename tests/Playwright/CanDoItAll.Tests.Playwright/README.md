# CanDoItAll.Tests.Playwright

## Purpose

Browser regression suite for the CanDoItAll web application and its cross-module flows.

## Prerequisites

- Build the project in Release and install its Chromium binary once per machine.
- Provide reachable PostgreSQL. The fixture checks
  `CANDOITALL_TESTS_POSTGRES_CONNECTION`, then the repository's local development
  connection, and can start the Compose `postgres` service when Docker Compose is
  available.
- By default, the fixture reserves a loopback URL and starts a local web host.
  `CANDOITALL_PLAYWRIGHT_BASEURL` selects a specific URL; when a compatible host already
  reports ready at `/_dev/runtime`, the fixture attaches without creating its own
  database or storage lease. Use that external-host mode only for focused tests that do
  not require fixture-owned database or storage details.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Playwright/CanDoItAll.Tests.Playwright/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --no-build --filter "Category!=Quarantined" /m:1
```

The maintained browser gate excludes explicitly quarantined tests. Quarantine is not a
passing result; run an unfiltered command only when validating those tests deliberately.

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Tests.Playwright.csproj](CanDoItAll.Tests.Playwright.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep tests focused on observable behavior and use shared fixtures from CanDoItAll.Tests.Support where cross-project setup is needed.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
