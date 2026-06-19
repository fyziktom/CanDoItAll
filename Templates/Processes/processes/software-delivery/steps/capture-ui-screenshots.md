# Capture and store .NET UI screenshots

Launch and observe the .NET UI screenshot writeback subprocess after runtime command nodes exist. UI targets must capture screenshots and store accepted image assets under a Screenshots parent node below the current process run node. Backend-only or no-UI targets must produce explicit no-UI evidence.

If a previous child screenshot subprocess is Completed, Failed, Cancelled, or Blocked, treat it as historical evidence rather than an active wait. Inspect its artifacts, then complete from valid child evidence or relaunch the screenshot subprocess when required evidence is missing and relaunch is allowed. Do not return `Blocked` only because the stopped child exists.

Write the parent step record to `artifacts/process-runs/<current-process-run-id>/steps/capture-ui-screenshots.md` after the child run completes. The final `evidenceRefs` for this parent step must include that exact current-run step artifact path plus the child `screenshot-handoff` evidence and any accepted screenshot/no-UI receipts. Do not return `Completed` with only child-run artifact refs; the runtime produced-artifact contract for this parent step requires the current-run step artifact ref.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET UI screenshot project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.
- Operation target scope: `ExternalActionControlled`
