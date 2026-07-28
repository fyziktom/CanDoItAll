# Testing

The routine verification contract is the filtered Release gate. Browser, live-process, long-running, and quarantined tests are separate extended gates and must not be described as passing unless their exact commands pass.

## Prerequisites

The primary solution retains compatibility coverage for the retired Cognitive Memory module. That test coverage references these sibling source repositories:

```text
<parent>\
  CanDoItAll\
  CanDoItAll.AgentFramework.Rag\
  CanDoItAll.AgentFramework.SemanticCompletion\
```

Place those repositories beside this one before restoring `CanDoItAll.slnx`. DotNetWatch integration tests additionally require the sibling `CanDoItAll.Mcp` repository.

The web project itself does not require the retired Cognitive Memory siblings for the quick-start build.

## Stable Release Gate

Run from the repository root:

```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test .\CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined" /m:1
```

`/m:1` avoids `bin` and `obj` contention when local MCP or watch processes are active. A developer with an isolated workspace may increase parallelism, but the result must still come from the same configuration and filter.

The filter intentionally excludes:

- browser automation
- process-spawning and live-host integration
- long-running suites
- tests with an explicit `Quarantined` trait

Quarantine is not a passing result. Remove a quarantine only with focused replacement evidence and a passing owning gate.

## Documentation

```powershell
& .\tools\Validation\Test-Documentation.ps1
```

Run this after changing maintained Markdown, repository metadata, public paths, or source-truth claims represented by the validator.

## Focused HTTP Integration

For CRM/HR API changes, run the real HTTP-host slice before the broad gate:

```powershell
dotnet test .\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~CrmHrApiIntegrationTests" /m:1
```

This proof must create and read linked records through `/api/crm-hr`; direct service or database setup does not validate the HTTP boundary.

Use the same pattern for other API families: choose the narrowest real-host test slice first, then run the stable solution gate.

## Browser Gate

Build the Playwright project and install Chromium once per machine:

```powershell
dotnet build .\tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Playwright\CanDoItAll.Tests.Playwright\bin\Release\net10.0\playwright.ps1 install chromium
```

Run the non-quarantined browser gate:

```powershell
dotnet test .\tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration Release --no-build --filter "Category!=Quarantined" /m:1
```

Playwright hosts infer the active build configuration from the test output path. Set `CANDOITALL_TEST_CONFIGURATION` only for a non-standard output layout.

## Live-Process Gates

Run the application integration slice:

```powershell
dotnet test .\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --filter "Category=LiveProcess" /m:1
```

Run the sibling DotNetWatch integration project from this repository root:

```powershell
dotnet test ..\CanDoItAll.Mcp\tests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj --configuration Release --filter "Category!=Quarantined" /m:1
```

The DotNetWatch assembly uses this repository for workspace settings and runtime state. Its live and long-running tests remain outside the routine gate.

## Unfiltered Suite

```powershell
dotnet test .\CanDoItAll.slnx --configuration Release --no-build
```

Do not report the full suite as green unless this exact no-filter command passes. Expected quarantine failures, missing browser binaries, and unavailable sibling processes are still failures of this gate and must be reported as such.
