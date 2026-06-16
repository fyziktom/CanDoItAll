# SB13 Story Coverage

| Covered input | Status | Implementation | Proof |
| --- | --- | --- | --- |
| REQ-030 | Covered | Projection-first shell routes render through application/projection DTOs. | `bundle://proof/SB13/test-components-process-shell.txt`; `bundle://proof/SB13/test-playwright-process-shell.txt` |
| REQ-051 | Covered | Contextual agent entry is supplied by `ProcessWorkspaceAgentEntryProjection`. | `bundle://proof/SB13/source-assertions.txt`; `bundle://proof/SB13/test-components-process-shell.txt` |
| REQ-052 | Covered | Refresh/freshness status is explicit and displays unavailable projection-store state. | `bundle://proof/SB13/test-components-process-shell.txt`; `bundle://proof/SB13/browser/processes-global-shell.png` |
| US-001 | Covered | `/processes` route renders the global workspace shell, tabs, command strip, and freshness cards. | `bundle://proof/SB13/test-playwright-process-shell.txt`; `bundle://proof/SB13/browser/processes-global-shell.png`; `bundle://proof/SB13/browser/processes-global-mcp-narrow.png` |
| US-020 | Covered | `/projects/{ProjectId}/processes?runId=...` renders a project-scoped shell and selects live runs. | `bundle://proof/SB13/test-playwright-process-shell.txt`; `bundle://proof/SB13/browser/processes-project-shell.png` |
| AC-021 | Covered | Process workspace shell remains browser reachable and route-aware. | `bundle://proof/SB13/test-playwright-process-shell.txt` |
| AC-035 | Covered | Command strip is projection-driven and exposes disabled command state explicitly. | `bundle://proof/SB13/test-components-process-shell.txt`; `bundle://proof/SB13/browser/processes-global-shell.png` |
| AC-039 | Covered | Agent entry point uses typed authorized application projection context. | `bundle://proof/SB13/test-components-process-shell.txt` |
| AC-040 | Covered | UI dependency scan shows no runtime/persistence references in the owned shell. | `bundle://proof/SB13/scans/ui-forbidden-runtime-persistence-scan.txt`; CodeAnalytics snapshot `snap-20260616003325-e1504595` |

## Deferred By SB13 Scope

- Definition catalog listing/editor behavior remains disabled for later UI subbundles.
- Launch/run creation remains disabled for later launch-planning and runtime UI subbundles.
- Live dashboard behavior remains a route entry point only; detailed live projections are downstream work.
