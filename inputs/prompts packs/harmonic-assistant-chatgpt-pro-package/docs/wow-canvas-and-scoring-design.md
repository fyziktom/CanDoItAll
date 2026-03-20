# Harmonic Assistant WOW Canvas and Scoring Design

## Purpose
This document describes the implemented "WOW canvas + MIDI scoring + planning" upgrade for the Realtime Harmonic Assistant.
It is written for engineers and AI assistants (including ChatGPT Pro) that need to tune or extend the system safely.

## Scope
The upgrade spans:

- Canvas visualization and layout behavior on `/harmony`
- Realtime MIDI note scoring and scored chord detection
- Scale-context inference (including pentatonic/blues)
- Context-aware route planning improvements
- Observability and debug tooling

## End-to-End Flow
1. MIDI input and manual notes enter `RealtimeChordDetectionSessionService`.
2. Events feed both:
   - `RealtimeChordWindowDetector` (stability snapshot)
   - `RealtimeNoteScoreTracker` (floating note/pitch-class scores)
3. `RealtimeChordDetectionService` runs scored recognition and returns:
   - ranked chord candidates
   - inferred scale context candidates
4. `HarmonicAssistantSessionService` passes detection results to `RealtimeHarmonicAssistantEngine`.
5. Engine updates hypotheses and computes ranked suggestion paths.
6. `Harmony.razor` builds semantic canvas snapshot v2 and sends it to JS canvas renderer.
7. `harmonicAssistantCanvas.js` computes responsive layout and renders a single horizontal timeline.

## Canvas Visualization
### Data model
Canvas receives semantic payload (`HarmonicAssistantCanvasSnapshotV2`) with:

- nodes: `id`, `label`, `kind`, `xIndex`, `worldY`, `pathId`, `stepIndex`, `probability`, `color`
- edges: `fromId`, `toId`, `kind`, `probability`
- optional layout and render hints

### Layout rules
- Horizontal timeline is driven only by `xIndex`.
- No row wrapping is allowed.
- Current chord remains centered on a visible centerline.
- Future paths branch rightward with lane offsets above/below center.
- `worldY` drives mood-axis placement (brighter up, darker down).
- Auto zoom-to-fit keeps all timeline content visible.

### Rendering behavior
- DPR-aware canvas resizing (`ResizeObserver` + `setTransform`) keeps text crisp.
- Curved connectors use gradients from source to target node colors.
- Current node has stronger glow/emphasis.
- Background tint shifts from current chord color.
- In-canvas controls `A-` and `A+` adjust text scale with pointer/touch support.

### Screenshot descriptions (text-only)
- Screenshot A: "Current chord centered with explicit centerline; history on left; two future branches on right; upper branch brighter hues, lower branch darker hues."
- Screenshot B: "Same progression after window resize; timeline remains single-row horizontal; no wrapping; labels remain legible."
- Screenshot C: "Hover tooltip over a future node showing probability and inferred scale; text-size panel visible in top-right."

## MIDI Scoring and Detection
### Realtime note scoring
Implemented in `RealtimeNoteScoreTracker` with configurable options:

- `WindowMs`
- `DecayMs`
- `NoteOnBoost`
- `VelocityWeight`
- `HoldBoostPerSecond`
- `SustainHeldMultiplier`
- `MinScoreToKeep`
- `MaxTrackedNotes`
- `BassScoreThreshold`

Semantics:

- Exponential decay is applied lazily per tracked note.
- Held notes accumulate score continuously.
- Sustain-held notes accumulate at reduced multiplier.
- Tracker prunes low/old entries and hard-bounds dictionary size.
- Snapshot exposes ranked pitch-class scores and bass pitch-class guess.

### Scored chord detection
`RealtimeChordDetectionService` now supports:

- `Detect(snapshot, scores, options)` overload
- legacy signature preserved (`scores = null`)

When scores are present:

