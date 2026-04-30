# Test Suite Failure Taxonomy and Stabilization Analysis

## Current problem

Codex reported that targeted round 3 tests passed but `dotnet test CanDoItAll.slnx --configuration Release --no-build` still fails due to broad existing suites. The snapshot contains clear infrastructure problems that can cause those failures independent of the MAF recovery changes.

The goal is not to hide failing tests. The goal is to create a truthful, maintainable test strategy where the default verification gate is green and heavy/optional suites are explicit.

## Failure classes to address

### 1. Release/no-build mismatch in Playwright fixtures

`dotnet test -c Release --no-build` runs tests from Release output. Some Playwright fixtures then launch the web app with `dotnet run --no-build` without `--configuration Release`, which defaults to Debug output. That can fail when Debug was not built.

Affected files:

- `tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`
- `tests/CanDoItAll.Tests.Playwright/DatabaseSwitchWorkbenchPlaywrightTests.cs`

Reference pattern:

- `tests/CanDoItAll.Tests.Playwright/WebGlSandboxPlaywrightFixture.cs` uses a Release-aware command.

### 2. Hardcoded Windows repository roots and Debug assembly paths

Affected files:

- `tests/CanDoItAll.Tests.Integration/ProcessesMcpStdioIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectStructureMcpStdioIntegrationTests.cs`

Problems:

- hardcoded `C:epositories\CanDoItAll`;
- hardcoded `bin\Debug
et10.0`;
- not portable;
- incompatible with Release/no-build verification.

### 3. Live-process / long-running integration tests mixed into default gate

DotNetWatch and MCP stdio tests spawn processes and can be timing-sensitive. They should either be made deterministic and green under the default gate, or tagged and moved into an explicit extended gate.

### 4. Host lifetime replacement in ProjectStructure host tests

Codex reported host lifetime replacement failures. The host bootstrap path should be audited for duplicate/conflicting `IHostApplicationLifetime` registration and web-host specific services.

### 5. Brittle component/canvas tests

Component tests that assert exact markup, CSS ordering, transient canvas details, or implementation-specific DOM can become obsolete quickly. Convert them to semantic assertions, `data-testid` selectors, stable accessibility assertions, or move them to Playwright if browser behavior matters.

### 6. Storage/project-structure integration failures

Storage/project-structure integration tests need isolated temp roots, unique profiles, no shared global state, no hardcoded paths, and deterministic cleanup.

## Proposed test taxonomy

Use xUnit traits consistently:

- `Category=Unit`
- `Category=Component`
- `Category=Integration`
- `Category=Playwright`
- `Category=LiveProcess`
- `Category=LongRunning`
- `Category=Quarantined`

Default green gate should include stable categories only. Heavy suites can remain, but they must have explicit commands and prerequisites.

## Default vs extended gates

### Default gate target

```bash
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined"
```

Alternatively, make the full no-filter command green. Do not leave a silent gray zone.

### Extended gates

```bash
# Playwright
PLAYWRIGHT_BROWSERS_PATH=... dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --no-build

# Live MCP / dotnet-watch
RUN_LIVE_PROCESS_TESTS=true dotnet test ... --filter "Category=LiveProcess"
```

## Obsolete tests

If a test is obsolete, delete it or mark it quarantined with a written rationale. Do not leave obsolete tests failing in the broad suite and do not silently skip them without documentation.
