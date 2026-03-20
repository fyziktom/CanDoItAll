# PROMPT 06 — In-canvas HUD + Radial quick menu (D1-D3)

Goal: Implement a real pointer-centered radial menu and a canvas HUD. Remove reliance on HTML floating toolbar.

Read:
- `DESIGN/CANVAS_HUD_RADIAL_MENU.md`

Tasks:
1) Add a canvas HUD render layer:
   - Draw tool + duration buttons inside overlay canvas.
   - Add HitMap regions for these buttons.

2) Implement radial menu:
   - Hold Space (or Q toggle) opens at pointer.
   - Highlight slice by pointer angle.
   - Release selects and updates editor settings.

3) Keyboard shortcuts:
   - Extend existing shortcuts to include 32/64 and InsertMode toggle/cycle.
   - Add a help overlay (press ?).

4) Playwright tests:
   - open radial menu, pick Eighth, insert note, verify duration.
   - open HUD button for Rest tool, insert rest.

Update checklist:
- Mark **D1-D3** done.

STOP.
