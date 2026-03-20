// Helper snippet: layout math (v2 payload)
//
// Keep allocations low. Prefer plain objects and reuse arrays where possible.

function clamp(v, min, max) { return Math.max(min, Math.min(max, v)); }

function computeZoom(width, requiredWidth, minZoom) {
  if (requiredWidth <= 1) return 1;
  return clamp(width / requiredWidth, minZoom, 1.0);
}

function cubicConnectorPath(x0, y0, x1, y1) {
  const dx = x1 - x0;
  const c1x = x0 + dx * 0.45;
  const c2x = x0 + dx * 0.55;
  return { c1x, c1y: y0, c2x, c2y: y1 };
}
