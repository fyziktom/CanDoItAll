# Realtime Harmonic Assistant Deep Dive

## 1) What It Is
The Realtime Harmonic Assistant is a Blazor page at route `/harmony` that:
- receives realtime chord detections (from MIDI or manual chord text),
- maintains harmonic hypotheses over time,
- predicts short future chord paths,
- renders past/current/future harmony on a canvas,
- shows textual explanation signals (probability + reasons).

Primary page implementation:
- `src/App.Blazor/Pages/Harmony.razor:1`

## 2) Runtime Architecture
The feature is composed of five layers:

1. UI layer (Blazor page)
- `src/App.Blazor/Pages/Harmony.razor`

2. Session orchestration
- `src/App.Blazor/Services/HarmonicAssistantSessionService.cs`

3. Input detection session
- `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs`

4. Harmonic prediction engine
- `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`

5. Canvas rendering
- C# interop: `src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs`
- JS renderer: `src/App.Web/wwwroot/harmonicAssistantCanvas.js`

## 3) Dependency Injection and Lifetimes
Registrations:
- `RealtimeChordDetectionService` singleton
- `RealtimeHarmonicAssistantEngine` singleton
- `IMidiService` scoped
- `RealtimeChordDetectionSessionService` scoped
- `HarmonicAssistantSessionService` scoped
- `HarmonicAssistantCanvasInterop` scoped
- Source: `src/App.Blazor/ServiceCollectionExtensions.cs:24-48`

Important implication:
- `RealtimeHarmonicAssistantEngine` is stateful and singleton (`state` field), so its state can persist longer than a single page visit unless reset.
- In Blazor WASM, scoped behaves app-lifetime scoped; this is effectively long-lived per browser tab.

## 4) UI Surface and Control Mapping
Page-level controls:

1. Mood sliders
- Brightness slider bound to `settings.Brightness` and `OnBrightnessChanged`.
- Colorfulness slider bound to `settings.Colorfulness` and `OnColorfulnessChanged`.
- Source: `src/App.Blazor/Pages/Harmony.razor:21-29`, handlers `:199-209`.

2. Style pack selector
- `RadzenDropDown<StylePackPreset>` bound to `settings.StylePack`.
- Source: `src/App.Blazor/Pages/Harmony.razor:31-35`, handler `:211-219`.

3. Section controls and presets
- Verse/Chorus/Bridge section buttons.
- Preset shortcuts `Give me a chorus` and `Give me a verse`.
- Source: `src/App.Blazor/Pages/Harmony.razor:39-45`, handlers `:221-237`.

4. Lock key toggle
- Toggle updates only `settings.LockKey`.
- Source: `src/App.Blazor/Pages/Harmony.razor:53-55`, handler `:239-243`.

5. Manual fallback chord input
- Textbox + apply button sends chord text to session service parser.
- Source: `src/App.Blazor/Pages/Harmony.razor:56-60`, handler `:245-248`.

6. Reset
- Calls session reset then reconfigure with current settings.
- Source: `src/App.Blazor/Pages/Harmony.razor:44`, `:250-255`.

Display regions:
- MIDI status chip (`StatusText`) at `:49-51`, computed at `:122-143`.
- Canvas at `:66`.
- History list at `:73-85`.
- Predicted suggestion cards at `:91-105`.

## 5) End-to-End Data Flow
### 5.1 Initialization Flow
On page init:
1. Subscribe `HarmonicAssistantSession.UpdateChanged`.
2. Start session.
3. Configure assistant with default settings.
4. Pull current update.

Source:
- `src/App.Blazor/Pages/Harmony.razor:145-151`

Session start:
1. Subscribe to chord detection session `DetectionChanged`.
2. Start detection session (which initializes MIDI service, subscribes MIDI messages).

Source:
- `src/App.Blazor/Services/HarmonicAssistantSessionService.cs:24-28`
- `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs:42-69`

