# 08 — Implementation blueprint (concrete steps, file targets, and acceptance criteria)

This is the “do this in order” plan for Codex.

---

## Phase 0 — Infrastructure (glyphs + render target)
### 0.1 Add a path-based glyph provider (optional but enables full symbol set)
Why:
- SMuFL codepoint mapping is incomplete in the repo.
- VexFlow includes outline glyphs (we provide SVGs + metrics).

Deliverables:
- `SvgGlyphProvider` that loads `assets/svg/vexflow-bravura/glyphs.json`.
- A new render command that can draw SVG paths via Canvas `Path2D`.

Suggested files:
- `src/MusicTheory.Core/NotationEditor/Rendering/IMusicGlyphProvider.cs`
- `src/MusicTheory.Core/NotationEditor/Rendering/MusicGlyph.cs` (extend with `SvgPathD` optional)
- `src/MusicNotation.Editor/wwwroot/notationEditorCanvas.js` (implement `drawPath` command)

Acceptance:
- Render a g clef using SVG path (matches old font glyph position).
- Cache `Path2D` objects by glyph id for speed.

---

## Phase 1 — Key signature, accidentals, time signature changes, transposition
### 1.1 Model changes (schema)
- Add `KeySignature` default to `ScoreDocument`.
- Add `KeySignatureChanges` list.
- Add `TimeSignatureChanges` list.
- Update JSON serialization:
  - bump schema version,
  - add migration defaults.

Files:
- `src/MusicTheory.Core/NotationEditor/Model/ScoreDocument.cs`
- `src/MusicTheory.Core/NotationEditor/Formats/NotationJsonFormatService.cs`
- `src/MusicTheory.Core/NotationEditor/Formats/ScoreDocumentSchema.cs` (if present)

### 1.2 Context resolution helpers
- Add `ScoreContext.GetKeySignatureAt(...)`
- Add `ScoreContext.GetTimeSignatureAt(...)`

### 1.3 Update capacity usage in editing commands
Search for `score.TimeSignature.Capacity` and replace with per-measure capacity.

Likely impacts:
- `InsertNoteCommand`, `InsertRestCommand`, spill/shift logic
- Auto-rest recomputation
- Beaming grouping

### 1.4 Engrave key/time signatures
- Extend layout measure padding:
  - `MeasureLayout.ContentLeft` grows when a signature change must be drawn.
- Add `layout.KeySignatureGlyphs` and `layout.TimeSignatureGlyphs` lists.

### 1.5 Accidentals
- Implement `AccidentalEngine` in layout.
- Add `AccidentalLayout` model (x/y/glyph id).
- Render in `NotationSceneRenderer`.

### 1.6 Editor tools + shortcuts
- Add accidental commands:
  - for selected notes,
  - sticky for insertion.
- Update toolbar model (DOM or canvas HUD):
  - add buttons for #, b, n.

### 1.7 Transposition
- Implement `TransposeScoreCommand` and `TransposeSelectionCommand`.
- Update chord symbols (optional phase 1.7b).

Acceptance:
- Unit tests for key/time context.
- E2E screenshot tests for key signature + accidentals.

---

## Phase 2 — Ties + improved slurs (engraving quality)
### 2.1 Tie layout
- Use existing `NoteEvent.TieStart/TieStop`:
  - pair notes by voice + pitch (or chord index if implemented).
- Create `TieSegmentLayout` similar to `SlurSegmentLayout`.
- Handle:
  - same measure tie,
  - cross-measure tie (partial tie at measure edges).

### 2.2 Tie rendering
- Render as filled shape (quadratic) like VexFlow.

### 2.3 Slur rendering upgrade
- Keep placement, but render as filled ribbon.
- Add thickness/taper controls in `ScoreLayoutOptions`.

Acceptance:
- E2E screenshot tests for ties and slurs.

---

## Phase 3 — Canvas HUD toolbars + radial menu
### 3.1 HUD model contract
- Define `HudModel` and serialize to JS.
- JS draws:
  - top toolbar (tools + durations + dot + accidental)
  - floating toolbar near selection
  - optional radial menu

### 3.2 JS hit testing and action callbacks
- JS captures pointer events for HUD regions.
- On hit, calls into C#:
  - `OnHudAction(string actionId)`

### 3.3 Accessibility mirror (recommended)
- Keep hidden DOM toolbar with `data-testid` hooks.

Acceptance:
- Playwright test can toggle tools by clicking canvas HUD.
- Keyboard shortcuts remain functional.

---

## Phase 4 — Tuplets (triplets first)
### 4.1 Data model
Option A (minimal):
- Store `TupletGroupId` on events.
Option B (explicit tuplet objects):
- `Tuplet { Start, End, Ratio, ShowBracket }`

### 4.2 Layout and engraving
- Bracket placement above/below beam group.
- Number centered.

### 4.3 Editing
- Tool: create tuplet from selection or next N notes.
- Automatically recompute durations inside tuplet.

Acceptance:
- Unit tests for duration math.
- E2E screenshot test for a triplet group.

---

## Phase 5 — Extend symbol set (articulations, ornaments, repeats, etc.)
Implement in small increments:
- additional articulations (marcato, fermata, breath)
- tempo marks
- repeats and voltas
- grace notes
- pedal and ottava brackets

Each addition should include:
- model representation,
- layout placement,
- rendering,
- at least one unit test,
- (optionally) one screenshot test.
