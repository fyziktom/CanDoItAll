# Harmonic Assistant Source Map

This map lists code and docs that directly affect the Realtime Harmonic Assistant.

## A) Core Runtime Files
1. `src/App.Blazor/Pages/Harmony.razor`
- Main page route `/harmony`.
- Owns UI controls, binds settings, handles update events, builds canvas snapshot.

2. `src/App.Blazor/Services/HarmonicAssistantSessionService.cs`
- Orchestrates assistant updates from chord detection and manual input.
- Owns update debouncing (`95ms`) and synchronization gate.

3. `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs`
- Bridges MIDI input stream to chord detection results.
- Owns note-event mapping, chord window detector usage, detection debounce.

4. `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`
- Main algorithm engine: hypotheses, key resolution, transition generation, beam search.

5. `src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs`
- JS interop boundary for assistant canvas render lifecycle.

6. `src/App.Blazor/Models/HarmonicAssistantCanvasSnapshot.cs`
- DTO contracts sent to JS renderer.

7. `src/App.Web/wwwroot/harmonicAssistantCanvas.js`
- Canvas drawing implementation (background, grid, edges, nodes, caption).

## B) Upstream Input and Theory Dependencies
1. `src/MusicNotation.Editor/Services/IMidiService.cs`
- Contract used by detection session and page status.

2. `src/MusicNotation.Editor/Services/MidiService.cs`
- Web MIDI wrapper and event source for note/sustain events.

3. `src/MusicTheory.Core/Recognition/RealtimeChordWindowDetector.cs`
- Converts midi stream into stable window snapshots.

4. `src/MusicTheory.Core/Recognition/RealtimeChordDetectionService.cs`
- Detects ranked chord candidates and compatible scales from snapshot.

5. `src/MusicTheory.Core/Models/KeyContext.cs`
- Key containment, scale degrees, display naming used by assistant hypotheses.

6. `src/MusicTheory.Core/Theory/TonalScaleLibrary.cs`
- Scale inference used in detection and suggestion step scale hints.

7. `src/MusicTheory.Core/Generation/HarmonicStylePack.cs`
- Style pack schema (device groups, weights, constraints).

8. `src/MusicTheory.Core/Generation/HarmonicStylePackLibrary.cs`
- Concrete style packs and device configurations.

9. `src/MusicTheory.Core/Models/Enums.cs`
- `StylePackPreset`, `HarmonicDeviceGroup`, related enums used in settings and transitions.

## C) Composition and Wiring
1. `src/App.Blazor/ServiceCollectionExtensions.cs`
- Registers assistant engine/services and MIDI detection services.

2. `src/App.Web/Program.cs`
- Hosts `AddAppBlazor()` in WASM app.

3. `src/App.Blazor/Layouts/MainLayout.razor`
- Navigation entry to `/harmony`.

## D) UI Styling and Surface Behavior
1. `src/App.Blazor/wwwroot/app.css`
- `hero-card`, `panel-card`, `status-chip`, `harmony-canvas`, summary card styles.

## E) Tests
1. `tests/MusicTheory.Tests/RealtimeHarmonicAssistantTests.cs`
- Unit tests for hypothesis stability, style effects, deterministic beam top path.

2. `tests/MusicTheory.Tests/RealtimeChordDetectionTests.cs`
- Unit tests for chord window stability and detection behavior.

3. `tests/App.Web.PlaywrightTests/ProductFlowsTests.cs`
- Existing e2e includes `/harmony/chords` but not `/harmony`.

## F) Product/Requirement Context
1. `docs/product/backlog.md`
- Epic 6 defines predictive harmonic guidance acceptance criteria.

2. `docs/product/definition-of-done.md`
- Defines `<250ms` harmonic assistant update target and testing expectations.

3. `docs/product/product-vision-and-packaging.md`
- Positions advanced harmonic assistant modes/style packs as premium scope.

## G) Related Existing Theory Docs
1. `docs/style-packs.md`
- General style pack behavior.

2. `docs/harmonic-devices.md`
- Harmonic device concepts used by style packs.

3. `docs/midi-overview.md`
- MIDI architecture and browser support limitations relevant to realtime input.

## H) Excluded But Nearby
Files not directly powering `/harmony` but often confused with it:
- `src/App.Blazor/Pages/MidiChordDetector.razor` (`/harmony/chords`)
- Progression generator and long-form harmonic device engines

These are adjacent features and can provide implementation ideas but are not the active realtime assistant page.
