# AUDIT_CURRENT_STATE.md — Zyphonote vs requirements (as of pack v3)

This audit is written for Codex to avoid re-discovering the same facts.

## What already exists (implemented)
### Core model
- `ScoreDocument` supports:
  - global `TimeSignature` + `TimeSignatureChanges` (per measure)
  - global `KeySignature` + `KeySignatureChanges` (per measure)
  - `AutoRestFillEnabled`
  - `Slurs`, `Dynamics`, `Hairpins`
- `ScoreMeasure` contains:
  - `ChordSymbol` (one per measure)
  - `Events`: `NoteEvent` and `RestEvent`

### Editing / commands
- Insert operations support `InsertMode`:
  - Replace / InsertAndShift / Split
  - Implemented in `ScoreEditingOperations.InsertNote/InsertRest`
- Reflow across measures exists:
  - `ReflowEngine.NormalizeFrom(...)` moves overflow events to later measures
  - can split notes and set `TieStart/TieStop`
- Auto-rest fill exists:
  - `AutoRestFillEngine.RecomputeAll(score)` fills gaps with `RestEvent` Origin=Auto

### Layout / rendering
- Layout:
  - `ScoreLayoutEngine.Compute(...)` builds `ScoreLayout` with `Systems -> Measures -> Events`
  - Current horizontal placement uses **proportional mapping**:
    - `x = ContentLeft + ContentWidth * (Start / MeasureCapacity)`
- Rendering:
  - `NotationSceneRenderer` draws staff lines, noteheads, rests, accidentals, beams, slurs, etc.
  - Debug: JS stores `window.__notationLastBaseCommands` for Playwright inspection.

### UI / interaction
- Top toolbar (HTML) exists: `NotationEditorToolbar.razor`
- Floating toolbar (HTML overlay) exists: `NotationEditorFloatingToolbar.razor`
- Keyboard shortcuts in `NotationEditorShell.razor` (tools, durations, dotted, accidentals, etc.)
- Dot tool exists:
  - Click note with tool=Dot -> `SetNoteDotsCommand`

## Known correctness issues (must fix)
### 1) Duration changes do not ripple / shift following notes
- `SetNoteDotsCommand` calls `ScoreEditingOperations.SetNoteDots(...)`
- That calls `ScoreEditingOperations.ChangeDuration(...)`
- `ChangeDuration(...)` updates duration and calls `ReflowEngine.NormalizeFrom`, but **does not apply InsertMode ripple**, so:
  - in 4/4: two half notes; dot the first (3/4) -> second stays at 1/2 => overlap in the same voice.

### 2) Auto-rest fill is incomplete for small subdivisions
- `AutoRestFillEngine` uses a hard-coded `CandidateDurations` list that stops at 1/32.
- If remaining gap is smaller than the smallest candidate, the fill loop breaks, leaving gaps.
- Beat splitting uses `beatUnit = 1/TimeSignature.Denominator` instead of `MeterGrouping.GetBeatBoundaries(...)`.

### 3) Layout collisions for dense rhythms
- `ScoreLayoutEngine` uses proportional spacing (time ratio) and does not enforce a minimum spacing per symbol.
- Dense rhythms (e.g., 1/32, 1/64) will collide: rests and noteheads can overlap visually.

### 4) Beam/flag level uses total duration (wrong for dotted notes)
- `NotationDurationHelper.BeamLevel(Rational duration)` uses duration thresholds.
- Dotted eighth (3/16) incorrectly becomes “no beam” because 3/16 > 1/8.
- Beam/flag must be based on **BaseDuration**.

### 5) Canvas HUD requirements not met
- Current “quick tools” are HTML floating toolbar, not a radial menu anchored to the pointer.
- Requirement is: HUD should be drawn inside canvas and be operable without traveling to the top.

## Missing larger features (for later prompts)
- Multi-part / voicing (stacked instruments, staff names)
- Lyrics entry + rendering
- Print/page layout (A4/Letter), page borders, overflow warnings
- Proper VexFlow-like formatting / justification at system level

## Primary files to read first
- Editing:
  - `src/MusicTheory.Core/NotationEditor/Commands/ScoreEditingOperations.cs`
  - `src/MusicTheory.Core/NotationEditor/Commands/AutoRestFillEngine.cs`
  - `src/MusicTheory.Core/NotationEditor/Commands/ReflowEngine.cs`
- Layout:
  - `src/MusicTheory.Core/NotationEditor/Layout/ScoreLayoutEngine.cs`
  - `src/MusicTheory.Core/NotationEditor/Layout/BeamingEngine.cs`
- Rendering:
  - `src/MusicTheory.Core/NotationEditor/Rendering/NotationSceneRenderer.cs`
- UI:
  - `src/MusicNotation.Editor/Components/NotationEditorCanvas.razor`
  - `src/MusicNotation.Editor/Components/NotationEditorShell.razor`
