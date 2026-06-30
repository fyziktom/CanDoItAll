# SB10 Semantic Invariants

| Invariant | Proof | Status |
| --- | --- | --- |
| `SB10_INV_UI_TOOL_001` the capability setup surface exposes `Tool` beside Skill and MCP, with non-raw normal-case configuration fields. | `source-assertions.txt`, `component-setup-flow-tests.txt`, `playwright-capability-setup-flow-large.txt` | `Passed` |
| `SB10_INV_SETUP_001` external Tool setup tests call the shared setup-test service and preserve typed diagnostic category, masked detail, repair hint, and correlation path. | `component-setup-flow-tests.txt`, `playwright-capability-setup-flow-large.txt`, screenshot | `Passed` |
| `SB10_INV_SETUP_002` MCP setup tests call the setup-flow service and return typed `ImplementationMissing` diagnostics when the host lacks a live MCP runtime adapter. | `component-setup-flow-tests.txt` | `Passed` |
| `SB10_INV_ACCESS_001` access preview uses the shared typed policy evaluator and reports allowed/suppressed capability sets without launching runtime execution. | `component-setup-flow-tests.txt`, `source-assertions.txt` | `Passed` |
| `SB10_INV_API_001` API endpoints expose Tool setup-test, MCP setup-test, and access-preview calls through typed request/response DTOs. | `source-assertions.txt`, `dotnet-build-agentframework-module.txt` | `Passed` |
| `SB10_INV_UI_DEFAULTS_001` a new Tool wizard no longer starts with invalid generated implementation key `external.`. | `failing-first-playwright-tool-default.txt`, `playwright-capability-setup-flow-large.txt` | `Passed` |
| `SB10_INV_STATIC_001` new setup-flow and wizard logic stays split by concern and below the local 500-line file-size guardrail. | `file-size-scan.txt`, `changed-file-hashes.txt` | `Passed` |

## Diagnostic Shape

- Tool setup diagnostics use `CapabilityDiagnostic` with category, severity, capability identity, field path, implementation key/transport when applicable, correlation id, masked detail, and repair hint.
- Invalid Tool JSON input returns `JsonParse` before process launch.
- MCP setup without a registered runtime adapter returns `ImplementationMissing` rather than silently skipping the test.
- Access-preview denials use `SuppressedCapabilityDiagnostic` through the shared evaluator; UI code does not duplicate policy matching rules.

## Browser Validation

- Route: `/agents?tab=capabilities&agentId={seeded-agent-id}`.
- Viewport: `1600x1000`.
- Evidence: `agent-capability-setup-flow-large.png`.
- Small and medium viewport validation was intentionally skipped per user instruction because the app targets large screens only.
