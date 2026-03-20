# 05 — Canvas-first toolbar / HUD / radial menu UX (Blazor + Canvas)

Goal:
- Make the editor usable without moving the pointer to the top DOM toolbar.
- Keep editor state + document mutations in C#.
- Render controls *inside the canvas* so users can work in the middle of the page.

This doc describes common notation-editor UX patterns **and** a concrete implementation strategy that fits Zyphonote’s current architecture (Blazor handling pointer events on the overlay canvas).

---

## 1) What users expect from notation editors (interaction patterns)

### Common patterns (MuseScore / Sibelius / Finale / Noteflight / Flat.io)
- **Step-time entry**:
  - choose duration (1,2,4,8,16,...) then click pitches.
  - number keys switch duration.
  - dot `.` toggles dotted.
- **Selection & editing**:
  - click selects, drag moves.
  - Delete removes.
  - Arrow keys move selection.
- **Context menus / palettes**:
  - right click brings a menu near selection.
  - palette for articulations, dynamics, slurs/ties.
- **Accidentals**:
  - `#`, `b`, `n` for accidentals.

### A pragmatic UX objective
- 80% of actions should be possible **without leaving the staff area**:
  - via a canvas HUD + (optional) radial menu + keyboard.

---

## 2) UI layers

Zyphonote already uses:
- **Base canvas**: stable notation drawing.
- **Overlay canvas**: selection/ghost/cursor overlays.

Canvas HUD should be drawn in the **overlay canvas** (drawn last).

---

## 3) v2 recommended architecture (C# owns hit testing)

Because Zyphonote already routes pointer events through Blazor (`@onpointerdown` etc.), the most robust approach is:

- **C# computes HUD layout** (rectangles for buttons) every frame (or when state changes).
- **C# appends HUD render commands** to the overlay command list.
- **C# performs hit testing** for pointer events against HUD rectangles *before* normal notation hit-testing.
- JS remains a dumb renderer: it only draws render commands.

This keeps all UX rules and state transitions in C# and avoids JS → .NET roundtrips.

### Data flow
- C# → JS: `RenderCommand[]` already exists. HUD is just more commands (rect/text/path).
- Pointer: Blazor receives pointer events and checks HUD regions first.

### HUD hit region model
A minimal in-component structure is enough:

```csharp
private sealed record HudHitRegion(string Id, LayoutRect Bounds);
private List<HudHitRegion> hudRegions = new();
```

The `Id` maps to an action:
- `tool.select`, `tool.note`, `dur.quarter`, `toggle.dotted`, `accidental.sharp`, etc.

---

## 4) What to put into the canvas HUD

### Minimum viable HUD
Top-left toolbar (inside the canvas):
- Tools: Select / Note / Rest / Eraser
- Durations: Whole / Half / Quarter / Eighth / Sixteenth
- Toggles: Dotted, Add-to-chord
- Accidentals: Sharp / Flat / Natural

### Optional enhancements
- Floating toolbar near selection.
- Radial menu for fast duration selection.
- A small shortcut hint overlay toggled by `?`.

---

## 5) Radial menu (fast entry)

Trigger ideas:
- Right click in staff area.
- Long-press on touch.
- Hold Space (or Q) to show while pressed.

Menu content:
- durations around the circle.
- accidentals as a secondary ring.
- articulations when a note is selected.

Selection:
- drag to a sector and release.

---

## 6) Visual design rules

- Large hit targets: >= 32 px.
- Semi-transparent background behind HUD.
- Active button: high contrast background.
- Disabled: reduced opacity.

Icons:
- Prefer actual SMuFL glyph icons (noteheads, rests, accidentals).
- Fall back to letters if needed.

---

## 7) Accessibility strategy

Canvas-only UI is not accessible by default.
Recommended compromise:
- Keep an optional hidden DOM mirror (not visible) for screen readers.
- BUT Playwright tests for canvas HUD should primarily click the canvas.

---

## 8) Playwright testing approach

Two stable strategies:

1) Click canvas coordinates (preferred for validating canvas HUD works):
   - compute canvas bounding box,
   - click relative coordinates.

2) Expose a debug snapshot:
   - set `window.__notationEditorSettings` from Blazor (JS interop) OR expose via a debug DOM node.
   - assert tool/duration changes.
