# SB13 Proof Manifest

## Implementation Proof

- `semantic-invariants.md`
- `story-coverage.md`
- `browser-validation.md`
- `appdbcontext-migration-snapshot-cleanup.md`
- `codeanalytics-snapshot-summary.txt`
- `subbundle-closure-gate-sb13.md`
- `source-assertions.txt`

## Browser Artifacts

- `browser/processes-global-shell.png`
- `browser/processes-project-shell.png`
- `browser/processes-global-mcp-browser.png`
- `browser/processes-global-mcp-snapshot.md`
- `browser/processes-global-mcp-narrow.png`
- `browser/processes-global-mcp-narrow-snapshot.md`

## Raw Scans

- `scans/ui-forbidden-runtime-persistence-scan.txt`
- `scans/anti-stub-scan.txt`

## Build And Test

- `build-process-module.txt`
- `build-solution-sb13.txt`
- `test-components-process-shell.txt`
- `test-playwright-process-shell.txt`
- `test-unit-process-slice-sb13.txt`
- `ef-pending-model-check.txt`
- `bundle-validator-prepared-sb13.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative / Boundary Proof |
| --- | --- | --- | --- | --- |
| `ProcessWorkspaceShellProjection` | `ProcessWorkspaceShellProjectionService` | `ProcessWorkspaceProjectionClient`, `ProcessWorkspaceShell.razor` | Built per route request from typed scope, selection, command, tab, freshness, and agent-entry DTOs. | `test-components-process-shell.txt`; `scans/ui-forbidden-runtime-persistence-scan.txt` |
| `IProcessWorkspaceProjectionClient` | `ProcessWorkspaceProjectionClient` | Process Blazor shell component | Keeps component loading behind an application service boundary. | `source-assertions.txt`; `test-components-process-shell.txt` |
| `/processes` route | `ProcessesPage.razor` | Main layout/workbench and direct browser navigation | Renders global Process shell and refreshes projection state through the client. | `test-playwright-process-shell.txt`; `browser/processes-global-shell.png`; `browser/processes-global-mcp-narrow.png` |
| `/projects/{ProjectId}/processes` route | `ProjectProcessesPage.razor` | Project workbench tab resolver | Renders project-scoped Process shell and preserves selected run tab state. | `test-playwright-process-shell.txt`; `browser/processes-project-shell.png` |
| Process shell navigation row | `ProcessesShellNavigationContributor` | Shared shell navigation | Adds `/processes` through `IShellNavigationContributor` without reviving legacy UI services. | `test-components-process-shell.txt`; `source-assertions.txt` |
| Active AppDbContext migration snapshot | `ProcessModuleArchitectureV3RuntimePersistence` migration | App database bootstrapper and Playwright host startup | Removes legacy AppDbContext `Processes_*` tables from the active migration snapshot after the legacy module was removed. | `ef-pending-model-check.txt`; `appdbcontext-migration-snapshot-cleanup.md` |
| Unavailable process runtime evidence provider | `UnavailableProcessRuntimeEvidenceSourceProvider` | AgentFramework runtime evidence resolver | Satisfies DI after legacy Process removal and throws a deterministic unavailable-source error if Process runtime evidence is requested before deployment. | `test-playwright-process-shell.txt`; `source-assertions.txt` |

## Story Coverage

| Story | Result | Proof |
| --- | --- | --- |
| US-001 process workspace route remains reachable | Covered | `/processes` Playwright assertions in `test-playwright-process-shell.txt`, screenshots in `browser/processes-global-shell.png` and `browser/processes-global-mcp-browser.png`. |
| US-020 project-scoped process context remains reachable | Covered | Project creation and `/projects/{ProjectId}/processes?runId=...` assertions in `test-playwright-process-shell.txt`, screenshot in `browser/processes-project-shell.png`. |

## File Integrity

- `changed-file-hashes.txt`
- `line-counts.txt`
