# Repository Summary (expected structure)

This bundle assumes the app has these key parts (adjust paths if your repo differs):

- `src/App.Blazor/Pages/Harmony.razor`  
  UI host for harmonic assistant + canvas element, binds to session services.

- `src/App.Web/wwwroot/harmonicAssistantCanvas.js`  
  Canvas renderer with pointer handlers and in-canvas controls.

- `src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs`  
  JS interop to init/render/resize/dispose canvas.

- `src/App.Blazor/Services/HarmonicAssistantSessionService.cs`  
  Maintains state: history, suggestions, render snapshot.

- `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs`  
  Consumes WebMIDI and pushes detected chord events.

- `src/MusicTheory.Core/Recognition/*`  
  Real-time note/chord detection.

- `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`  
  Route planning engine.

- `docs/harmonic-assistant/*`  
  Design notes, roadmap, validation plan.

- `tests/*`  
  Unit tests for recognition and generation.

Primary goal of v2.1:
- Move UI complexity into canvas widgets, keep Blazor mostly as host.
- Keep heavy algorithms in C# (parallelizable), keep rendering in JS canvas.
