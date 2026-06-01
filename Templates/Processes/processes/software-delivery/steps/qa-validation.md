# Run QA validation and runtime or browser proof

Execute targeted regression, runtime/API/browser proof as applicable, and defect triage against the reviewed implementation package. When project structure, scope, or implementation evidence identifies a visible browser workflow, capture current-run process-visible browser artifacts under `artifacts/process-runs/<run-id>/browser/` before selecting quality-accepted.

## Contract
- Inputs: Peer-reviewed change set, changed-surface inventory, and release-scope assumptions.
- Outputs: Targeted QA result with runtime/API/browser evidence as applicable, regressions, warning and executed-test counts, shipped entrypoint/runtime consistency, residual quality risk, and an explicit accepted or repair-required branch. Browser-workflow quality acceptance requires process-visible screenshot, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, launch and cleanup receipts, and acceptance-state assertion.
- Evidence: Regression logs, warning-free validation output unless explicitly accepted, nonzero executed-test proof when tests are expected, shipped entrypoint plus referenced-runtime inspection, stale or unreferenced artifact assessment, runtime/API/browser proof as applicable, screenshots for UI surfaces, defect notes, and current-run process-visible browser artifacts when a visible browser workflow is in scope.
- Operation target scope: `ExternalProductTargetReadOnly`