### 5.2 MIDI Event Flow
MIDI message pipeline:
1. `MidiService` receives JS callback `OnMidiMessage`.
2. `RealtimeChordDetectionSessionService` maps `ParsedMidiMessage` to `RealtimeMidiEvent`.
3. `RealtimeChordWindowDetector` updates active note window.
4. Debounced evaluation (`DebounceMs`, default 110ms) computes `RealtimeChordDetectionResult`.
5. Detection result event triggers harmonic session queued update (additional 95ms debounce).
6. Harmonic engine updates state and suggestions.
7. UI receives `UpdateChanged`, renders canvas + rerenders text cards.

Sources:
- MIDI parsing callback: `src/MusicNotation.Editor/Services/MidiService.cs:155-214`
- Chord session mapping/evaluation: `src/App.Blazor/Services/RealtimeChordDetectionSessionService.cs:98-140`
- Harmonic session queue: `src/App.Blazor/Services/HarmonicAssistantSessionService.cs:110-141`
- UI update callback: `src/App.Blazor/Pages/Harmony.razor:172-197`

### 5.3 Manual Chord Flow
Manual chord flow bypasses live MIDI detection:
1. Parse chord text.
2. Construct synthetic candidate + synthetic snapshot.
3. Call engine update directly.
4. Emit update event.

Source:
- `src/App.Blazor/Services/HarmonicAssistantSessionService.cs:44-101`

## 6) Settings Object and Effective Behavior
Assistant settings record:
- `StylePack`, `Brightness`, `Colorfulness`, `Section`, `HorizonChords`, `BeamWidth`, `LockKey`, `LockedKeyPitchClass`, `LockedMode`
- Source: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:14-23`

Currently exposed on page:
- Exposed: style, brightness, colorfulness, section, lock toggle
- Not exposed: horizon, beam width, locked key pitch class, locked mode

Behavior impact summary:
- `Brightness`: affects key candidate mode enrichment (Dorian option) and certain transition options.
- `Colorfulness`: controls richer chord symbols and some non-diatonic device activation.
- `Section`: influences mode/borrowing choices, especially bridge behavior.
- `StylePack`: currently used mainly for enabled device group checks in transitions.
- `LockKey`: only effective if `LockedKeyPitchClass` is non-null.

## 7) Harmonic Engine Internal State
Engine state (`HarmonicAssistantState`) contains:
- `History`: rolling detected chord events, currently capped to 32 total by keeping last 31 and appending new.
- `Hypotheses`: weighted chord+key interpretations.
- `Settings`: latest effective settings.
- Source: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:54-112`

Update behavior:
1. Build weighted candidates from top 5 detection candidates.
2. If none, keep current hypotheses and only regenerate suggestions from existing state.
3. Append event to history.
4. Update hypotheses from previous hypotheses + current event candidates.
5. Generate suggestions using beam search per hypothesis.
6. Commit new state and return update object.

## 8) Candidate and Hypothesis Mechanics
Weighted candidates:
- Takes top 5 raw detection candidates.
- Shifts scores by min score and adds epsilon to avoid zeros.
- Normalizes to probabilities.
- Source: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:114-140`

Hypothesis generation:
- New hypotheses from current event candidates + resolved candidate keys.
- Continuation hypotheses from previous hypotheses mixed with current candidates using continuity, key compatibility, voice-leading, and stability bonuses.
- Top 5 kept, then normalized to probability-like weights.
- Source: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:142-198`

Key resolution:
- If lock key enabled with locked key pitch class, force that key context.
- Else infer from chord root + chord symbol and optionally relative key / darker mode.
- Source: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:200-252`

## 9) Suggestion Generation Pipeline
High-level:
1. For each hypothesis, run beam search.
2. Combine all suggestion paths.
3. Keep top 12 by score.
4. Convert score to probability via softmax (`exp(score) / sum(exp(score))`).

Source:
- `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:254-289`

Beam search controls:
- Horizon clamped to 3..6.
- Width clamped to 4..12.
- Initial score starts at `log(max(0.0001, hypothesis.Weight + 0.0001))`.
- Source: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:291-341`