1. Ranked pitch classes are selected from highest-scored tones.
2. Recognition iterates from `StartPitchClassCount` to `MaxPitchClassCount`.
3. Each attempt computes confidence from candidate gap + coverage.
4. Acceptance uses confidence and matched-pitch-class thresholds.
5. Best accepted attempt is selected (fallback to best attempt if needed).

Low-scored tones remain in score snapshot and are reused for context inference.

## Scale Context Inference
### Added scale modes
`ModeType` and `TonalScaleLibrary` now include:

- `MajorPentatonic`
- `MinorPentatonic`
- `BluesMinor`
- `BluesMajor`

### Score-profile inference API
`TonalScaleLibrary.GetCandidateScalesForPitchClassScores(...)`:

- normalizes score profile
- evaluates roots against all scale definitions
- computes:
  - coverage (in-scale score mass)
  - penalty (out-of-scale mass above threshold)
  - implied-root bias
  - mode-priority bias
- returns deterministic top candidates

Detection results now expose `InferredScaleContext` without removing per-candidate `CompatibleScales`.

## Route Planning Improvements
`RealtimeHarmonicAssistantEngine` now uses:

- style-pack device weights
- style constraints over recent usage counters
- tonal distance (circle of fifths)
- mood-axis continuity (shared with canvas mapping)
- inferred scale context compatibility
- blues-context grammar bias

### Added planning features
- Transition metadata:
  - `DeviceName`
  - `IsChromatic`
  - `IsSecondaryDominant`
  - `IsSubstitution`
- Constraint multipliers:
  - chromatic bars cap
  - secondary dominant cap
  - substitution cap
- Blues grammar transitions:
  - I7 / IV7 / V7 emphasis when blues context is inferred

## Harmony.razor Integration Notes
Key page capabilities now include:

- configurable history steps slider (`8..256`)
- canvas v2 snapshot building with mood color mapping
- chord-name parsing fallback for future-step color projection
- resize observer initialization
- optional debug panel toggle with:
  - top detection candidates
  - pitch-class score profile
  - inferred scale context
  - top suggestion paths and reasons

## Tuning Parameters
### UI settings
- `AssistantSettings.HistorySteps`
- `AssistantSettings.Brightness`
- `AssistantSettings.Colorfulness`
- `AssistantSettings.StylePack`
- `AssistantSettings.HorizonChords`
- `AssistantSettings.BeamWidth`
- `AssistantSettings.LockKey`

### Detection settings
- `MinDetectionConfidence`
- `MinMatchedPitchClasses`
- `StartPitchClassCount`
- `MaxPitchClassCount`
- `MinPitchClassScoreToInclude`
- `SustainDecayMs`

### Note scoring settings
- `WindowMs`
- `DecayMs`
- `HoldBoostPerSecond`
- `SustainHeldMultiplier`
- `MaxTrackedNotes`

### Canvas tuning anchors (JS)
- `BASE_STEP_PX`
- `BASE_LANE_PX`
- `MIN_ZOOM`
- vertical scaling constants
- margin constants

## Performance Notes
- Render loop is event-driven (no continuous animation loop).
- Chord detection loop is bounded by max pitch-class count.
- Beam search remains bounded by clamped width/horizon.
- Note tracker remains bounded by `MaxTrackedNotes`.
- Debug logs are throttled to reduce noise.

## Known Limitations
- Manual QA requiring interactive browser+MIDI hardware must still be executed on a local workstation.
- Canvas currently recomputes layout objects per render; this is acceptable for current graph sizes but can be further pooled if needed.
- Render-time metric is not yet surfaced in UI (optional enhancement).

## Suggested Next Improvements
1. Persist canvas text scale and other visual preferences in user settings.
2. Add lightweight render-time telemetry from JS to debug panel.
3. Add history-aware device usage queue (true rolling window) inside beam nodes.
4. Add integration tests that exercise `/harmony` debug panel with deterministic mock MIDI streams.
5. Add adaptive confidence thresholds based on detected context density.
