# Assumptions And Risks

## Assumptions

- Contextual agent runs that mutate project/process data are initiated from the shared contextual window, so a component callback can notify the owning canvas host after successful run completion or approval continuation.
- Preserving `CanvasWorkbenchUiState` before reload is enough to preserve pan, zoom, selection, minimap/diagnostic state, and open CanvasLib floating windows owned by the parent canvas.
- The contextual chat floating window itself is local component state and will stay mounted when the parent refreshes the canvas surface.
- The export feature can include the latest 25 sessions rather than every historical session ever created, matching the thread-history dialog limit and avoiding excessive browser payloads.

## Critical Path Risks

- Subbundle 01 is a critical foundation because an incorrect refresh path can reset the canvas viewport or close windows, invalidating all UI follow-on work.
- Subbundle 02 changes nested interactive markup on the agent row; click/double-click regressions would block both new-thread and history-thread workflows.
- Subbundle 03 depends on execution-history APIs returning details with tool receipts; a shallow export would not satisfy the debug requirement.

## Validation Risks

- Real agent execution may require configured provider credentials; fallback proof may need component tests plus a browser check of UI controls if live run completion is unavailable.
- Browser file downloads can be hard to assert visually; use JS/DOM assertions plus targeted unit/component proof for export payload shape.
- Process and project canvases may need seeded data to reach routes with accessible agents.

## Reopen Triggers

- If a canvas refresh closes or moves existing floating windows, reopen Subbundle 01.
- If panning or zooming before agent completion does not survive refresh, reopen Subbundle 01.
- If the history icon steals the agent row double-click/new-thread behavior, reopen Subbundle 02.
- If exported JSON lacks execution log, approvals, artifacts, checkpoints, or tool receipts for runs that have them, reopen Subbundle 03.
