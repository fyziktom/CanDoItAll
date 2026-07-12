# Capture and store .NET UI screenshots

Launch and observe the .NET UI screenshot writeback subprocess before first-pass QA validation by calling `project_structure_process_subprocess_launch` for `dotnet-ui-screenshot-writeback`. This parent step does not own browser navigation, app startup, screenshot capture, image storage, or product triage; those actions belong to the child subprocess and then QA. UI targets must capture screenshots and store accepted or diagnostic image assets under a Screenshots parent node below the current process run node. Backend-only or no-UI targets must produce explicit no-UI evidence.

Use the architecture handoff, acceptance-driven validation plan, implementation evidence, peer-review note, and process-run context as the child inputs. Runtime command nodes may not exist before QA. When they are absent, the child must use the grounded app project and its direct launch fallback rather than treating missing command metadata as a blocker.

If a previous child screenshot subprocess is Completed, Failed, Cancelled, or Blocked, treat it as historical evidence rather than an active wait. Inspect its artifacts, then complete from valid child evidence or relaunch the screenshot subprocess when required evidence is missing and relaunch is allowed. Do not return `Blocked` only because the stopped child exists.

When a child screenshot handoff reports `visual-accepted` or `visual-defect-observed`, verify that the child evidence includes current-run `workspace_analyze_image` receipts for individual screenshots and `workspace_analyze_images` receipts for ordered comparisons or time-dependent behavior. `visual-defect-observed` is a completed evidence handoff for QA, not a child no-go. When it reports `no-ui-evidence-recorded`, verify the explicit no-browser-UI classification evidence and continue to non-browser QA; do not require browser, image-analysis, or image-asset receipts for that branch. Do not complete from screenshot paths, dimensions, or project-structure image asset ids alone when the downstream decision depends on visual content.

When source ImageAsset targets exist, verify that the child screenshot handoff includes a `Visual target comparison` section naming the source ImageAsset node id, media path or file name, screenshot ref, comparison method, and accepted or observed-defect disposition.

Write the parent step record to `artifacts/process-runs/<current-process-run-id>/steps/capture-ui-screenshots.md` after the child run completes. The final `evidenceRefs` for this parent step must include that exact current-run step artifact path plus the child `screenshot-handoff` evidence and any accepted screenshot/no-UI receipts. Do not return `Completed` with only child-run artifact refs; the runtime produced-artifact contract for this parent step requires the current-run step artifact ref.

## Contract
- Inputs: Scope boundary packet, architecture handoff, acceptance-driven validation plan, implementation evidence, peer-review note, and process run node context before QA.
- Outputs: Observed .NET UI screenshot project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, exactly `visual-accepted`, `visual-defect-observed`, or `no-ui-evidence-recorded`; provider-backed image-analysis receipts for visual screenshot decisions; explicit no-UI classification evidence when browser validation is inapplicable; Visual target comparison details when source ImageAsset targets exist; project-structure receipts, node ids, accepted or diagnostic screenshot assets or no-UI receipts, direct-launch fallback evidence when needed, and concrete execution blockers.
- Operation target scope: `ExternalActionControlled`
