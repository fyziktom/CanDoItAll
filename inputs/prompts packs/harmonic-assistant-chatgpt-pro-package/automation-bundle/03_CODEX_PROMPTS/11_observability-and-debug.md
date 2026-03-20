# 11 — Observability + debugging hooks (lightweight)

Goal: make it practical to debug live MIDI sessions and evaluate quality improvements.

## Files to modify
- `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs`
- `src/App.Blazor/Services/HarmonicAssistantSessionService.cs`
- `src/App.Blazor/Pages/Harmony.razor`
- `src/App.Web/wwwroot/harmonicAssistantCanvas.js` (optional: render time metric)

## 1) Add lightweight structured logging
Inject `ILogger<...>` into:
- `RealtimeChordDetectionSessionService`
- `HarmonicAssistantSessionService`

Log at debug level (guarded by `logger.IsEnabled(LogLevel.Debug)`):
- detection evaluation latency
- top detected chord + confidence
- top inferred scale context
- engine update runtime + top suggestion probability

Do not spam logs; throttle (e.g., only once per stable change).

## 2) Add optional debug panel in Harmony page
Behind a toggle (e.g., `ShowDebug` checkbox):
- show:
  - top detection candidates
  - current pitch class scores (top 8)
  - inferred scale context candidates
  - top 3 suggestion paths with reasons
This is extremely useful for tuning.

## 3) (Optional) Canvas render time
In JS, measure render duration with `performance.now()` and expose the last render ms:
- either in console debug,
- or via a tiny DOM element overlay (optional)

## Acceptance criteria
- Debug panel helps verify blues inference and chord stability.
- No UI clutter when debug disabled.

## Self-check
- Manual: toggle debug while playing MIDI / using manual chord input.
