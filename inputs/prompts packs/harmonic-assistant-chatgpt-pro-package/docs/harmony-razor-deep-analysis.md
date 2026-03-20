# Harmony.razor Deep Analysis

File: `src/App.Blazor/Pages/Harmony.razor`

## Role
`Harmony.razor` is the orchestration UI for realtime harmonic assistance. It is responsible for:

- user controls (mood, style, history, section, key lock, manual chord)
- rendering the responsive canvas graph through JS interop
- presenting realtime history and suggestion paths
- exposing a debug panel for tuning

It does not perform harmonic prediction itself; it coordinates session services and converts domain output into render payloads.

## Dependencies
Injected services:

- `IMidiService` for capability/status display
- `RealtimeChordDetectionSessionService` for debug data (detection + score snapshot)
- `HarmonicAssistantSessionService` for assistant updates
- `HarmonicAssistantCanvasInterop` for JS module lifecycle and render calls

## Lifecycle
### `OnInitializedAsync`
- subscribes to `HarmonicAssistantSession.UpdateChanged`
- starts session service
- applies initial settings
- captures current assistant update + detection/score snapshot

### `OnAfterRenderAsync(firstRender)`
- initializes canvas interop once
- registers resize observation
- marks canvas ready and triggers first render

### `DisposeAsync`
- unsubscribes update event
- disposes canvas interop safely (exception tolerant)

## Update Loop
Event handler: `HandleAssistantUpdateChanged(...)`

- updates `update` (assistant state + suggestions)
- refreshes `latestDetection` and `latestScoreSnapshot`
- schedules UI render via `InvokeAsync`
- re-renders canvas (v2 path active)

This keeps UI cards, debug panel, and canvas in sync with one update stream.

## Settings and Controls
The page maps UI controls directly to `AssistantSettings`:

- `Brightness` slider
- `Colorfulness` slider
- `StylePack` dropdown
- `HistorySteps` slider (`8..256`)
- section quick-select buttons (`Verse`, `Chorus`, `Bridge`)
- helper presets (`Give me a chorus`, `Give me a verse`)
- `LockKey` toggle
- manual chord text + apply
- reset
- debug panel toggle

Changes call `HarmonicAssistantSession.ConfigureAsync(settings)`.

## Canvas Rendering Path
### Current path
`useCanvasV2 = true`

### Render flow
`RenderCanvasAsync()`:

1. exits if canvas not ready or page disposed
2. builds semantic snapshot via `BuildCanvasSnapshotV2(update)`
3. calls `CanvasInterop.RenderV2Async(snapshot)`

### Snapshot construction
`BuildCanvasSnapshotV2(...)` converts assistant output into semantic nodes/edges:

- history nodes:
  - `xIndex` negative to zero (relative to current)
  - `kind = history/current`
  - `worldY` and `color` from `HarmonyVisualMapping`
- future nodes:
  - `xIndex = step + 1`
  - `pathId` and `stepIndex` used by JS laneing
  - metadata includes `stepScore` and `suggestedScale`
- edges:
  - history edges and prediction edges with probabilities
- render hints:
  - current worldY is passed to guide JS centering

This design keeps C# semantic and leaves responsive layout to JS.

## Chord Parsing Utility Path
Future-step labels are strings, so the page includes parsing helpers:

- `TryParseChordFromDisplayName(...)`
- `TryNormalizeChordSymbol(...)`

These ensure color/mood mapping can still run for predicted chord names even if not directly serialized as chord objects.

## Debug Panel Behavior
When enabled:

- shows top detection candidates with scores
- shows pitch-class score profile (top 8)
- shows inferred scale context candidates
- shows top suggestion paths with reasons

This panel is intended for live tuning of scoring, context inference, and planning behavior.

## Failure Handling and Stability
- interop init/render paths are wrapped in try/catch to avoid route-breaking errors
- late updates during disposal are ignored
- default snapshots and fallback mappings prevent null-reference faults

## Key Extension Points
1. Add callbacks to persist canvas text size from JS to settings.
2. Surface render timing and detection confidence directly in panel.
3. Add optional lane-collision diagnostics in panel metadata.
4. Add quick scenario presets (ii-V-I, blues, modal) for manual tuning workflows.
