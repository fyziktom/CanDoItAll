# Round 4 Readiness Gate

## Mandatory commands

Run in the repository root:

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
```

Then choose one of the following release policies.

### Policy A — full suite green

```bash
dotnet test CanDoItAll.slnx --configuration Release --no-build
```

This is the preferred outcome.

### Policy B — documented default gate plus explicit extended gates

Only acceptable if heavy/browser/live-process tests are intentionally categorized and documented.

```bash
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined"
```

Then run the relevant extended gates:

```bash
dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --configuration Release --no-build
RUN_LIVE_PROCESS_TESTS=true dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category=LiveProcess|Category=LongRunning"
```

## Mandatory targeted tests

Run tests covering:

- secret scanning;
- tool classification;
- process mutation approval/policy;
- finalizer sequence validation;
- typed recovery models;
- QA rework packet creation;
- proof fingerprint reuse/invalidation;
- retry ledger/backoff/loop control;
- Playwright Release/no-build launch helper;
- MCP stdio path resolution;
- ProjectStructure host stabilization.

## Completion criteria

- No raw secrets in source.
- Build passes.
- Default green gate passes.
- Full suite either passes or has documented, intentional extended/quarantined categories.
- No execution report claim lacks file/test evidence.
- `git diff --check` passes.
