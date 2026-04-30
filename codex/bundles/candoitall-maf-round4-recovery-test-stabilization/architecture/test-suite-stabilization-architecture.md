# Target Test Suite Stabilization Architecture

## Goal

The repository needs a reliable verification model. The default gate should be green, reproducible, and fast enough for routine validation. Extended gates should cover browser, live-process, and long-running behavior explicitly.

## Test categories

Introduce or normalize xUnit traits:

```csharp
[Trait("Category", "Unit")]
[Trait("Category", "Component")]
[Trait("Category", "Integration")]
[Trait("Category", "Playwright")]
[Trait("Category", "LiveProcess")]
[Trait("Category", "LongRunning")]
[Trait("Category", "Quarantined")]
```

Prefer small helper attributes to reduce typo risk, for example `UnitTestAttribute` or category constants.

## Verification commands

### Build

```bash
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
```

### Default green test gate

Option A: full suite green.

```bash
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

Option B: stable default suite green, heavy suites explicit.

```bash
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined"
```

If Option B is used, update docs and CI so nobody mistakes it for full-suite green.

### Extended gates

```bash
# Browser tests
dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --no-build --filter "Category=Playwright"

# Live process tests
RUN_LIVE_PROCESS_TESTS=true dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category=LiveProcess|Category=LongRunning"
```

## Fixture rules

- Do not hardcode repository roots.
- Do not hardcode Debug paths.
- Use `AppContext.BaseDirectory` or MSBuild-provided project paths.
- Respect current test configuration (`Release` vs `Debug`).
- If a fixture uses `dotnet run --no-build`, pass the active configuration explicitly.
- Avoid shared ports; allocate ports dynamically or reserve safely.
- Avoid shared databases; use isolated temp roots and profiles.
- Dispose child processes robustly.
- Capture logs as test output artifacts.

## Obsolete tests

When removing obsolete tests:

1. Explain why the behavior is no longer valid.
2. Identify the replacement coverage, if any.
3. Avoid deleting coverage silently.
4. Prefer semantic/behavioral assertions over brittle implementation details.
