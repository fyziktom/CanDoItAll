# SB09 Semantic Invariants

| Invariant | Proof | Status |
| --- | --- | --- |
| `SB09_INV_RUNTIME_HARDENING_001` MAF access adapters are split into focused partials and new split files stay below the 500-line guardrail. | `file-size-scan.txt`, `changed-file-hashes.txt` | `Passed` |
| `SB09_INV_RUNTIME_HARDENING_002` configured workspace/storage tools are attached only through tools allowed by `RuntimeCapabilityAccessPlan.InitialAllowedCapabilities`. | `source-assertions.txt`, `runtime-composition-tests.txt` | `Passed` |
| `SB09_INV_RUNTIME_HARDENING_003` the raw `WorkspaceToolsEnabled` attach branch no longer hides configured workspace tools after policy evaluation. | `hidden-filter-static-search.txt`, `runtime-composition-tests.txt` | `Passed` |
| `SB09_INV_RUNTIME_HARDENING_004` registered runtime-provider tool filtering appends shared access diagnostics from `EvaluateRuntimeToolAccess`. | `source-assertions.txt`, `runtime-composition-tests.txt` | `Passed` |
| `SB09_INV_RUNTIME_HARDENING_005` structured diagnostics survive template, tool, skill, MCP, timeout, cancellation, and cleanup failure paths. | `runtime-diagnostics-contract-tests.txt` | `Passed` |
| `SB09_INV_RUNTIME_HARDENING_006` process/workflow capability filtering behavior remains stable after hardening refactors. | `runtime-capability-filtering-integration-tests.txt` | `Passed` |
| `SB09_INV_RUNTIME_HARDENING_007` scoped codeanalytics dependency review reports no final scoped dependency cycle. | `codeanalytics-dependency-summary.md` | `Passed` |
| `SB09_INV_RUNTIME_HARDENING_008` focused performance/static scans found no new SB09 sync-over-async, sleeps, artificial delays, repeated hot-path template parsing, or unbounded external output reads. | `focused-performance-scan.txt`, `anti-stub-audit.txt`, `runtime-hardening-report.md` | `Passed with accepted existing findings` |

## Diagnostic Shape

- Access denials continue to use `SuppressedCapabilityDiagnostic` records with identity, optional rule id, scope, selector kind, category, reason, repair hint, and correlation id.
- Initial catalog/configured capability denials are copied into runtime state before attachment.
- Runtime-provider denials are appended to the same runtime `CapabilityAccessDiagnostics` collection.
- Workspace-tool disablement is represented as access-policy diagnostics; the configured-tool attach path no longer adds an independent raw-context suppression branch.

## Browser Validation

- `N/A`: SB09 has no browser-visible surface. Small and medium viewport UI validation was skipped per user instruction because the app targets large screens only.
