# Governance browser fixture

This test-only host renders the real Web Governance tab against an isolated test profile. It is outside the stable solution because browser acceptance is run explicitly. It adds no production endpoint or service registration.

The profile uses the repository PostgreSQL fixture and canonical workspace adapters. Execution history uses the real profile-scoped file store. The seeded provider is for rendering only; no provider or tool is invoked. The test decorator can delay or fail catalog/list/detail publication without changing canonical records.

From the repository root, build the production theme and fixture:

```sh
npm run tailwind:build
dotnet build tests/Playwright/GovernanceBrowserFixture/GovernanceBrowserFixture.csproj -c Release /m:1
dotnet run --project tests/Playwright/GovernanceBrowserFixture -c Release --no-build -- prepare .
```

Prepare once. A second prepare fails explicitly to avoid replacing an existing fixture. Private environment and database credentials remain under ignored `.artifacts/governance-final/browser-data`; never retain or publish them.

Set `GOV_PLAYWRIGHT` to the installed Playwright module directory, then run:

```sh
node tests/Playwright/GovernanceBrowserFixture/browser.cjs . web
```

The script starts and stops only its own host/browser. Web uses port 5285; its loopback fault-control endpoint uses 17315. Scenarios cover accepted selections, noncooperative late completion, independent retries, removed targets, encoded long text, UTC, keyboard selection and responsive bounds. The script retains only an aggregate result and four Web screenshots in the ignored task directory.

For the same Governance Surface in the existing sandbox, build that project with `CatalogAssetMode=Parity` or `Fast`, prepare the corresponding assets, then pass `Parity` or `Fast` instead of `web`. These runs use ports 5395/5396 and one screenshot each. They register no production services.

`smoke.cjs` runs the explicitly frozen lightweight watch protocol. Its plan and final summary are described in the [Governance closure](../../../codex/bundles/UI_AgentGovernance_01_State_Read_Seams_Bundle/report.md). It restores exact edited bytes, stops owned process trees, and rejects concurrent source changes. Run it sequentially without parallel builds or tests.
