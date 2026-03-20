# 01 — VexFlow feature inventory (engraving primitives)

This inventory is focused on VexFlow features that matter for **standard Western music notation** (and common extensions like TAB).

VexFlow is primarily an **engraving / rendering library**, not a full editor. It gives you:
- glyphs, drawing primitives, and layout helpers,
- note / stave / voice abstractions,
- spanners (ties, slurs, hairpins, brackets),
- formatting engines (tick context, formatter).

It does **not** give you:
- a full document model with import/export parity to MusicXML,
- editor UI/interaction, selection, clipboard, undo/redo,
- automatic collision avoidance comparable to a mature engraving engine.

## 1) Rendering backends and contexts
### Renderer / RenderContext
- `Renderer` (Canvas / SVG backends): create a rendering surface and context.
- `RenderContext`: generic drawing API.
- `CanvasContext`, `SVGContext`: backend-specific context implementations.

Key implications:
- You can render the same score to Canvas or SVG (and export SVG).

### Element base class
- `Element`: base type for most notation items.
- Style pipeline: `applyStyle()`, `restoreStyle()`, group open/close in SVG.

## 2) High-level score construction helpers
### Factory
- `Factory`: convenience builder that wires together fonts, renderer, staves, voices, etc.

### System
- `System`: multi-stave system layout helper (e.g., grand staff + brace).

## 3) Staves and stave-level modifiers
### Stave / TabStave
- `Stave`: staff with lines, clefs, signatures, barlines, modifiers.
- `TabStave`: tablature staff.

### StaveModifier
- Base for modifiers that attach to a stave (e.g., clefs, signatures).

### KeySignature / TimeSignature / Clef
- `KeySignature`: draws key signature and manages cancellation logic.
- `TimeSignature`: draws common and numeric time signatures.
- `Clef`: draws clef glyphs; supports mid-stave clef changes via `ClefNote`.

### Barlines and repeats
- `Barline`, `BarlineType`: single/double/end/repeat styles.
- `Repetition` + `Volta`: repeat structures (e.g., 1st/2nd endings).

### StaveConnector
- brace/bracket/line connectors for multi-stave systems.

### StaveText / StaveTempo
- generic stave text (rehearsal marks, instructions),
- tempo markings (including metronome-style).

## 4) Notes, voices, and rhythmic layout
### Notes
- `StaveNote`: pitched notes on a standard stave.
- `TabNote`: tablature notes.
- `Rest` is represented by `StaveNote` with rest glyphs or dedicated classes depending on use.
- `GhostNote`: spacing placeholder (silent, but consumes ticks).

### Voices / tick model
- `Voice` (with `VoiceMode`): rhythm container, tick alignment.
- `TickContext`: aligns items at the same tick position across voices.
- `Formatter`: formats voices and distributes horizontal spacing.

### Fraction
- `Fraction`: rational tick durations.

## 5) Beams, tuplets, and rhythmic groups
- `Beam`: beaming logic (including multiple beam levels).
- `Tuplet`: tuplet brackets/ratios.

## 6) Common note modifiers (symbols attached to notes)
### Accidentals, dots, articulations
- `Accidental`: standard and microtonal accidentals.
- `Dot`: augmentation dots.
- `Articulation`: staccato, accent, tenuto, marcato, etc.

### Ornaments and effects
- `Ornament`: trills, turns, mordents, etc.
- `Tremolo`: tremolo strokes / measured tremolo.
- `Stroke`: arpeggio strokes.

### Fingering and strings (common for guitar/piano pedagogy)
- `FretHandFinger`: LH fingering.
- `StringNumber`: string indicators.

### Bends and vibrato (TAB / guitar)
- `Bend`, `Vibrato`, `VibratoBracket`, `TabSlide`.

### Parenthesis
- `Parenthesis`: parentheses around accidentals or noteheads.

## 7) Spanners / lines / curves
### Slurs
- `Curve`: general slur-like curve between notes (filled shape; cubic bezier).

### Ties
- `StaveTie`: ties between notes (filled shape; quadratic curve; supports multiple chord indices).

### Hairpins
- `StaveHairpin` and `Crescendo`: wedge hairpins.

### Text brackets
- `TextBracket`: brackets with text (e.g., "8va", "rit.", "solo").

### Pedal
- `PedalMarking`: sustain pedal marks.

### StaveLine
- general-purpose line between notes (e.g., gliss, fall, etc. depending on style).

## 8) Chords and annotations / text
- `ChordSymbol`: chord symbols with formatting/positioning helpers.
- `Annotation` / `TextNote` / `TextDynamics`: text attached to notes.

## 9) Music theory helpers
- `Music`: interval/key parsing utilities.
- `KeyManager`: diatonic key spelling + accidental state helper.
- `EasyScore` + `Parser`: string-based DSL for quickly creating notes.

## 10) Layout / geometry utilities
- `BoundingBox` (+ computation helpers): collision regions and measurement.
- `Tables`: glyph metrics tables (notehead widths, stem extents, etc).

---

## What matters for an **editor**
VexFlow gives excellent drawing primitives, but an editor still needs:
- a document model (measures, events, annotations, parts),
- editing operations (insert/replace/shift, split/merge, tuplets),
- hit-testing and selection,
- undo/redo and clipboard,
- serialization (JSON) and interchange (MusicXML/MIDI),
- consistent UX (tool palettes, keyboard entry, playback).

Use VexFlow as:
1) a renderer,
2) a source of proven engraving algorithms (ties/slurs/beams),
3) a glyph / metric reference.
