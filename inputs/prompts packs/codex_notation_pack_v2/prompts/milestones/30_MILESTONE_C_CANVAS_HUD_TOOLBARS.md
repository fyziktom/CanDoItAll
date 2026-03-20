You are Codex. Milestone C: move editor controls into the canvas (canvas-first HUD).

Goal:
- Most editing controls must be drawn inside the overlay canvas.
- Pointer interactions must work on the HUD without relying on HTML toolbars.
- C# remains the single source of truth for editor state; canvas HUD is just a rendering + hit-test surface.

Do NOT skip this milestone. The previous attempt skipped it.

Architecture requirement (recommended approach):
- Build HUD render commands in C# and append them to the overlay command list.
- Maintain a list of HUD hit regions in C# for pointer down/up.
- This avoids JS roundtrips for hit testing and keeps the state in C#.

Expected repo integration points:
- `src/MusicNotation.Editor/Components/NotationEditorCanvas.razor`
  - Append HUD commands to `currentFrame.OverlayCommands` before invoking JS draw.
  - Add hit-testing at the start of pointer handlers.
- `src/MusicNotation.Editor/Components/NotationEditorShell.razor`
  - Keep keyboard shortcuts but ensure state reflects HUD changes.
- `src/MusicTheory.Core/NotationEditor/Rendering/RenderCommand.cs`
  - You may need additional fields for HUD shapes (e.g., rounded rect path).

HUD feature scope (must implement at minimum):
1) Top-left in-canvas toolbar with:
   - Tool select: Select / Note / Rest / Eraser
   - Duration: Whole / Half / Quarter / Eighth / Sixteenth
   - Toggles: Dotted, Add-to-chord
   - Accidentals: Sharp / Flat / Natural (toggle)
2) Visual feedback:
   - Active tool/duration/toggles must have a distinct background.
3) Hit-testing:
   - Clicking a HUD button updates `State.Settings` exactly like the HTML toolbar did.
4) Discovery:
   - Show a small hint overlay when user presses `?` listing key shortcuts (from `design/06_Keyboard_Shortcuts.md`).

Optional (nice to have, but do if you can):
- Radial menu invoked by holding Space or right-click.
- Floating mini-toolbar near the pointer when selection exists.

Implementation detail suggestions:
- Render HUD using `RenderCommand.Kind` of `rect`, `text`, and optionally `path` (rounded rect).
- Use a consistent scale based on `LayoutOptions.StaffSpacing` or a constant.
- Store HUD hit regions as a list of `{ id, bounds, action }` in `NotationEditorCanvas`.

Tests (mandatory):
- Add Playwright E2E tests that:
  - Click inside the overlay canvas at the HUD coordinates.
  - Verify that tool/duration state changed (use existing CSS class logic OR expose state via a small `window.__notationEditorStateSnapshot`).
  - Insert a note after selecting Note tool via HUD.

Important:
- You may keep the HTML toolbar for now, but tests for this milestone MUST NOT depend on it.
- Update `codex/STATUS.md` with evidence.

Deliverables:
- Passing tests.
- `codex/NEXT_PROMPT.md` set to `prompts/milestones/40_MILESTONE_D_KEY_TIME_SIGNATURE_EDIT_UI.md`.
