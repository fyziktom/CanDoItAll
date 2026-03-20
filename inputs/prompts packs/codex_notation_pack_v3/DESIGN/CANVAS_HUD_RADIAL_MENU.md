# DESIGN — In-Canvas HUD + Radial Quick Menu (Pointer-Centered)

Goal: eliminate long pointer travel to the HTML toolbar, especially on large pages / multi-part scores.

## Principles (UX)
- Users expect:
  - step-time note entry (click inserts at snapped position)
  - quick duration change without leaving the cursor area
  - predictable keyboard shortcuts
- A radial menu is best when:
  - it opens at the cursor
  - selection is “flick”/gesture-based
  - it closes automatically after choosing
- Do not hide the top toolbar initially; provide both:
  - top toolbar (discoverable)
  - in-canvas HUD + radial menu (power workflow)

## Interaction design
### 1) In-canvas top HUD (compact ribbon)
Always visible inside the canvas overlay.
Contains:
- Tool: Select / Note / Rest / Eraser
- Duration: Whole / Half / Quarter / 8th / 16th / 32nd / 64th
- Toggles: Dotted, Add-to-chord, InsertMode (Replace/Insert/ Split)
- Accidentals: #, b, natural (with “toggle-off” state)

Hit areas should be large (>= 32px height) for touch.

### 2) Radial quick menu (pointer centered)
Open:
- Hold **Space** (recommended) or press **Q**
- Optional: right-click (context menu replacement)

Layout:
- 8 slices around the cursor (N, NE, E, SE, S, SW, W, NW)
- Center shows current tool/duration summary
- Two concentric rings (optional):
  - inner ring: durations
  - outer ring: tools + modifiers

Selection:
- while menu is open:
  - moving the pointer highlights a slice by angle
  - releasing Space / clicking selects highlighted item
- Escape cancels

Suggested default mapping (inner ring):
- N: Whole
- NE: Half
- E: Quarter
- SE: Eighth
- S: Sixteenth
- SW: Thirty-second
- W: Sixty-fourth
- NW: toggle dotted

Outer ring (tools):
- N: Select
- E: Note
- S: Rest
- W: Eraser
- NE: Slur
- SE: Dot tool
- SW: Chord symbol
- NW: Lyrics (once implemented)

### 3) Keyboard shortcuts (must remain)
Keep current shortcuts and extend:
- tools: S (select), N (note), R (rest), E (eraser)
- durations: 1,2,4,8,6 (16th), 3 (32nd), 5 (64th) OR similar
- dotted: .
- add-to-chord: A
- accidentals: # / b / n
- insert mode: I cycles (Replace/Insert/Split)
- radial: hold Space, or Q toggle
- help overlay: ?

## Implementation plan (fits existing architecture)
### Rendering
- Add a new overlay render pass in C#:
  - HUD buttons as `RenderCommand.Kind='rect'` and `Kind='text'` or 'glyph'
  - radial menu slices as arcs (approx with bezier) or simple polygon wedges
- Add stable css classes:
  - `hud-button tool-note`, `hud-button dur-8`, `radial-slice dur-16`, etc.

### Hit-testing
- Extend existing `HitMap` to include HUD regions.
- Precedence:
  1) if pointer is within HUD/radial hit region, handle as UI click
  2) otherwise handle as score click (existing behavior)

### State + events
- HUD actions map to mutations of `NotationEditorState.Settings`:
  - set tool/duration/dotted/accidental overrides/insert mode
- Radial menu state:
  - store `RadialMenuState` in editor state or as transient in `NotationEditorCanvas.razor`
  - includes `IsOpen`, `AnchorX/Y`, `HighlightedSliceId`

### Performance
- Radial menu highlight updates can be computed locally on pointer-move.
- Only redraw overlay layer when highlight changes.

## Playwright tests (minimum)
- Open editor
- Press/hold Space to open radial menu
- Move pointer to “Eighth”
- Release Space
- Assert toolbar state changed (duration=8th)
- Then click on canvas and assert an inserted note uses the new duration.
