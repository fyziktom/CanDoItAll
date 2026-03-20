# Technical notes (Canvas + Blazor interop)

## Canvas performance
- Prefer immediate-mode rendering per update (not continuous animation) to keep CPU low.
- Avoid allocating many objects inside render loops:
  - cache gradients if possible (keyed by fromColor+toColor+rounded positions)
  - reuse arrays for nodes/edges layout results
- Use `ctx.save()/ctx.restore()` sparingly; prefer setting styles explicitly.

## Crisp text / avoiding blur
- Always scale the canvas backing store by `devicePixelRatio` and call:
  - `ctx.setTransform(dpr, 0, 0, dpr, 0, 0)`
- Keep coordinates in **logical pixels** after transform.
- Align strokes to half-pixels only if you want sharp 1px lines (optional).

## Hit testing
- Maintain computed node bounding boxes in layout results.
- Convert pointer coordinates into logical pixels (account for CSS scaling).
- Use generous hit radii for touch (>= 22 logical px).

## Avoiding line wrapping (single horizontal flow)
- Never “stack rows” by pathIndex.
- X is always computed from xIndex and zoomed spacing.
- All future paths share the same xIndex axis and differ only by Y + lane offset.

## Resizing
- Prefer `ResizeObserver` on the canvas container.
- On resize: recompute DPR backing size + re-render last snapshot.

## Blazor interop design
- Keep canvas UI interactions in JS (text size control) for immediate feedback.
- Optionally persist text size in Blazor settings later via callback (not required for v1).

## Debuggability
- Expose minimal debug data in the payload meta:
  - scale context label
  - chord root PC
  - probability/confidence
This enables tooltips without extra interop calls.
