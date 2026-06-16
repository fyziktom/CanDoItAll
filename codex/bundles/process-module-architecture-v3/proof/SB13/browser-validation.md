# SB13 Browser Validation

| Route | Viewport | Tool | Actions | Assertions | Artifacts | Result |
| --- | --- | --- | --- | --- | --- | --- |
| `/processes` | 1440x900 | xUnit Playwright | Navigate to global route; dismiss startup modal if present; wait for shell, command strip, definitions tab; screenshot. | HTTP 2xx; `processes-shell`, `processes-command-strip`, and `processes-tab-definitions` visible; no `#blazor-error-ui`. | `browser/processes-global-shell.png`; `test-playwright-process-shell.txt` | Passed |
| `/projects/{ProjectId}/processes?runId=55555555-5555-5555-5555-555555555555` | 1440x900 | xUnit Playwright | Create project through UI; navigate to project-scoped Process route with run selection; screenshot. | HTTP 2xx; `processes-shell` visible; `processes-tab-panel-liveruns` visible; no `#blazor-error-ui`. | `browser/processes-project-shell.png`; `test-playwright-process-shell.txt` | Passed |
| `/processes` | 1440x900 | Playwright MCP / in-app browser | Start isolated in-memory host; navigate; save accessibility snapshot and screenshot. | Page title `Processes`; global shell visible after startup modal confirmation. | `browser/processes-global-mcp-browser.png`; `browser/processes-global-mcp-snapshot.md` | Passed |
| `/processes` | 390x844 | Playwright MCP / in-app browser | Resize; dismiss startup modal; save accessibility snapshot and screenshot. | Narrow route shell readable; command strip wraps without overlap; no browser-host process left running. | `browser/processes-global-mcp-narrow.png`; `browser/processes-global-mcp-narrow-snapshot.md` | Passed |

## Console And Network Notes

- The xUnit Playwright proof asserts successful route responses and no Blazor error UI.
- The manual in-app browser session initially displayed the expected startup database modal because a development database override was configured. The modal was dismissed with the same `database-startup-continue` action used by the automated tests before capturing the corrected narrow route proof.
- No network failures were observed in route assertions.
