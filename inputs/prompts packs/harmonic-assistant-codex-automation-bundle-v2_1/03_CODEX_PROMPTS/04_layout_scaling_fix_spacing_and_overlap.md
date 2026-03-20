# 04 — Layout scaling fix (spacing + overlap)

Goal: prevent overlaps when text size increases.

Tasks (JS):
1) In `computeLayout`, scale:
   - `stepPx` and `lanePx` with fontScale (see /02_DESIGN/02_layout-scaling-and-collision.md).
2) Add overlap detection:
   - detect node-node overlaps for nodes within ~2 steps in xIndex
   - if overlap count > threshold:
     - reduce zoom by 5% and recompute (retry up to 3x)
     - else apply a small stretch factor to stepPx
3) Ensure curves and grid remain stable.

Acceptance:
- At maximum fontScale, default history window produces no overlaps.
- When overlaps are unavoidable (very dense), zoom adjusts automatically.

Self-check:
- Manually set A+ multiple times and observe spacing grows.
