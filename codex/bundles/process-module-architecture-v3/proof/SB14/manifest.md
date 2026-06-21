# SB14 Proof Manifest

## Implementation Proof

- `semantic-invariants.md`
- `story-coverage.md`
- `browser-validation.md`
- `codeanalytics-snapshot-summary.txt`
- `subbundle-closure-gate-sb14.md`
- `source-assertions.txt`

## Browser Artifacts

- `browser/processes-global-definition-catalog-mcp.png`
- `browser/processes-global-definition-catalog-mcp-snapshot.md`
- `browser/processes-global-definition-catalog-narrow-mcp.png`
- `browser/processes-global-definition-catalog-narrow-mcp-snapshot.md`
- `browser/processes-global-definition-catalog-playwright.png`
- `browser/processes-project-shell-playwright.png`

## Raw Scans

- `scans/ui-forbidden-runtime-persistence-scan.txt`
- `scans/ui-no-template-or-file-dependency-scan.txt`
- `scans/anti-stub-scan.txt`

## Build And Test

- `build-process-module.txt`
- `build-solution-sb14.txt`
- `test-unit-definition-catalog-sb14.txt`
- `test-components-process-shell-sb14.txt`
- `test-playwright-process-shell-sb14.txt`
- `bundle-validator-prepared-sb14.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative / Boundary Proof |
| --- | --- | --- | --- | --- |
| `ProcessDefinitionCatalogProjection` | `ProcessDefinitionCatalogProjectionService` | `ProcessWorkspaceShellProjectionService`, `ProcessWorkspaceShell.razor` | Built per shell request from typed scope, search text, selected key, scope filter, template pack summaries, and command receipt state. | `test-unit-definition-catalog-sb14.txt`; `scans/ui-forbidden-runtime-persistence-scan.txt` |
| `ProcessTemplatePackLoader` | `CanDoItAll.Processes.Templates` | Application catalog projection service | Loads canonical `Templates/Processes/manifest.json` and each `definition.json` through source-generated JSON metadata. | `test-unit-definition-catalog-sb14.txt`; `scans/ui-no-template-or-file-dependency-scan.txt` |
| `ProcessDefinitionFeedDefaultsCommand` | UI/application command boundary | `ProcessDefinitionCatalogProjectionService` | Produces an explicit command receipt, affected count, accepted timestamp, and refresh token. | `test-components-process-shell-sb14.txt`; `browser-validation.md` |
| Definition search and scope tree | `ProcessWorkspaceShell.razor` | `/processes`, project route, live route | Search text and selected key enter the typed projection query; scope changes reload the projection without deriving runtime truth in the component. | `test-components-process-shell-sb14.txt`; `test-playwright-process-shell-sb14.txt` |
| `/processes` definition catalog flow | Process routes and shell component | Browser users | Renders counters, search, global/project scopes, selected definition metadata, empty states, and Feed Defaults receipt. | `browser/processes-global-definition-catalog-mcp.png`; `browser/processes-global-definition-catalog-playwright.png` |
| Project-scoped route compatibility | `ProjectProcessesPage.razor`, `ProcessWorkspaceShell.razor` | Project workbench route | Preserves project process route rendering while definition catalog query parameters remain available. | `browser/processes-project-shell-playwright.png`; `test-playwright-process-shell-sb14.txt` |

## Story Coverage

| Story | Result | Proof |
| --- | --- | --- |
| US-001 process workspace route remains reachable | Covered | `/processes` Playwright assertions in `test-playwright-process-shell-sb14.txt`; screenshots in `browser/processes-global-definition-catalog-playwright.png` and `browser/processes-global-definition-catalog-mcp.png`. |
| US-002 definition catalog can be browsed and selected | Covered | Component search/selection tests in `test-components-process-shell-sb14.txt`; selected metadata in `browser/processes-global-definition-catalog-mcp-snapshot.md`. |
| US-003 definition catalog search and empty states are predictable | Covered | Unit projection filtering test in `test-unit-definition-catalog-sb14.txt`; narrow project-scope empty-state proof in `browser/processes-global-definition-catalog-narrow-mcp.png`. |
| US-004 default definitions can be fed through a command receipt | Covered | Feed Defaults component test and Browser MCP receipt proof in `browser-validation.md`. |

## File Integrity

- `changed-file-hashes.txt`
- `line-counts.txt`
