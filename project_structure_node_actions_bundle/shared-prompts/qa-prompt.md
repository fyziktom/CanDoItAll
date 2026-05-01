# QA Prompt

Validate the implemented subbundle against the raw request, not only the code diff.

For UI subbundles:

- Open `/projects/{projectId}/structure`.
- Use a large desktop viewport first.
- Open the double-click quick-action modal and context menu for the relevant node type.
- Confirm labels are readable without zooming.
- Confirm the overlay is not clipped by the canvas, viewport, or floating windows.
- Confirm no action label overlaps adjacent content.
- Confirm menu/dialog z-order is above neighboring canvas chrome.
- Capture screenshots and record paths in `reviews/01-execution-report.md`.

For host actions:

- Confirm code paths still use guarded Workbench services.
- If PowerShell, UAC, File Explorer, or OS-level browser launching cannot be fully automated, record the exact validation gap and the closest deterministic proof.

For MCP/internal agent contracts:

- Confirm `project_structure_read` and internal compact nodes expose explicit action capability data.
- Confirm descriptions explain capability semantics without implying the agent can launch host actions remotely.

Closure:

- Mark each raw note as `Solved`, `Partially solved`, or `Not solved`.
- Reopen the owning subbundle for any partial note.
