# 05 — Rewrite Canvas renderer for v2 (single-line flow + branches + auto-zoom + text size control)

Goal: implement the killer-feature canvas visualization described in:
- `/02_DESIGN/01_canvas-visual-spec.md`
- `/02_DESIGN/06_coordinate-rules-examples.md`
- SVG diagrams in `/02_DESIGN/svg/*`

This prompt makes the renderer actually draw v2 snapshots with:
- single horizontal flow (no wrapping),
- branching from current node,
- vertical mood axis + lanes,
- background tint shift,
- larger nodes,
- curved connectors,
- auto-zoom-to-fit,
- in-canvas text size control.

## Files to modify
- `src/App.Web/wwwroot/harmonicAssistantCanvas.js`
- (optional) `src/App.Blazor/wwwroot/app.css` for cursor styles
- `src/App.Blazor/Pages/Harmony.razor` (flip `useCanvasV2 = true` after renderer is ready)

## 1) JS renderer state
Extend each renderer object to include:
- `fontScale` (default 1.0)
- `minFontScale` (e.g., 0.70), `maxFontScale` (e.g., 1.60)
- `hoverNodeId` (optional)
- `controls` hit boxes for A-/A+ buttons
- last computed layout (optional, for hit testing)

Attach pointer events in `init(canvas)`:
- `pointerdown`, `pointermove`, `pointerup`, `pointerleave`
- Use `canvas.setPointerCapture(e.pointerId)` on down when interacting
- Convert client coords to canvas logical coords (respecting CSS size + DPR transform)

## 2) Payload detection
In `drawFrame(renderer, payload)`:
- If payload is v1 (has node.x/node.y), keep a fallback draw or show a message.
- If payload is v2 (has node.xIndex/worldY), use the new pipeline.

## 3) Layout computation (no wrapping)
Implement `computeLayout(payload, width, height, rendererState)` returning:
- `nodesById` with computed `x`, `y`, `radius`, `fontPx`, `color`
- `edges` with computed stroke styles
- `zoom`, `stepPx`, `lanePx`, `centerY`, `currentColor`, `currentWorldY`

Rules (must match design):
- Determine current node (kind=="current" or isCurrent==true).
- Compute:
  - `minXIndex`, `maxXIndex`
  - `requiredWidth`
  - `zoom = clamp(width/requiredWidth, 0.35, 1.0)`
  - `stepPx = baseStepPx * zoom`
  - `verticalAmp = height * verticalScale`
- `viewY`:
  - `centerY = height * 0.5`
  - `viewY = centerY + (node.worldY - current.worldY) * verticalAmp`
  - Clamp to top/bottom margins
- Lanes:
  - group future nodes by pathId
  - determine path direction using first step delta (worldY - currentWorldY)
  - assign lane offsets per design (upper negative, lower positive)
  - apply lane offset to future nodes only (history uses raw viewY)
- X mapping:
  - `x = left + (node.xIndex - minXIndex) * stepPx`

## 4) Background (mood tint shift)
Draw:
- base gradient background
- a soft horizontal band centered on centerline with the **current node color**:
  - use `ctx.globalAlpha` to keep subtle
- optionally draw faint bands above/below to hint the mood axis

## 5) Edges (curved connectors)
- History edges: cubic Bezier from history[i] -> history[i+1]
- Prediction edges:
  - current -> future[step1]
  - future step -> next step
- Use gradient stroke from fromColor to toColor:
  - `const grad = ctx.createLinearGradient(x0,y0,x1,y1)`
  - `grad.addColorStop(0, fromColor)`
  - `grad.addColorStop(1, toColor)`
- Stroke width:
  - base 2.0 + probability*4.0, scaled by zoom

## 6) Nodes (bigger + WOW)
- Node radius:
  - base radius = 14px
  - + probability*12px
  - + current bonus (e.g., +6px) + glow ring
- Current node:
  - draw outer glow ring (shadowBlur or layered circles)
  - optional subtle pulse (use time-based animation only if you implement requestAnimationFrame; otherwise keep static)
- Text:
  - fontPx = (12px * zoom * renderer.fontScale) for history/future
  - current font slightly larger/bold
  - draw centered text; if label too long:
    - measureText and apply ellipsis

## 7) In-canvas text size control (MANDATORY)
Draw A-/A+ buttons at top-right (see `/02_DESIGN/svg/02_node-and-label-layout.svg`):
- hit areas >= 36px
- show current scale % text
- on click:
  - decrease/increase `renderer.fontScale` within min/max
  - re-render immediately

## 8) Flip Harmony page to use v2
After the v2 renderer works:
- Set `useCanvasV2 = true` in `Harmony.razor` (or auto-detect JS support).
- Ensure the graph renders with new layout.

## Acceptance criteria
- Suggestions never wrap below history (single-line x flow).
- Branches from current go right with lanes above/below.
- Vertical travel in history is visible via smooth curves.
- Auto zoom-to-fit prevents clipping for large HistorySteps within reason.
- In-canvas text size control works with mouse + touch.
- No severe performance regressions (avoid per-frame allocations; reuse arrays/maps where possible).

## Self-check
- Manual: open `/harmony`, apply manual chord, observe graph.
- Build/test:
  - `dotnet build`
  - `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj`
