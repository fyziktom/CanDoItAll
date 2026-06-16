# SB15 Browser Validation

## Route And Setup

- Route: `http://127.0.0.1:5500/processes`
- App host: `src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- Managed app session: `process-module-sb15`
- Browser tools: Browser MCP plus focused Playwright test.

## Desktop Flow

1. Opened `/processes`.
2. Dismissed startup modal with `database-startup-continue`.
3. Confirmed `processes-shell` loaded.
4. Searched definitions for `architecture`.
5. Selected `processes-definition-architecture-decision-governance`.
6. Opened `processes-definition-editor`.
7. Edited:
   - Name: `Architecture decision governance SB15 MCP final`
   - Owner: `Architecture board MCP final`
   - Manager override: `Use the architecture board manager for governance exceptions.`
8. Clicked Save draft.
9. Confirmed `Draft saved` receipt.
10. Clicked Publish.
11. Confirmed accepted published receipt and `Published` status.
12. Captured screenshot, accessibility snapshot, console summary, network summary, and JSON assertions.

## Desktop Result

`browser/desktop-editor-assertions.json`:

```json
{
  "editorVisible": true,
  "hasPublishedStatus": true,
  "blazorErrorVisible": false,
  "managerOverride": "Use the architecture board manager for governance exceptions."
}
```

Console/network:

- `browser/desktop-console-warnings.md`: 0 warnings, 0 errors at warning level.
- `browser/desktop-network.md`: Blazor initializer and negotiate requests returned 200 OK.

Screenshots:

- `browser/processes-definition-editor-desktop-mcp.png`
- `browser/processes-definition-editor-published-playwright.png`

## Narrow Viewport

- Viewport: 390x844.
- `browser/narrow-editor-assertions.json` reports:
  - `editorVisible`: true
  - `overflowCount`: 0
  - `blazorErrorVisible`: false

Screenshots:

- `browser/processes-definition-editor-narrow-mcp.png`
- `browser/processes-definition-editor-narrow-lint-actions-mcp.png`

## Playwright Test Proof

`test-playwright-process-shell-sb15.txt` passed 1/1. The smoke test covers `/processes`, definition search/selection, edit/save/publish with manager override, editor screenshot capture, Feed Defaults receipt, and project-scoped route rendering.