Transition generation:
- Starts with functionally diatonic target degrees based on current degree.
- Adds device candidates based on style+settings (secondary dominant, tritone sub, modal interchange, bright lift, simple cadence anchor).
- Dedupe by `(root pitch class, chord symbol)`.
- Keep top 10 transitions.
- Source: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:343-438`

## 10) Canvas Rendering Model
Page builds a normalized snapshot object:
- Node type: history/current/future
- Edge type: history/prediction
- Caption string
- Source: `src/App.Blazor/Pages/Harmony.razor:268-334`

Layout rules:
- History nodes: fixed y=120, x increments by 120.
- Future nodes: x increments by 170 per step, y by 85 per path.
- Up to 8 history events and 3 suggestion paths visualized.

Rendering:
- C# interop imports JS module and obtains renderer id.
- `render(id, snapshot)` draws background, grid, edges, nodes, caption.
- Source:
  - `src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs:15-80`
  - `src/App.Web/wwwroot/harmonicAssistantCanvas.js:70-167`

## 11) Latency Budget and Timing
Relevant delays:
- Chord detection session debounce: 110ms default (`RealtimeChordWindowOptions.DebounceMs`).
- Harmonic session extra debounce: 95ms.
- Total debounce-only delay: about 205ms before engine compute and render.

Target requirement:
- DoD says harmonic assistant update target is under 250ms from stable chord input.
- Source: `docs/product/definition-of-done.md:7`

Given current debounce stack, the target is reachable but with narrow headroom under load.

## 12) Existing Test Coverage
Core assistant tests:
- `tests/MusicTheory.Tests/RealtimeHarmonicAssistantTests.cs`
- Validates stability of repeated chord behavior.
- Validates style pack change impacts reason vocabulary.
- Validates determinism of top beam path for same input/settings.

Realtime detection tests:
- `tests/MusicTheory.Tests/RealtimeChordDetectionTests.cs`
- Validate stable chord windowing and sustain weighting behavior.

Coverage gaps:
- No Playwright test currently targets `/harmony` page itself.
- Existing e2e coverage includes `/harmony/chords` (MIDI chord detector page), not this assistant page.

## 13) Observed Design Strengths
1. Clean layering between detection, harmonic logic, and rendering.
2. Deterministic search behavior (important for reproducibility and testability).
3. Explicit reason labels exposed in suggestions.
4. Manual fallback path allows non-MIDI usage.
5. Defensive handling around async disposal and late UI callbacks.

## 14) Current Functional Constraints and Risks
1. Lock key toggle likely ineffective in current UI flow.
- `LockKey` toggles, but `LockedKeyPitchClass` is never set in this page.
- Engine lock branch requires both `LockKey` and `LockedKeyPitchClass.HasValue`.
- Sources:
  - `src/App.Blazor/Pages/Harmony.razor:239-243`
  - `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:202-208`

2. No MIDI input selection on Harmony page.
- Page starts detection session but has no control to connect an input port.
- If user has not connected elsewhere, suggestions depend on manual input.

3. Style pack richness is underused in realtime engine.
- Realtime transition generation checks enabled device groups but not device weights/constraints.
- Source:
  - style definitions: `src/MusicTheory.Core/Generation/HarmonicStylePackLibrary.cs`
  - realtime transition logic: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:349-437`

4. Canvas resize path is not wired by page.
- Interop provides `ResizeAsync`, JS provides `resize`, but page never calls it.
- Source:
  - `src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs:65-80`
  - no call site in `src/App.Blazor/Pages/Harmony.razor`

5. Status chip can show online styling when only "supported", not connected.
- CSS state class depends on `MidiService.IsSupported`.
- Source: `src/App.Blazor/Pages/Harmony.razor:49`

6. Suggestion display omits per-step scale suggestions even though engine computes them.
- `SuggestedScale` generated in engine step but not rendered in UI cards/canvas.
- Source:
  - engine: `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs:310-319`
  - page cards: `src/App.Blazor/Pages/Harmony.razor:100-103`

7. UI text encoding issue in form labels.
- Slider labels show `â†”` instead of arrow glyph in current file content.
- Source: `src/App.Blazor/Pages/Harmony.razor:21`, `:26`

## 15) Product Context Alignment
Product backlog calls for:
- At least 3-chord prediction horizon.
- Reason labels per suggestion.
- Source: `docs/product/backlog.md:73-79`

Current implementation status:
- Horizon default is 3 and clamped minimum 3.
- Reason labels are displayed for each suggestion card.
- So core epic acceptance is functionally satisfied, but quality and control depth are expandable.
