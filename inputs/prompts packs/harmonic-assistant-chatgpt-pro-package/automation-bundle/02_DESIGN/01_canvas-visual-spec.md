# Canvas visual spec (Killer Feature)

This spec defines the target “WOW but usable” canvas visualization.

## 1) Non-negotiable UX behavior
1. The graph renders as **one horizontally flowing timeline**:
   - history on the left -> current chord -> future branches to the right
   - no wrapping or stacking suggestion rows under history
2. Nodes (markers) are **visibly larger** than current implementation.
3. A **canvas UI control** adjusts text size (and optionally node scale).
4. Branching:
   - branches emanate from the **current chord node** to the right
   - all motion is horizontally rightward
   - “happier/brighter” suggestions branch **up**
   - “darker” suggestions branch **down**
5. The **main centerline** is at **mid-height** of the canvas.
6. Background “mood tint” shifts:
   - when the user moves toward darker harmony, the centerline region becomes darker
   - the current chord always feels visually “centered” in the mood space
7. History must show vertical travel:
   - when chords change mood, their nodes shift vertically
   - transitions between different y positions use **smooth curved connectors**
8. If content does not fit in width:
   - **auto-zoom out** to keep it readable
   - do not wrap
9. History window is configurable:
   - long sessions remain readable by changing how many history events are rendered

## 2) Coordinate model

### 2.1 World mood coordinate (absolute)
Each chord gets a **WorldYNormalized** in `[0..1]` based on harmony mood classification.
- Example targets (approx):
  - C7 (more vivid): ~0.30 (closer to top)
  - C# minor (darker): ~0.70 (closer to bottom)

This mapping is computed in C# (shared with route planning), not guessed in JS.

### 2.2 View transform (camera centering on the current chord)
We render in “view space” where the **current chord is anchored on the centerline**:

- `centerY = canvasHeight * 0.5`
- `worldCurrent = current.WorldYNormalized`
- `delta = (node.WorldYNormalized - worldCurrent)` in `[-1..+1]`
- `viewY = centerY + delta * (canvasHeight * verticalScale)`

Recommended `verticalScale`:
- 0.38 for desktop
- 0.32 for mobile
Clamp viewY to margins.

This simultaneously satisfies:
- “centerline stays in the middle”
- “background shifts so center becomes the current mood”
- “history shows up/down travel” via relative vertical displacement

### 2.3 Horizontal axis
- X is strictly increasing left->right, no wrap.
- Each event has an `XIndex` (integer):
  - history: `-N+1..0` where 0 is current chord
  - future steps: `1..H` per path (branch)
- Base spacing (before zoom): 140 px per step (tunable).

### 2.4 Zoom-to-fit
Compute required width:
- `required = leftMargin + (maxXIndex - minXIndex) * stepSpacing + rightMargin`
- `zoom = clamp((canvasWidth / required), minZoom, 1.0)`
- apply zoom to spacing, node radius, fonts, line widths
- `minZoom` recommended: 0.35

If `zoom == minZoom` and still not enough:
- reduce history window (use configured history steps)
- optional: collapse repeated chords / apply LOD

## 3) Branch layout (lanes)
Multiple future paths must not overlap.

1. Determine path “direction” based on first step mood delta:
   - if firstDelta < 0 => **upper group**
   - else => **lower group**

2. Within each group, sort by absolute mood delta (more extreme gets a larger offset).

3. Assign a **laneOffset**:
   - laneSpacing base = 42 px (scaled by zoom)
   - upper lanes: `-laneSpacing * laneIndex`
   - lower lanes: `+laneSpacing * laneIndex`

4. Final y for a future node:
- `viewY(node) + laneOffset(path)`

This creates parallel “rails” with gentle curves for mood changes.

## 4) Visual style guidelines (“WOW but usable”)
- Background:
  - a base dark gradient
  - plus a soft **centerline “mood band”** tinted by the current chord color
  - plus subtle bands above/below (lighter/darker) to hint the vertical mood map
- Edges:
  - history edges: gradient from node A color -> node B color
  - future edges: tinted by the path color family (or target node color)
  - use cubic Bezier for smoothness and readability
  - edge thickness = f(probability)
- Nodes:
  - current chord node: glow + thicker outline + gentle pulse
  - future nodes: smaller but readable
  - history nodes: medium size
  - draw label inside circle; for long labels, use ellipsis with tooltip on hover
- Text:
  - crisp text: scale by DPR, avoid blur
  - text scale adjustable via canvas UI

## 5) Canvas UI control: text size
Implement in-canvas control (top-right recommended):
- “A−” and “A+” buttons (hit areas >= 36px)
- optional: display “Text 100%”
- store fontScale in renderer state
- re-render immediately after change

## 6) Interaction (optional but recommended)
- Hover tooltip for nodes:
  - chord name, probability, inferred scale/style hints
- Highlight path when hovering the corresponding suggestion card (requires a link between DOM and canvas):
  - optional; can be added later

## 7) Acceptance criteria
- Suggestions never appear as wrapped rows under history.
- Centerline is always mid-canvas and the current chord is anchored to it.
- Playing darker harmony visibly moves history “downwards” relative to center.
- Auto zoom-to-fit keeps graph in one line without clipping.
- Text size control works on mouse and touch.
