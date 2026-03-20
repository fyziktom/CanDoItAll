# 09 — JS Interop contract (render commands + HUD events)

This describes minimal changes needed to support:
- SVG-path-based glyph rendering,
- filled slurs/ties,
- canvas HUD hit-testing.

---

## 1) Render commands (C# → JS)

Current JS (`notationEditorCanvas.js`) supports:
- `line`
- `bezier` (stroke only)
- `rect`
- `ellipse`
- `text` (font glyphs are drawn via text)

### 1.1 Add `path` command (SVG path fill/stroke)
Proposed shape:

```jsonc
{
  "kind": "path",
  "d": "M ... Z",          // SVG path data in absolute coordinates
  "x": 120.0,             // translation
  "y": 240.0,
  "scale": 0.0125,        // uniform scale
  "fill": "#000",
  "stroke": "none",
  "strokeWidth": 0,
  "opacity": 1.0,
  "rotationDegrees": 0,
  "cssClass": "glyph accidental"
}
```

JS behavior:
- `const p = cache.get(d) ?? new Path2D(d)`
- `ctx.save()`
- `ctx.translate(x, y)`
- `ctx.rotate(...)`
- `ctx.scale(scale, scale)`
- `ctx.fill(p)`
- `ctx.restore()`

Caching:
- Cache `Path2D` by `d` (or by glyph id) to avoid re-parsing.

### 1.2 Add `filledBezier` command (optional convenience)
If you prefer not to compute path strings in C#:

```jsonc
{
  "kind": "filledBezier",
  "x": 10, "y": 20,
  "c1x": 30, "c1y": 10,
  "c2x": 50, "c2y": 10,
  "x2": 70, "y2": 20,
  "thickness": 2.0,
  "fill": "#000"
}
```

JS builds a closed ribbon path and fills it.

---

## 2) HUD events (JS → C#)

### 2.1 Register a .NET callback
Expose a .NET instance:
- `DotNetObjectReference<NotationEditorShell>`

JS keeps:
- `hudHitRegions: Array<{ id, x,y,w,h }>`
- on pointerdown:
  - if hit, `dotNetRef.invokeMethodAsync("OnHudAction", id)`

### 2.2 Suggested `actionId` naming
- `tool.select`
- `tool.note`
- `tool.rest`
- `tool.eraser`
- `duration.whole`
- `duration.half`
- `duration.quarter`
- `duration.eighth`
- `duration.sixteenth`
- `toggle.dotted`
- `accidental.sharp`
- `accidental.flat`
- `accidental.natural`

This keeps actions stable and testable.

---

## 3) Debug hooks for Playwright
To avoid brittle pixel-based checks:
- set `window.__notationHudState = {...}`
- set `window.__notationEditorSettings = {...}`
- update after each action

Playwright can assert on:
- active tool/duration,
- active accidental mode,
- selection count.
