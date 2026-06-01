# Re-run QA validation and runtime or browser proof after repair

Re-run targeted runtime/API/browser validation as applicable against the repaired package and select an explicit accepted or unresolved-repair branch. When project structure, original QA findings, or repair evidence identifies a visible browser workflow, recapture current-run process-visible browser artifacts under `artifacts/process-runs/<run-id>/browser/` before selecting quality-accepted.

## Contract
- Inputs: Repair change set, original QA findings, and reviewed implementation package.
- Outputs: Recheck result with warning-free validation, nonzero executed-test proof when tests are expected, shipped entrypoint/runtime consistency, runtime/API/browser evidence as applicable, regression evidence, and explicit quality disposition. Browser-workflow repair acceptance requires fresh process-visible screenshot, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, launch and cleanup receipts, and acceptance-state assertion.
- Evidence: Regression logs, warning-free validation output unless explicitly accepted, nonzero executed-test proof when tests are expected, shipped entrypoint plus referenced-runtime inspection, stale or unreferenced artifact assessment, runtime/API/browser proof as applicable, screenshots for UI surfaces, repair verification, unresolved defects if any, and fresh current-run process-visible browser artifacts when a visible browser workflow is in scope.
- Operation target scope: `ExternalProductTargetReadOnly`
