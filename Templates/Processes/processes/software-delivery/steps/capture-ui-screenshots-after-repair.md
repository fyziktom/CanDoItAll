# Capture and store repaired .NET UI screenshots

Launch and observe UI screenshot writeback after repaired runtime command nodes exist by calling `project_structure_process_subprocess_launch` for `dotnet-ui-screenshot-writeback`. This parent step does not own browser navigation, app startup, screenshot capture, or image storage; those actions belong to the child subprocess. UI targets must store repaired screenshots under Screenshots below the process run node; no-UI targets must carry explicit no-UI evidence.

Before launching repaired screenshot capture for a UI target, verify the repaired runtime command handoff includes a launcher-compatible Run app node or concrete degraded browser evidence with an actual base URL. If the Run app node is not launcher-compatible and no degraded URL evidence exists, block with the missing command metadata and why screenshots cannot be captured instead of returning `Completed` without screenshots.

If a previous child screenshot subprocess is Completed, Failed, Cancelled, or Blocked, treat it as historical evidence rather than an active wait. Inspect its artifacts, then complete from valid child evidence or relaunch the screenshot subprocess when required evidence is missing and relaunch is allowed. Do not return `Blocked` only because the stopped child exists.

When a child screenshot handoff accepts or rejects repaired screenshots for visual reasons, the parent step must verify that the child evidence includes current-run `workspace_analyze_image` receipts for individual screenshots and `workspace_analyze_images` receipts for ordered comparisons or time-dependent behavior. Do not complete from screenshot paths, dimensions, or project-structure image asset ids alone when the downstream decision depends on visual content.

When source ImageAsset targets exist, verify that the child screenshot handoff includes a `Visual target comparison` section naming the source ImageAsset node id, media path or file name, screenshot ref, comparison method, and accepted or blocked disposition.

Write the parent step record to `artifacts/process-runs/<current-process-run-id>/steps/capture-ui-screenshots-after-repair.md` after the child run completes. The final `evidenceRefs` for this parent step must include that exact current-run step artifact path plus the child `screenshot-handoff` evidence and any accepted screenshot/no-UI receipts. Do not return `Completed` with only child-run artifact refs; the runtime produced-artifact contract for this parent step requires the current-run step artifact ref.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET UI screenshot project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, provider-backed image-analysis receipts for repaired visual screenshot decisions, Visual target comparison details when source ImageAsset targets exist, project-structure receipts, node ids, screenshot or no-UI receipts, runtime command compatibility evidence, and blockers.
- Operation target scope: `ExternalActionControlled`
