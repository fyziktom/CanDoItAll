# Implementation Prompt

Implement only the currently selected subbundle from `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle`.

- Re-read the root `README.md`, `plan/01-phase-plan.md`, the selected subbundle README, and the relevant traceability rows before editing code.
- Preserve the literal scope from the user request, especially the separate branch node, per-outcome plus default and error ports, role-definition input, and the rule that legacy nodes remain unchanged.
- Keep shared canvas contract changes in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`.
- Keep process-specific branch-node projection, authoring, and scenario changes in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes`.
- Update `analysis/03-architecture-troubles-log.md` whenever implementation exposes a real architectural gap, not only when it succeeds cleanly.
- Add or update the smallest correct tests in the nearby component or integration suites.
- If the subbundle is UI-relevant, use the live browser route and capture screenshot proof before marking it complete.
