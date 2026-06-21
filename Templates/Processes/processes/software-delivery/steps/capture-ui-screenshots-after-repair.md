# Capture and store repaired .NET UI screenshots

Launch and observe UI screenshot writeback after repaired runtime command nodes exist. UI targets must store repaired screenshots under Screenshots below the process run node; no-UI targets must carry explicit no-UI evidence.

Before launching repaired screenshot capture for a UI target, verify the repaired runtime command handoff includes a launcher-compatible Run app node or concrete degraded browser evidence with an actual base URL. If the Run app node is not launcher-compatible and no degraded URL evidence exists, block with the missing command metadata and why screenshots cannot be captured instead of returning `Completed` without screenshots.

If a previous child screenshot subprocess is Completed, Failed, Cancelled, or Blocked, treat it as historical evidence rather than an active wait. Inspect its artifacts, then complete from valid child evidence or relaunch the screenshot subprocess when required evidence is missing and relaunch is allowed. Do not return `Blocked` only because the stopped child exists.

Write the parent step record to `artifacts/process-runs/<current-process-run-id>/steps/capture-ui-screenshots-after-repair.md` after the child run completes. The final `evidenceRefs` for this parent step must include that exact current-run step artifact path plus the child `screenshot-handoff` evidence and any accepted screenshot/no-UI receipts. Do not return `Completed` with only child-run artifact refs; the runtime produced-artifact contract for this parent step requires the current-run step artifact ref.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET UI screenshot project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, screenshot or no-UI receipts, runtime command compatibility evidence, and blockers.
- Operation target scope: `ExternalActionControlled`
