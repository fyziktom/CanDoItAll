# SB11 Semantic Invariants

| Invariant | Proof | Status |
| --- | --- | --- |
| `SB11_INV_REGRESSION_001` seeded default catalog parity and managed seed refresh behavior are preserved. | `integration-seed-filter-api-workflow-regression.txt` | `Passed` |
| `SB11_INV_ACCESS_001` process/workflow access scopes deny representative Skill, Tool, MCP server, and MCP tool capabilities through the shared evaluator, including denied-required diagnostics. | `unit-capability-runtime-regression.txt`, `source-assertions.txt` | `Passed` |
| `SB11_INV_API_001` external Tool setup failure, MCP list-tools failure, invalid selector validation, and denied required capability are repairable and typed through API endpoints. | `integration-seed-filter-api-workflow-regression.txt`, `AgentCapabilitySetupApiIntegrationTests` source assertions | `Passed` |
| `SB11_INV_PROCESS_001` process shell route, definition editor, role editor, step editor, template preview, live dashboard, and project-scoped process route still render and operate. | `playwright-large-screen-regression.txt`, process screenshots, `process-shell-browser-validation-summary.txt`, component matrix | `Passed` |
| `SB11_INV_WORKFLOW_001` workflow templates and runtime preview still execute and expose browser-visible runtime evidence. | `integration-seed-filter-api-workflow-regression.txt`, `component-setup-process-workflow-regression.txt`, `workflow-shell-runtime-large.png` | `Passed` |
| `SB11_INV_UI_001` capability setup UI and seeded capability list still expose repairable setup diagnostics and access preview behavior on a large screen. | `playwright-large-screen-regression.txt`, `agent-capability-setup-flow-large.png`, component matrix | `Passed` |
| `SB11_INV_STATIC_001` SB11 additions stay focused, below file-size guardrails, and do not introduce fallback/stub code or credential leaks. | `file-size-scan.txt`, `anti-stub-and-secret-scan.txt`, `changed-file-hashes.txt` | `Passed` |

## Diagnostic Shape

- External Tool negative setup returns `CapabilityDiagnosticCategory.JsonParse` with `$.jsonInput` before launch.
- MCP negative setup returns `CapabilityDiagnosticCategory.McpListTools` through the setup service, masks the raw sentinel value, and completes cleanup.
- Invalid policy selectors return `CapabilityDiagnosticCategory.AccessPolicy` validation issues with exact selector field path.
- Denied required capabilities return `SuppressedCapabilityDiagnostic` with the exact denying rule id, selector kind, correlation id, and repair hint.

## Browser Validation

- Capability route: `/agents?tab=capabilities&agentId={seeded-agent-id}`, viewport `1600x1000`.
- Process routes: `/processes`, `/processes/live`, `/projects/{projectId}/processes?runId={runId}`, viewport `1440x900`.
- Workflow route: `/agents/workflows`, viewport `1600x1000`.
- Small and medium viewport validation was intentionally skipped per user instruction because the app targets large screens only.
