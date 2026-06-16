# SB14 Story Coverage

| Story | Coverage | Evidence |
| --- | --- | --- |
| US-001 global process workspace route remains reachable | `/processes` renders the process shell and definition tab with projection-backed catalog counters. | `test-playwright-process-shell-sb14.txt`; `browser/processes-global-definition-catalog-playwright.png` |
| US-002 users can browse and select definitions | Catalog renders visible template-backed definitions and selection updates the projected selected-details panel. | `test-components-process-shell-sb14.txt`; `browser/processes-global-definition-catalog-mcp.png` |
| US-003 users can search and scope definitions | Search text flows into `ProcessDefinitionCatalogQueryProjection`; scope buttons reload the projection; project scope shows an explicit empty state when no project definitions exist. | `test-unit-definition-catalog-sb14.txt`; `browser/processes-global-definition-catalog-narrow-mcp.png` |
| US-004 users can feed default definitions | Feed Defaults dispatches `ProcessDefinitionFeedDefaultsCommand`, displays a receipt, records affected count and refresh token, and refreshes the shell projection. | `test-components-process-shell-sb14.txt`; `browser-validation.md` |

## Acceptance Criteria

| Criterion | Result | Proof |
| --- | --- | --- |
| AC-021 projection-backed process shell | Passed | `ProcessWorkspaceShellProjection` now carries `ProcessDefinitionCatalogProjection`; module build and component tests pass. |
| AC-022 definition browse/search/selection | Passed | Unit, component, Playwright, and Browser MCP proof cover search, selection, scope filter, and empty state. |
| AC-035 route-level browser proof | Passed | Playwright and Browser MCP screenshots/snapshots are stored under `proof/SB14/browser/`. |
| AC-039 application boundary for commands | Passed | Feed Defaults uses `ProcessDefinitionFeedDefaultsCommand` through `IProcessWorkspaceProjectionClient`. |
| AC-040 no runtime/persistence coupling from UI | Passed | UI forbidden runtime/persistence scan has 0 matches and CodeAnalytics dependency graph has no cycles. |

## Not Implemented By Design

- Definition editing forms remain out of scope for SB14 and are left to SB15.
- Project-specific definition persistence remains out of scope; the project scope is rendered with zero count and an explicit empty state.
- Runtime launch/live-history details remain out of scope for this catalog subbundle.
