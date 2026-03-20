# Coordinate rules & numeric examples

These examples are designed so Codex can implement layout without guessing.

## Example canvas dimensions
- logicalWidth = 960px
- logicalHeight = 380px
- margins:
  - left = 70px
  - right = 60px
  - top = 50px
  - bottom = 46px
- centerY = 190px

## Base spacing
- baseStepPx = 140px (before zoom)
- laneSpacing = 42px (before zoom)
- verticalScale = 0.36 (of canvasHeight)

## Example: history window N=12, horizon H=6
- minXIndex = -(N-1) = -11
- maxXIndex = +H = +6
- spanSteps = maxXIndex - minXIndex = 17

Required width:
- required = left + spanSteps * baseStepPx + right
- required = 70 + 17*140 + 60 = 70 + 2380 + 60 = 2510px

Zoom:
- zoom = clamp(width/required, 0.35, 1.0)
- width/required = 960 / 2510 = 0.382...
- zoom = 0.382 (>= minZoom 0.35)

Scaled:
- stepPx = 140 * 0.382 ≈ 53.5px
- lanePx = 42 * 0.382 ≈ 16.0px

X mapping:
- x(index) = left + (index - minXIndex) * stepPx
- x(-11) = 70 + (0)*53.5 = 70
- x(0) = 70 + (11)*53.5 ≈ 658.5
- x(+6) = 70 + (17)*53.5 ≈ 979.5 (near right edge)

Y mapping:
- verticalAmplitude = canvasHeight * verticalScale = 380 * 0.36 = 136.8px
- currentWorldY = 0.40 (example)
- nodeWorldY = 0.63 (example darker)
- delta = 0.63 - 0.40 = 0.23
- viewY = centerY + delta*2*verticalAmplitude?  (choose one)
  Recommended:
  - treat worldY as [0..1] and map delta in [-0.5..+0.5] by:
    deltaCentered = (nodeWorldY - currentWorldY)   // already [-1..1] in practice
    viewY = centerY + deltaCentered * (verticalAmplitude)
- viewY ≈ 190 + 0.23*136.8 ≈ 221.5px

Lane offsets:
- upper lane #1: -lanePx * 1 = -16px
- lower lane #2: +lanePx * 2 = +32px

Final viewY for a future node in lower lane #2:
- y = viewY + 32px

## Curves
History connectors between two nodes:
- Use cubic Bezier:
  - P0 = (x0, y0)
  - P3 = (x1, y1)
  - dx = (x1 - x0)
  - P1 = (x0 + dx*0.45, y0)
  - P2 = (x0 + dx*0.55, y1)

This keeps curves mostly horizontal while still showing vertical travel.

## Sample semantic snapshot JSON (v2)
```json
{
  "caption": "Top path probability 48%",
  "nodes": [
    { "id":"h-0", "label":"Am7", "kind":"history", "isCurrent":false, "xIndex":-2, "pathId":null, "stepIndex":null, "probability":0.62, "worldY":0.62, "color":"#244d7a" },
    { "id":"h-1", "label":"D7",  "kind":"history", "isCurrent":false, "xIndex":-1, "pathId":null, "stepIndex":null, "probability":0.71, "worldY":0.40, "color":"#ff5a5f" },
    { "id":"h-2", "label":"Gmaj7", "kind":"current", "isCurrent":true, "xIndex":0, "pathId":null, "stepIndex":null, "probability":0.88, "worldY":0.28, "color":"#2fb67e" },

    { "id":"p0-1", "label":"Cmaj7", "kind":"future", "isCurrent":false, "xIndex":1, "pathId":"p0", "stepIndex":1, "probability":0.48, "worldY":0.25, "color":"#2fb67e" }
  ],
  "edges": [
    { "fromId":"h-0", "toId":"h-1", "kind":"history", "probability":0.6 },
    { "fromId":"h-1", "toId":"h-2", "kind":"history", "probability":0.6 },
    { "fromId":"h-2", "toId":"p0-1", "kind":"prediction", "probability":0.48 }
  ]
}
```
