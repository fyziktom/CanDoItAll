# Capture and store .NET UI screenshots

Launch and observe the .NET UI screenshot writeback subprocess after runtime command nodes exist. UI targets must capture screenshots and store accepted image assets under a Screenshots parent node below the current process run node. Backend-only or no-UI targets must produce explicit no-UI evidence.

Before launching screenshot capture for a UI target, verify the runtime command handoff includes a launcher-compatible Run app node or concrete degraded browser evidence with an actual base URL. If the Run app node is not launcher-compatible and no degraded URL evidence exists, block with the missing command metadata and why screenshots cannot be captured instead of returning `Completed` without screenshots.

If a previous child screenshot subprocess is Completed, Failed, Cancelled, or Blocked, treat it as historical evidence rather than an active wait. Inspect its artifacts, then complete from valid child evidence or relaunch the screenshot subprocess when required evidence is missing and relaunch is allowed. Do not return `Blocked` only because the stopped child exists.

When a child screenshot handoff accepts or rejects screenshots for visual reasons, the parent step must verify that the child evidence includes current-run `workspace_analyze_image` receipts for individual screenshots and `workspace_analyze_images` receipts for ordered comparisons or time-dependent behavior. Do not complete from screenshot paths, dimensions, or project-structure image asset ids alone when the downstream decision depends on visual content.

Write the parent step record to `artifacts/process-runs/<current-process-run-id>/steps/capture-ui-screenshots.md` after the child run completes. The final `evidenceRefs` for this parent step must include that exact current-run step artifact path plus the child `screenshot-handoff` evidence and any accepted screenshot/no-UI receipts. Do not return `Completed` with only child-run artifact refs; the runtime produced-artifact contract for this parent step requires the current-run step artifact ref.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET UI screenshot project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, provider-backed image-analysis receipts for visual screenshot decisions, project-structure receipts, node ids, screenshot or no-UI receipts, runtime command compatibility evidence, and blockers.
- Operation target scope: `ExternalActionControlled`
