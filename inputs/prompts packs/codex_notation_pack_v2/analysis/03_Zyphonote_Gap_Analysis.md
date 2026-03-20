# 03 — Zyphonote editor gap analysis (what to implement next)

This document assumes you are working in the `zyphonote-main` architecture:
- `MusicTheory.Core` contains model/layout/render/interaction.
- `MusicNotation.Editor` hosts the canvas and JS render loop.
- `App.Blazor` is the playground shell and toolbar.

## What is already implemented well
### Architecture
- Clean separation: model → layout → render scene → targets.
- Hit testing via `HitMap` (canvas-first UI).
- Command history (undo/redo) with snapshot commands.
- Reflow + auto-rest-fill engine.

### Notation MVP
- Grand staff geometry (treble+bass) and pitch snapping rules.
- Noteheads and rests for durations: whole/half/quarter/eighth/sixteenth.
- Stem direction + simple beaming (horizontal, deterministic).
- Augmentation dots (0..2).
- 3 articulations (staccato/tenuto/accent) rendered geometrically.
- Slurs, hairpins, dynamic text (limited but functional).

## Missing engraving features (high impact)
### 1) Key signatures + accidentals (core)
Current state:
- Model has `NotePitch` spelling with `Accidental`, but no rendering of accidentals.
- `ScoreDocument` has no key signature.
- Layout does not allocate horizontal space for accidentals.
- No per-measure accidental state (reset at barline).

Required additions:
- Add key signature definition + per-measure key context.
- Add accidental rendering (sharp/flat/natural, at least).
- Add accidental placement rules inside chords.
- Add keyboard tools for accidental entry (sticky accidental input).

Key files:
- Model: `src/MusicTheory.Core/NotationEditor/Model/ScoreDocument.cs`
- Layout: `src/MusicTheory.Core/NotationEditor/Layout/ScoreLayoutEngine.cs`
- Rendering: `src/MusicTheory.Core/NotationEditor/Rendering/NotationSceneRenderer.cs`
- Interaction: `src/MusicTheory.Core/NotationEditor/Interaction/NotationInteractionEngine.cs`
- UI: `src/MusicNotation.Editor/Components/Toolbar/*` (or migrate to canvas HUD)

### 2) Time signature changes mid-score (core)
Current state:
- `ScoreDocument.TimeSignature` is a single global value.
- Measure capacity is fixed; insert/shift logic assumes constant capacity.

Required additions:
- Allow `TimeSignature` change at measure boundaries.
- Reflow + auto-rest fill must use the **effective time signature per measure**.
- Engrave the time signature change in the first measure where it applies.

Key files:
- Model: `ScoreDocument.cs`, `ScoreMeasure.cs` (or a change list)
- Commands: Insert/Shift logic uses `score.TimeSignature.Capacity`
- Layout: measure capacity and spacing
- Formats: JSON schema + MusicXML subset import/export

### 3) Ties (core) + better slurs (visual quality)
Current state:
- `NoteEvent` has `TieStart` / `TieStop`, but:
  - ties are not laid out and not rendered,
  - slurs are rendered as a single bezier stroke (thin line), which looks wrong compared to standard engraving.

Required additions:
- Tie layout engine (ties within measure and across barlines).
- Render ties as a **filled shape** (like VexFlow `StaveTie`).
- Improve slurs to use a filled shape with thickness and better curvature.

Key files:
- Model: `ScoreEvent.cs`, `ScoreAnnotations.cs`
- Layout: `ScoreLayoutEngine.cs` (currently has `SlurSegments`)
- Rendering: `NotationSceneRenderer.DrawAnnotations(...)`
- Render target: add filled bezier/path drawing (Canvas & SVG)

### 4) Tuplets + grace notes (expected)
Current state:
- Not implemented.

Required additions:
- Model support for tuplets (group ids or explicit tuplet objects).
- Layout engine: bracket placement and ratio text.
- Editing: tuplet creation tool, splitting durations.
- Rendering: bracket line + number, or bracketless depending on style.

### 5) Clef changes (expected)
Current state:
- Only initial treble/bass clefs.

Required additions:
- Model: clef changes at measure boundaries (or at time positions).
- Layout: allocate space and draw clef change glyphs.
- Interaction: pointer-to-pitch mapping must be clef-aware per segment.

## Missing editor UX features (high impact)
### In-canvas toolbars / HUD / radial menu
Current state:
- Toolbars are DOM-based Blazor components.
- Canvas already supports overlay drawing.

Desired:
- A canvas HUD:
  - top toolbar strip for tools + durations,
  - floating contextual toolbar near selection,
  - radial menu around pointer (fast entry).

Implementation direction:
- Keep state in C# (`NotationEditorState.Settings`).
- Send a compact `HudModel` to JS each frame.
- JS draws UI and performs hit testing; calls back into C# for actions.

## Minimum “next milestone” proposal
Implement in this order:
1) Key signature + accidentals (rendering + editing).
2) Time signature changes (model + layout + commands + import/export).
3) Ties + improved slurs (filled shapes + placement rules).
4) Canvas HUD (tool/duration/radial menu) with keyboard shortcuts aligned.
5) Tuplets (triplets first).
