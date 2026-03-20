# 02 — Complete music-notation feature checklist (engraving + editor UX)

Legend:
- **VexFlow**: ✅ supported, ⚠️ partial, ❌ not built-in
- **Zyphonote snapshot** (current repo): ✅ present, ⚠️ partial / MVP, ❌ missing

> This checklist is intentionally exhaustive for *typical notation editors*.  
> If you only target lead sheets or basic piano input, you can prioritize a subset.

---

## A) Staves, systems, and score structure
### Staff types & grouping
- Standard 5-line staff: VexFlow ✅ | Zyphonote ✅
- Grand staff (brace): VexFlow ✅ (`System`, `StaveConnector`) | Zyphonote ✅ (grand staff geometry)
- Multi-staff systems (>2 staves): VexFlow ✅ | Zyphonote ⚠️ (StaffMode exists, but layout is focused on grand staff)
- Bracket (instrument family) + brace combos: VexFlow ✅ | Zyphonote ❌
- Instrument names / abbreviations (left labels): VexFlow ✅ (`StaveText`) | Zyphonote ❌
- Staff size per instrument (cue size / small staff): VexFlow ⚠️ | Zyphonote ❌

### Clefs
- Treble / Bass clef: VexFlow ✅ | Zyphonote ✅
- Alto / Tenor (C clef): VexFlow ✅ (`cClef`) | Zyphonote ❌
- Percussion clef: VexFlow ✅ | Zyphonote ❌
- TAB clef: VexFlow ✅ | Zyphonote ❌
- Mid-system clef changes: VexFlow ✅ (`ClefNote`) | Zyphonote ❌

### Barline styles
- Single barline: VexFlow ✅ | Zyphonote ✅
- Double / end barline: VexFlow ✅ | Zyphonote ❌
- Repeat start/end: VexFlow ✅ | Zyphonote ❌
- Dashed barlines: VexFlow ⚠️ | Zyphonote ❌
- Multi-measure rests: VexFlow ✅ (`MultiMeasureRest`) | Zyphonote ❌

---

## B) Meter, key, and global notation state
### Time signatures
- One global time signature: VexFlow ✅ | Zyphonote ✅
- Time signature changes mid-score: VexFlow ✅ | Zyphonote ❌
- Common/cut time glyphs: VexFlow ✅ | Zyphonote ❌
- Additive meters (e.g., 3+2+3/8): VexFlow ⚠️ | Zyphonote ❌

### Key signatures + accidentals
- Key signature at start: VexFlow ✅ | Zyphonote ❌
- Key signature changes mid-score: VexFlow ✅ | Zyphonote ❌
- Cancellation / courtesy naturals: VexFlow ✅ | Zyphonote ❌
- Note accidentals (sharp/flat/natural): VexFlow ✅ | Zyphonote ❌
- Double accidentals: VexFlow ✅ | Zyphonote ❌
- Microtonal accidentals: VexFlow ✅ | Zyphonote ❌ (not in model)
- Courtesy accidentals in parentheses: VexFlow ✅ | Zyphonote ❌

### Transposition
- Transpose by interval / target key: VexFlow (not an editor concern) ❌ | Zyphonote ❌
- Transposing instruments (written vs sounding): VexFlow ❌ | Zyphonote ❌

---

## C) Notes, rests, durations
### Noteheads
- Whole/half/black: VexFlow ✅ | Zyphonote ✅
- Breve / longa: VexFlow ✅ | Zyphonote ❌
- Cue-size noteheads: VexFlow ⚠️ | Zyphonote ❌
- Alternative noteheads (cross, diamond, triangle): VexFlow ✅ | Zyphonote ❌

### Rests
- Whole/half/quarter/eighth/sixteenth: VexFlow ✅ | Zyphonote ✅
- 32nd+ rests: VexFlow ✅ | Zyphonote ❌
- Multi-measure rest glyphs: VexFlow ✅ | Zyphonote ❌

### Dots
- 1–2 dots: VexFlow ✅ | Zyphonote ✅
- 3 dots: VexFlow ✅ | Zyphonote ❌

### Stems, flags, ledger lines
- Stems up/down: VexFlow ✅ | Zyphonote ✅
- Flags (8th/16th/32nd...): VexFlow ✅ | Zyphonote ⚠️ (up to 16th)
- Ledger lines: VexFlow ✅ | Zyphonote ✅

### Beams
- Beaming groups: VexFlow ✅ | Zyphonote ✅ (horizontal beams)
- Sloped beams: VexFlow ✅ | Zyphonote ❌
- Feathered beams: VexFlow ⚠️ | Zyphonote ❌
- Cross-staff beaming: VexFlow ⚠️ | Zyphonote ❌

