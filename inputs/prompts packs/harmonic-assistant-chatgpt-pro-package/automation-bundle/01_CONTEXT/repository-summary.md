# Repository summary (Realtime Harmonic Assistant)

## High-level architecture
The realtime assistant at route `/harmony` consists of five layers:

1) **UI layer (Blazor)**
- `src/App.Blazor/Pages/Harmony.razor`
- Owns UI controls (brightness/colorfulness/style/section/lock key toggle, manual chord input)
- Subscribes to session update events
- Builds a canvas snapshot DTO and calls JS interop to render

2) **Session orchestration**
- `src/App.Blazor/Services/HarmonicAssistantSessionService.cs`
- Subscribes to chord detection session updates
- Debounces updates (~95ms) and calls the engine
- Supports manual chord fallback (parses chord text, constructs a synthetic detection result)
- Maintains `CurrentUpdate` and emits `UpdateChanged`

3) **Realtime input detection session**
- `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs`
- Starts WebMIDI (`IMidiService.InitializeAsync`)
- Maps MIDI messages to `RealtimeMidiEvent` (note on/off, sustain)
- Uses `RealtimeChordWindowDetector` to build stable snapshots and debounces evaluation (~110ms)
- Calls `RealtimeChordDetectionService.Detect(snapshot, options)` and emits `DetectionChanged`

4) **Prediction engine**
- `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`
- Tracks state:
  - `History` (currently capped to 32 by `.TakeLast(31).Append(newEvent)`)
  - `Hypotheses` (top 5)
  - `Settings`
- For each stable detection:
  - builds weighted chord candidates (top 5)
  - updates hypotheses (continuity, key-compat, voice-leading, stability bonus)
  - generates suggestions via bounded beam search (horizon 3..6, width 4..12)

5) **Canvas rendering**
- Interop: `src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs`
- JS: `src/App.Web/wwwroot/harmonicAssistantCanvas.js`
- Current rendering is simple:
  - fixed history nodes on one y row
  - future paths stacked by row (pathIndex -> y)
  - edges use quadratic curves (always arch up)
  - no interactive canvas controls
  - layout is precomputed in C# with absolute x/y pixels

## Current canvas snapshot DTO
- `src/App.Blazor/Models/HarmonicAssistantCanvasSnapshot.cs`
- Node fields: `Id, Label, X, Y, Weight, Kind, IsCurrent`
- Edge fields: `FromId, ToId, Weight, Label`

## Key current limitations (relevant to the upgrade)
- Graph layout is “row based” and suggestion rows appear below history. This violates the new requirement: **single horizontal flow with branching**.
- Nodes are too small and text size is fixed.
- There is no mapping from harmony -> mood axis -> colors (only fixed colors by node kind).
- Chord detection is based on **active notes** + sustain downweighting, but does not support “floating scoring” for arpeggios + melody noise.
- Route planning uses rule-based transitions and ignores most style pack weights/constraints.
- History retention is hard-coded (32 in engine; 8 shown on canvas).

