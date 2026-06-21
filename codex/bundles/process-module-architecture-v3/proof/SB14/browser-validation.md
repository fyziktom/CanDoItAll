# SB14 Browser Validation

## Playwright Proof

Command:

```text
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter FullyQualifiedName~ProcessShellSmokeTests
```

Result:

```text
Passed: 1/1. See test-playwright-process-shell-sb14.txt.
```

Artifacts:

- `browser/processes-global-definition-catalog-playwright.png`
- `browser/processes-project-shell-playwright.png`

The Playwright test navigates to `/processes`, dismisses startup gating if present, searches for `architecture`, selects `architecture-decision-governance`, runs Feed Defaults, waits for the receipt, then creates a project and verifies `/projects/{ProjectId}/processes?runId=...`.

## Browser MCP Proof

Runtime:

- Managed app session started through dotnetwatch on `https://localhost:7271`.
- Final Browser MCP capture was taken after the source-generation loader fix and app restart.

Desktop proof:

- Route: `https://localhost:7271/processes`
- Viewport: `1440x900`
- Search: `architecture`
- Selected definition: `architecture-decision-governance`
- Receipt: `24 default process definition(s) are available from template pack 2.1.0-live-run-governance. Refresh token: feed-defaults:2.1.0-live-run-governance:20260616012314.`
- Blazor error UI: not visible.
- Artifacts: `browser/processes-global-definition-catalog-mcp.png`, `browser/processes-global-definition-catalog-mcp-snapshot.md`

Narrow proof:

- Viewport: `390x844`
- Search: `architecture`
- Scope selected: `Project (0)`
- Empty state visible: `No definitions match the current search`.
- Blazor error UI: not visible.
- Artifacts: `browser/processes-global-definition-catalog-narrow-mcp.png`, `browser/processes-global-definition-catalog-narrow-mcp-snapshot.md`

## Visual Review

- Desktop catalog is readable, shows the real 24-definition counter, selected definition details, Feed Defaults receipt, and wrapped result list.
- Narrow catalog stacks search actions and scope buttons, keeps the selected project scope visible, and renders the empty state without incoherent overlap.
- Screenshots show no visible Blazor error UI.