### Tuplets
- Tuplets (triplets etc): VexFlow ✅ | Zyphonote ❌

### Grace notes
- Grace notes & groups: VexFlow ✅ | Zyphonote ❌

### Ties and slurs
- Ties: VexFlow ✅ (`StaveTie`) | Zyphonote ❌ (model has flags but no rendering/layout)
- Slurs: VexFlow ✅ (`Curve`) | Zyphonote ⚠️ (exists, but line-only and visually weak)

---

## D) Articulations, ornaments, and performance marks
### Articulations
- Staccato, accent, tenuto: VexFlow ✅ | Zyphonote ✅ (but primitive drawing, not glyph-based)
- Marcato, staccatissimo: VexFlow ✅ | Zyphonote ❌
- Fermata, breath mark, caesura: VexFlow ✅ | Zyphonote ❌
- Up/down bowing, harmonics, snap pizz.: VexFlow ✅ | Zyphonote ❌

### Ornaments
- Trill, mordent, turn: VexFlow ✅ | Zyphonote ❌

### Tremolo
- Single-note tremolo strokes: VexFlow ✅ | Zyphonote ❌

### Glissando / portamento
- Gliss line / slide: VexFlow ⚠️ (`StaveLine`, `TabSlide`) | Zyphonote ❌

---

## E) Dynamics, expressions, and spanners
- Dynamic letters (ppp..fff, sfz): VexFlow ✅ (`TextDynamics`) | Zyphonote ✅ (text)
- Hairpins (cresc/decresc): VexFlow ✅ | Zyphonote ✅
- Expression text (dolce, espress.): VexFlow ✅ | Zyphonote ❌
- Tempo text + metronome mark: VexFlow ✅ | Zyphonote ⚠️ (metadata tempo text exists but not engraved)
- Octave lines (8va/15ma): VexFlow ⚠️ (`TextBracket`) | Zyphonote ❌
- Pedal markings: VexFlow ✅ | Zyphonote ❌

---

## F) Repeats and navigation
- Repeat barlines: VexFlow ✅ | Zyphonote ❌
- Volta brackets (1., 2.): VexFlow ✅ | Zyphonote ❌
- Segno, coda, DS/DC, Fine: VexFlow ✅ (glyphs) | Zyphonote ❌

---

## G) Text layers
- Measure-level chord symbols: VexFlow ✅ | Zyphonote ✅
- Beat-level chord symbols: VexFlow ✅ | Zyphonote ❌
- Lyrics: VexFlow ⚠️ (via text) | Zyphonote ❌
- Fingering: VexFlow ✅ | Zyphonote ❌
- Rehearsal marks: VexFlow ✅ | Zyphonote ❌

---

## H) Editor UX (non-engraving)
### Core editing
- Selection / multiselect: VexFlow ❌ | Zyphonote ✅ (hit map)
- Undo/redo: VexFlow ❌ | Zyphonote ✅ (command history)
- Copy/paste: VexFlow ❌ | Zyphonote ❌
- Drag to move notes: VexFlow ❌ | Zyphonote ⚠️ (some pointer interactions, but limited)
- Step-time keyboard entry: VexFlow ❌ | Zyphonote ✅ (basic shortcuts)
- MIDI record entry: VexFlow ❌ | Zyphonote ⚠️ (recording modules exist, not a full editor UX)

### UI surface
- DOM toolbar: VexFlow ❌ | Zyphonote ✅
- In-canvas toolbar/hud/radial menu: VexFlow ❌ | Zyphonote ❌

### Import/export
- JSON: VexFlow ❌ | Zyphonote ✅
- MusicXML subset: VexFlow ❌ | Zyphonote ⚠️
- MIDI: VexFlow ❌ | Zyphonote ⚠️ (core midi modules exist)
- SVG/PNG export: VexFlow ✅ (SVG backend) | Zyphonote ⚠️ (render target is abstract; add SVG export easily)

---

## Priority tiers (recommended)
**Tier 0 (must-have for a usable editor):**
- Key signature + accidentals + per-measure accidental state
- Time signature changes mid-score
- Ties (and nicer slurs)
- Full articulation glyph set (at least common ones)
- Tuplets (triplet support)
- Better beaming (sloped beams optional, but grouping must be correct)
- In-canvas HUD/tooling (or very fast keyboard UX)

**Tier 1 (expected by most users):**
- Clef changes, tempo marks, repeats/voltas, grace notes
- Lyrics, beat-level chord symbols
- Multi-measure rests

**Tier 2 (advanced engraving):**
- Cross-staff beaming, ottava brackets, pedal, ornaments, tremolo, gliss, etc.
