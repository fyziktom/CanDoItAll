// NOTE: Reference snippet for Codex. Integrate into notationEditorCanvas.js
// All comments are in English by requirement.

/**
 * Draw a filled "ribbon" based on a cubic bezier curve.
 * This is a VexFlow-inspired approach that looks much closer to engraved slurs/ties than a stroked line.
 *
 * cmd example:
 * {
 *   kind: 'filledBezier',
 *   x, y, c1x, c1y, c2x, c2y, x2, y2,
 *   thickness: 2.0,
 *   fill: '#000',
 *   opacity: 1.0
 * }
 */
function drawFilledBezier(ctx, cmd) {
  const t = Math.max(0.5, Number(cmd.thickness ?? 2.0));
  const fill = cmd.fill || '#000';

  // Compute an approximate offset direction using the endpoints.
  const dx = cmd.x2 - cmd.x;
  const dy = cmd.y2 - cmd.y;
  const len = Math.max(1e-6, Math.hypot(dx, dy));
  const nx = -dy / len; // normal
  const ny = dx / len;

  // Offset by half thickness.
  const ox = nx * (t * 0.5);
  const oy = ny * (t * 0.5);

  ctx.beginPath();

  // Top curve
  ctx.moveTo(cmd.x + ox, cmd.y + oy);
  ctx.bezierCurveTo(cmd.c1x + ox, cmd.c1y + oy, cmd.c2x + ox, cmd.c2y + oy, cmd.x2 + ox, cmd.y2 + oy);

  // Bottom curve back
  ctx.bezierCurveTo(cmd.c2x - ox, cmd.c2y - oy, cmd.c1x - ox, cmd.c1y - oy, cmd.x - ox, cmd.y - oy);

  ctx.closePath();
  ctx.fillStyle = fill;
  ctx.fill();
}
