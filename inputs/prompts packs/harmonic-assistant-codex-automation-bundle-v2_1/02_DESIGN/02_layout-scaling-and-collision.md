# Layout Scaling & Collision Avoidance (v2.1)

Problem:
- FontScale increases node radius, but step spacing (`BASE_STEP_PX`) and lane spacing (`BASE_LANE_PX`) do not scale enough, causing overlaps.

## Fix: spacing scales with fontScale
Compute:
- `effectiveScale = zoom * lerp(1.0, fontScale, 0.75)`
- `stepPx = BASE_STEP_PX * effectiveScale`
- `lanePx = BASE_LANE_PX * effectiveScale`

## Additional collision guard
After initial layout:
- Sort nodes by x, then for nodes with close x distance:
  - if `distance(nodeA, nodeB) < (rA + rB + minGap)`:
    - increase local x spacing by applying a *timeline stretch* factor
or
    - reduce zoom slightly (within clamp) and recompute.

A practical strategy:
1) compute zoom-to-fit width
2) compute nodes with scaled spacing
3) detect overlaps in a single pass
4) if overlaps > threshold:
   - reduce zoom by 5% and retry up to N=3
5) if still overlaps:
   - increase `BASE_STEP_PX` for this frame via a `layoutStretch` factor.

## Curves
Keep bezier control points stable under zoom:
- cp1=(from.x + dx*0.45, from.y)
- cp2=(from.x + dx*0.55, to.y)

## Text fitting
- `fitText` remains, but font and radius must stay consistent:
  - compute `radius` based on label width at current fontPx
  - then compute spacing based on radius

Acceptance:
- With fontScale up to max, no node circles overlap at default history window size.
