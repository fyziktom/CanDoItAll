# 09 — Recording + import/export + replay

Goal: users can record sessions, export JSON, import later, replay.

Tasks:
1) Implement `HarmonicSessionRecorder` in C#:
   - start/stop
   - add chord events from detection
   - export to JSON string
   - import from JSON string
2) UI widget events:
   - Start/Stop/Save/Load/Clear/Replay controls
3) Replay:
   - plays events back into the session state (as if detected)
   - supports speed control (1x, 2x, 0.5x)

Acceptance:
- Exported JSON re-imports and reproduces history + canvas graph.
