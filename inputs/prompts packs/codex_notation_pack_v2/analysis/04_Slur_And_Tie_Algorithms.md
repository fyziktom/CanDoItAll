# 04 — Legato (slur) + tie engraving: VexFlow algorithm mapping and a better implementation plan

This doc has two parts:
1) **How VexFlow draws slurs and ties** (what to copy conceptually).
2) **How Zyphonote currently does it** and what to change to match engraving expectations.

---

## 1) VexFlow slur algorithm (Curve)

Primary file:
- `vexflow/src/curve.ts` (`Curve`)

### Inputs
- `from`, `to`: `Note` objects (start/end notes).
- Options (`CurveOptions`):
  - `position` (nearTop / nearHead),
  - `invert` (flip curve direction),
  - `xShift`, `yShift`,
  - `cps` (control point offsets),
  - `thickness` (filled curve thickness).

### Anchor points (X/Y)
VexFlow finds “tie/slur anchor points” from the notes:
- Start X is typically `from.getTieRightX()`
- End X is typically `to.getTieLeftX()`
- Y is derived from the notehead / stem extents depending on:
  - stem direction,
  - whether curve is above or below,
  - `position` option.

### Control points
VexFlow computes:
- horizontal spacing `cp_spacing = (endX - startX) / 3.5` (clamped)
- control point X:
  - `cpx1 = startX + cp_spacing + xShift`
  - `cpx2 = endX - cp_spacing + xShift`

Control point Y uses:
- an “arc height” derived from `cps` + `yShift` + direction:
  - `cpy1 = startY + (cps[0] * direction) + yShiftTotal`
  - `cpy2 = endY + (cps[1] * direction) + yShiftTotal`

So the curve is a cubic bezier:
- `P0 = (startX, startY)`
- `P1 = (cpx1, cpy1)`
- `P2 = (cpx2, cpy2)`
- `P3 = (endX, endY)`

### Filled shape (this is why it looks good)
Instead of stroking a single bezier line, VexFlow creates a **closed region**:
1) Draw top bezier P0→P3 with P1/P2
2) Draw bottom bezier back to P0 with a small Y offset (`thickness * direction`)
3) Close and fill

Conceptually:

```text
top:    bezier(P0, P1, P2, P3)
bottom: bezier(P3, P2+dy, P1+dy, P0)
fill
```

This yields:
- consistent thickness,
- solid engraving look (not a “hairline”).

---

## 2) VexFlow tie algorithm (StaveTie)

Primary file:
- `vexflow/src/stavetie.ts`

### Anchor points
- Start X = `first_note.getTieRightX()` (or stave tie start for partial ties)
- End X = `last_note.getTieLeftX()`

### Shape
VexFlow uses **two quadratic curves** to form a closed shape:
- quadratic P0→P3 with control point at midX and “top CP Y”
- quadratic back to P0 with “bottom CP Y”
- fill

Control points:
- `cp_x = midpoint(startX, endX)`
- `top_cp_y = midpoint(startY, endY) + cp1 * direction`
- `bottom_cp_y = midpoint(startY, endY) + cp2 * direction`

The parameters `cp1/cp2` are tuned for nice optical curvature.

---

## 3) Zyphonote current implementation (why it looks odd)
### Layout
- `ScoreLayoutEngine.BuildSlurSegments(...)` creates `SlurSegmentLayout` with:
  - endpoints (x1,y1) and (x2,y2)
  - control points:
    - C1X = x1 + (x2-x1)/3
    - C2X = x1 + 2*(x2-x1)/3
    - C1Y/C2Y = y + sign * arcHeight
- There is also collision adjustment:
  - detect intersections with stems / noteheads (sampling),
  - push the curve outward by bumping control points.

### Rendering
- `NotationSceneRenderer.DrawAnnotations(...)` draws:
  - `target.DrawBezier(...)` with a stroke width ≈ 1.35

This is the main issue:
- real-world engraving uses a **filled slur** with thickness and taper,
- a single stroked bezier looks like a generic curve, not a slur.

---

## 4) Recommended fix: keep Zyphonote placement, change rendering to filled shape

### Step 1 — Extend the render target to support filled curves
Add a render command type:
- `DrawFilledBezierRibbon(...)` OR `DrawPath(...)`

Minimal approach (VexFlow-like):
- given cubic bezier control points and a thickness, produce a closed path:
  - top cubic bezier
  - bottom cubic bezier back with an offset

Better approach (engraving-grade):
- compute an offset curve along the bezier normal (sample N points),
- thickness varies with `sin(pi*t)` to taper ends.

#### Suggested API (C#)
```csharp
public interface IRenderTarget
{
    // Existing:
    void DrawBezier(...);

    // New:
    void DrawFilledCubicBezier(
        double x1, double y1,
        double c1x, double c1y,
        double c2x, double c2y,
        double x2, double y2,
        double thicknessPx,
        string fill,
        RenderLayer layer,
        string? cssClass = null,
        float opacity = 1.0f);
}
```

#### JS Canvas implementation idea
- Build a `Path2D`:
  - MoveTo P0
  - bezierCurveTo P1 P2 P3
  - bezierCurveTo (P2 + n*thickness) (P1 + n*thickness) P0 (approx)
  - closePath
- Fill.

> If you want the “normal-based” variant, generate polyline rings and fill with `ctx.fill(path)`.

### Step 2 — Add thickness/taper settings
Recommended defaults:
- Slur thickness: ~2.0 px (scaled with staff spacing)
- Tie thickness: ~2.0 px
- Allow style tuning in `ScoreLayoutOptions`:
  - `SlurThicknessPxFactor`
  - `TieThicknessPxFactor`

### Step 3 — Preserve collision logic
Keep Zyphonote’s intersection avoidance (it’s valuable), but:
- when collision pushes the curve, push the *centerline* curve,
- render ribbon around that centerline.

---

## 5) (Optional but recommended) A nicer curve shape than simple “+arcHeight”
VexFlow’s curve “feels” nicer because:
- cp spacing depends on distance,
- cp vertical offsets are tuned.

Update Zyphonote’s control point Y computation:
- Use distance-based arc height (clamped):
  - `arc = clamp(distX * 0.18, min=10, max=35) * direction`
- Apply endpoint bias:
  - higher end gets slightly higher control point, etc.

This improves asymmetry and avoids “sagging” curves.

---

## 6) How to validate visually (tests)
Add Playwright screenshot tests that render:
1) a short slur between adjacent notes,
2) a long slur spanning a measure,
3) slur above and below,
4) slur with collision (stems/beams) to ensure it pushes out.

Compare screenshots with a pixel threshold.
