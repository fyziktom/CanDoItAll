# 02 — Canvas widget framework

Goal: generalize in-canvas controls beyond the current fixed mood + text controls.

Tasks:
1) In `harmonicAssistantCanvas.js`:
   - Add `renderer.widgets` array and a `WidgetRegistry` helper:
     - define widget panels and items
     - compute item rects (buttons/toggles/sliders)
   - Replace `drawMoodControls` and `drawTextControls` with widgets:
     - Widget: `mood`
     - Widget: `textAndZoom`
2) Add a unified pointer hit-test:
   - return `{ widgetId, itemId }`
3) Add unified callback to .NET:
   - `OnCanvasWidgetEvent(widgetId, itemId, eventKind, value)`
   - Keep existing `OnCanvasMoodChanged` temporarily (compat), but route new events via unified handler.

Acceptance:
- Existing functionality still works (mood controls + A-/A+).
- No regressions in hover tooltip.
- Widgets render and are clickable.

Self-check:
- Run the app and confirm buttons react.
