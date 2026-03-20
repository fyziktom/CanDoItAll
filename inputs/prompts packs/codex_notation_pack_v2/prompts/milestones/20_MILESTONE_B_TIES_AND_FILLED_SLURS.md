You are Codex. Milestone B: implement ties + improve slurs to look like engraved notation.

Primary pain point:
- Zyphonote currently renders slurs as a single stroked bezier curve, which looks “wrong” compared to standard engraving.
- Ties are present in the model (TieStart/TieStop) but not rendered.

Reference algorithms (must follow):
- VexFlow Curve (slur): `vexflow-master/src/curve.ts`
  - A filled ribbon is drawn by **two cubic beziers** (top curve + bottom curve with thickness) forming a closed shape, then filled.
- VexFlow StaveTie (tie): `vexflow-master/src/stavetie.ts`
  - A filled shape is drawn by **two quadratic curves** forming a closed shape, then filled.

Repo locations to modify (expected):
- Rendering interface:
  - `src/MusicTheory.Core/NotationEditor/Rendering/IRenderTarget.cs`
  - `src/MusicTheory.Core/NotationEditor/Rendering/CanvasRenderTarget.cs`
  - `src/MusicTheory.Core/NotationEditor/Rendering/SvgRenderTarget.cs`
  - `src/MusicNotation.Editor/wwwroot/notationEditorCanvas.js`
- Slur drawing is in:
  - `src/MusicTheory.Core/NotationEditor/Rendering/NotationSceneRenderer.cs` (DrawAnnotations)
- Slur layout is in:
  - `src/MusicTheory.Core/NotationEditor/Layout/ScoreLayoutEngine.cs`
- You will add tie layout in:
  - `src/MusicTheory.Core/NotationEditor/Layout/ScoreLayoutEngine.cs`
  - and a new record in `src/MusicTheory.Core/NotationEditor/Layout/LayoutModels.cs`

Required implementation steps:

1) Add a new render primitive for filled paths.
   - Add `DrawPath(string d, string fill, string stroke, double strokeWidth, ...)` to `IRenderTarget`.
   - Implement it in `CanvasRenderTarget` using a new `RenderCommand.Kind = "path"` with a `PathData` property.
   - Implement it in `SvgRenderTarget` using `<path d="..." fill="..." stroke="..." ... />`.
   - Update `notationEditorCanvas.js` to render `kind: 'path'` using `new Path2D(d)` and `fill()` / `stroke()`.
   - Add unit tests that serialize/emit the command correctly (at least 1).

2) Upgrade slur rendering to filled ribbon.
   - Keep existing collision avoidance (layout engine is fine).
   - In `NotationSceneRenderer`, replace `DrawBezier(... stroke ...)` for slurs with a filled path.
   - Path generation MUST follow VexFlow Curve math:
     - `cp_spacing = (last_x - first_x) / 4` (for two control points)
     - first cubic: start -> end with control points offset by `cp_spacing`
     - second cubic: end -> start with control points shifted by `thickness` along Y (direction-aware)
     - Close path and fill.
   - Thickness should scale with staff spacing (e.g., `thickness = staffSpacing * 0.18` with clamp).

3) Implement tie layout + rendering.
   - Use `NoteEvent.TieStart` / `TieStop` to produce drawable tie segments.
   - Basic rules:
     - A tie connects two adjacent notes of the same pitch (pitch class + octave) where first has TieStart and second has TieStop.
     - Support ties across measures.
   - Compute anchors similar to VexFlow:
     - X: near notehead right/left.
     - Y: near notehead (not on stem end).
     - Direction: opposite of stem direction if possible; stable fallback based on staff position.
   - Rendering MUST follow VexFlow StaveTie math:
     - compute `cp_x = (first_x + last_x)/2`
     - `top_cp_y = (first_y + last_y)/2 + cp1*direction`
     - `bottom_cp_y = (first_y + last_y)/2 + cp2*direction`
     - Two quadratic curves close to filled path.

4) Tests (mandatory):
   - Add fixture JSON: `tests/fixtures/score_ties_and_slurs.json`
   - Add Playwright E2E test(s) that:
     - Loads the fixture.
     - Asserts presence of slur paths (cssClass includes `slur` and kind == 'path')
     - Asserts presence of tie paths (cssClass includes `tie` and kind == 'path')
     - Optionally take screenshots and compare.

Acceptance criteria:
- Slurs are filled ribbons (not stroked). No longer use `kind: 'bezier'` for slurs.
- Ties are filled. A tie between adjacent same-pitch notes is rendered.
- Existing features remain unchanged.
- All tests pass.

Deliverables:
- Update `codex/STATUS.md` for all Milestone B items with evidence (test names + file paths).
- Update `codex/NEXT_PROMPT.md` to `prompts/milestones/30_MILESTONE_C_CANVAS_HUD_TOOLBARS.md`.
