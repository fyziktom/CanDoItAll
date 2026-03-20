# 03 — Migrate remaining Blazor controls to canvas widgets

Goal: remove most UI controls from Blazor page and show them as canvas widgets.

Tasks:
1) In `Harmony.razor`:
   - Keep only:
     - canvas element
     - minimal status line (optional)
   - Remove remaining control panels and replace with:
     - a single “Settings” / “Modules” widget payload passed to JS
2) Create widget definitions (payload) in C#:
   - `CanvasWidgetSnapshot` + `CanvasWidgetItemSnapshot`
   - Include:
     - History length (slider)
     - Loop mode toggle
     - Pattern module toggle and bias strength slider
     - Recording controls (Start/Stop/Save/Load/Clear/Replay)
     - MIDI status display + reconnect button
3) Implement .NET handler `OnCanvasWidgetEvent`:
   - route to session services:
     - update settings
     - toggle modules
     - start/stop recording
     - import/export workflows

Acceptance:
- Blazor page has dramatically fewer controls; major controls now appear in canvas.
- All settings persist in session state and affect rendering/planning.

Self-check:
- Basic smoke test with MIDI disabled still works.
