# 07_REALTIME_HARMONIC_ASSISTANT_CANVAS.md

## Goal
Implement the “killer feature”: a realtime harmonic assistant that:
- listens to played chords (from the MIDI chord detector),
- keeps chord history,
- estimates compatible scales/keys based on style + “color” + context,
- predicts next chords (2–3 bars ahead minimum) with probabilities,
- shows past + present + future suggestions in a beautiful **canvas** visualization,
- offers large “mood” controls (“brighter/darker”, “verse/chorus/bridge”, “more jazz”, etc).

This must run fully offline in WASM with low latency.

## Existing code to build on (MUST reuse where possible)
Harmony generation infrastructure:
- `src/MusicTheory.Core/Generation/HarmonicDeviceEngine.cs`
- `src/MusicTheory.Core/Generation/HarmonicStylePackLibrary.cs`
- `src/MusicTheory.Core/Generation/*` (cadences, devices, planners)
- `src/App.Blazor/Pages/ProgressionGenerator.razor` (UX controls inspiration + style packs)
- `src/App.Blazor/Services/LeadSheetSessionService.cs`
- Score creation patterns:
  - `src/MusicTheory.Core/Models/ChordProgression.cs`
  - `src/MusicTheory.Core/NotationEditor/Model/ScoreFactory.cs`

Chord detection input:
- Use output of the detector from prompt 06 (do not duplicate MIDI parsing).

## Architecture: modular engine
Implement in `src/MusicTheory.Core/Generation/Realtime/`:

### A) State model
- `HarmonicAssistantState`
  - `List<DetectedChordEvent>` history (timestamp, chord candidates with probabilities)
  - `EstimatedKeyContext` (multiple hypotheses allowed)
  - `AssistantSettings` (style pack, mood sliders, section type, horizon length)

### B) Hypotheses + probabilistic handling
- Keep top-K hypotheses (K=3–5) of:
  - current chord interpretation
  - key center / mode
- Each hypothesis has weight; normalize weights.
- When a new chord arrives, update hypotheses via Bayesian-ish scoring:
  - compatibility with existing key hypothesis
  - voice-leading plausibility from previous chord
  - style pack constraints (from `HarmonicStylePackLibrary`)
- Do not require perfect correctness; require stability and musical plausibility.

### C) Prediction algorithm (beam search)
- For each active hypothesis, generate forward sequences of length N (N=3 chords minimum).
- Use beam search width W (e.g., 8–12).
- Candidate generation sources:
  - diatonic next chords (I, ii, IV, V, vi)
  - functional moves (predominant → dominant → tonic)
  - style-pack devices:
    - secondary dominants
    - tritone substitutions
    - backdoor dominants
    - modal interchange
    - diminished passing
    - turnarounds
    - (optional) modulation bridges (low probability)
- Score each sequence by:
  - functional plausibility
  - voice-leading cost (see below)
  - complexity penalty vs user-selected “skill”
  - adherence to style constraints (max chromatic bars per 8, etc.)

### D) Voice-leading / musicality heuristics (must implement minimal)
Implement a lightweight voice-leading cost:
- Approximate chord tones as pitch classes with an assumed register.
- Penalize large total motion between successive chords.
- Prefer common tones and stepwise motion.
- Keep bass motion reasonable (favor 5th/4th/step).

You do NOT need perfect SATB writing; just enough to make suggestions “feel musical”.

### E) Explanations
Each suggested next chord should include:
- probability / score
- reason labels (e.g., “secondary dominant”, “ii–V”, “borrowed iv”, “tritone sub”)
This is critical for trust.

## UI/UX
Create a new page:
- `src/App.Blazor/Pages/HarmonicAssistant.razor` (route `/harmony`)
Components:
- Left/top control panel with large buttons/sliders:
  - Mood: `Brighter ↔ Darker`
  - Intensity: `Simple ↔ Colorful`
  - Style preset dropdown (maps to `StylePackPreset`)
  - Section buttons: Verse / Chorus / Bridge
  - Actions: “Give me a chorus”, “Give me a verse”, “Reset”, “Lock key”
- Main canvas visualization:
  - shows last ~8 chords played (history)
  - current chord highlighted
  - 2–3 bars ahead suggestions as branches
  - thickness/opacity based on probability
- Right/bottom “Details” panel:
  - selected node details: chord tones, suggested scale, reason labels

### Canvas implementation
- Add JS canvas renderer:
  - `src/App.Web/wwwroot/harmonicAssistantCanvas.js`
- Add Blazor interop service:
  - `src/App.Blazor/Services/HarmonicAssistantCanvasInterop.cs`
- Rendering constraints:
  - avoid rerendering the entire UI on every MIDI event; update canvas via JS with lightweight data DTO.

## Integration
- Harmonic assistant subscribes to the chord detector service (prompt 06).
- When detector produces stable chord candidates:
  - update assistant state
  - recompute predictions with debounce (e.g., 80–120ms) and cancellation

## Edge cases (MUST)
- Ambiguous chords: maintain multiple interpretations; do not “flip flop” rapidly.
- Pedal sustained notes: avoid false chord transitions (use stable chord window).
- No MIDI: assistant can run in “manual chord input” mode as fallback.
- Performance: ensure prediction does not block UI thread (use async + cancellation).

## Acceptance criteria (measurable)
- End-to-end latency (MIDI chord stable → new suggestions visible): **< 250ms** typical.
- Prediction horizon: at least **3 chords** ahead (configurable).
- Stability: if user holds a chord, suggestions do not jitter more than once per second.
- Visuals: canvas renders at 60fps when idle; updates without flicker.
- Suggestions must be musically coherent:
  - at least one suggestion path resolves to a cadence within 4 chords in “simple” mode.

## Tests
Add unit tests in `tests/MusicTheory.Tests`:
- hypothesis update stability for repeated same chord
- style pack changes influence suggested devices
- beam search returns deterministic top results given a seed

## Verification steps
- Manual:
  - Play I–V–vi–IV in C; assistant should strongly suggest common pop continuations.
  - Switch mood to “darker”; suggestions should introduce minor/interchange options more often.
  - Switch style pack to jazz; expect ii–V and secondary dominants to appear.
- Automated:
  - `dotnet test tests/MusicTheory.Tests`
