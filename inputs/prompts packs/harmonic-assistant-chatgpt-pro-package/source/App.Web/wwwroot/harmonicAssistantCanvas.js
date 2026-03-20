const renderers = new Map();
let rendererCounter = 1;

const BASE_STEP_PX = 140;
const BASE_LANE_PX = 42;
const MIN_ZOOM = 0.35;
const DESKTOP_VERTICAL_SCALE = 0.38;
const MOBILE_VERTICAL_SCALE = 0.32;
const V2_MARGINS = { left: 70, right: 60, top: 50, bottom: 46 };

function getRenderer(id) {
  return renderers.get(id) ?? null;
}

function resizeInternal(renderer) {
  const { canvas, context } = renderer;
  const dpr = window.devicePixelRatio || 1;
  const logicalWidth = Math.max(320, canvas.clientWidth || 960);
  const logicalHeight = Math.max(220, canvas.clientHeight || 380);

  canvas.width = Math.floor(logicalWidth * dpr);
  canvas.height = Math.floor(logicalHeight * dpr);
  context.setTransform(dpr, 0, 0, dpr, 0, 0);
  renderer.width = logicalWidth;
  renderer.height = logicalHeight;
}

function toLogicalPoint(renderer, event) {
  const rect = renderer.canvas.getBoundingClientRect();
  const x = (event.clientX - rect.left) * (renderer.width / Math.max(1, rect.width));
  const y = (event.clientY - rect.top) * (renderer.height / Math.max(1, rect.height));
  return { x, y };
}

function hitRect(point, rect) {
  return point.x >= rect.x &&
    point.x <= rect.x + rect.width &&
    point.y >= rect.y &&
    point.y <= rect.y + rect.height;
}

function findHoveredNode(renderer, point) {
  const layout = renderer.layout;
  if (!layout || !layout.nodes.length) {
    return null;
  }

  for (let i = layout.nodes.length - 1; i >= 0; i--) {
    const node = layout.nodes[i];
    const dx = point.x - node.x;
    const dy = point.y - node.y;
    const hitRadius = Math.max(22, node.radius + 8);
    if (dx * dx + dy * dy <= hitRadius * hitRadius) {
      return node;
    }
  }

  return null;
}

function updateCursor(renderer, point) {
  const controls = renderer.controls;
  const inMinus = controls && hitRect(point, controls.minus);
  const inPlus = controls && hitRect(point, controls.plus);
  if (inMinus || inPlus) {
    renderer.canvas.style.cursor = "pointer";
    return;
  }

  const hoveredNode = findHoveredNode(renderer, point);
  renderer.canvas.style.cursor = hoveredNode ? "pointer" : "default";
}

function handlePointerDown(renderer, event) {
  const point = toLogicalPoint(renderer, event);
  const controls = renderer.controls;
  if (!controls) {
    return;
  }

  let changed = false;
  if (hitRect(point, controls.minus)) {
    renderer.fontScale = clamp(renderer.fontScale - 0.1, renderer.minFontScale, renderer.maxFontScale);
    changed = true;
  } else if (hitRect(point, controls.plus)) {
    renderer.fontScale = clamp(renderer.fontScale + 0.1, renderer.minFontScale, renderer.maxFontScale);
    changed = true;
  }

  if (!changed) {
    const hoveredNode = findHoveredNode(renderer, point);
    renderer.hoverNodeId = hoveredNode ? hoveredNode.id : null;
    drawFrame(renderer, renderer.lastPayload);
    return;
  }

  event.preventDefault();
  if (renderer.canvas.setPointerCapture) {
    try {
      renderer.canvas.setPointerCapture(event.pointerId);
    } catch {
      // Ignore pointer capture errors.
    }
  }

  drawFrame(renderer, renderer.lastPayload);
}

function handlePointerMove(renderer, event) {
  const point = toLogicalPoint(renderer, event);
  updateCursor(renderer, point);

  const hoveredNode = findHoveredNode(renderer, point);
  const hoveredNodeId = hoveredNode ? hoveredNode.id : null;
  if (renderer.hoverNodeId !== hoveredNodeId) {
    renderer.hoverNodeId = hoveredNodeId;
    drawFrame(renderer, renderer.lastPayload);
  }
}

function handlePointerLeave(renderer) {
  renderer.hoverNodeId = null;
  renderer.canvas.style.cursor = "default";
  drawFrame(renderer, renderer.lastPayload);
}

function attachPointerHandlers(renderer) {
  const onPointerDown = event => handlePointerDown(renderer, event);
  const onPointerMove = event => handlePointerMove(renderer, event);
  const onPointerUp = event => updateCursor(renderer, toLogicalPoint(renderer, event));
  const onPointerLeave = () => handlePointerLeave(renderer);

  renderer.canvas.addEventListener("pointerdown", onPointerDown, { passive: false });
  renderer.canvas.addEventListener("pointermove", onPointerMove, { passive: true });
  renderer.canvas.addEventListener("pointerup", onPointerUp, { passive: true });
  renderer.canvas.addEventListener("pointerleave", onPointerLeave, { passive: true });

  renderer.handlers = { onPointerDown, onPointerMove, onPointerUp, onPointerLeave };
}

function detachPointerHandlers(renderer) {
  const handlers = renderer.handlers;
  if (!handlers) {
    return;
  }

  renderer.canvas.removeEventListener("pointerdown", handlers.onPointerDown);
  renderer.canvas.removeEventListener("pointermove", handlers.onPointerMove);
  renderer.canvas.removeEventListener("pointerup", handlers.onPointerUp);
  renderer.canvas.removeEventListener("pointerleave", handlers.onPointerLeave);
  renderer.handlers = null;
}

export function init(canvas) {
  if (!canvas) {
    throw new Error("Canvas element is required.");
  }

  const context = canvas.getContext("2d", { alpha: false });
  if (!context) {
    throw new Error("Canvas 2D context is unavailable.");
  }

  const id = rendererCounter++;
  const renderer = {
    canvas,
    context,
    width: 0,
    height: 0,
    lastPayload: null,
    fontScale: 1.0,
    minFontScale: 0.7,
    maxFontScale: 1.6,
    hoverNodeId: null,
    controls: null,
    layout: null,
    handlers: null,
    observer: null
  };

  canvas.style.touchAction = "none";
  renderers.set(id, renderer);
  attachPointerHandlers(renderer);
  resizeInternal(renderer);
  drawFrame(renderer, renderer.lastPayload);
  return id;
}

export function resize(id) {
  const renderer = getRenderer(id);
  if (!renderer) {
    return;
  }

  resizeInternal(renderer);
  drawFrame(renderer, renderer.lastPayload);
}

export function observeResize(id, element) {
  const renderer = getRenderer(id);
  if (!renderer || typeof ResizeObserver === "undefined") {
    return;
  }

  if (renderer.observer) {
    renderer.observer.disconnect();
    renderer.observer = null;
  }

  const observedElement = element || renderer.canvas;
  const observer = new ResizeObserver(() => {
    resizeInternal(renderer);
    drawFrame(renderer, renderer.lastPayload);
  });
  observer.observe(observedElement);
  renderer.observer = observer;
}

export function render(id, payload) {
  const renderer = getRenderer(id);
  if (!renderer) {
    return;
  }

  renderer.lastPayload = payload ?? null;
  drawFrame(renderer, renderer.lastPayload);
}

export function dispose(id) {
  const renderer = getRenderer(id);
  if (!renderer) {
    return;
  }

  if (renderer.observer) {
    renderer.observer.disconnect();
    renderer.observer = null;
  }

  detachPointerHandlers(renderer);
  renderer.canvas.style.cursor = "default";
  renderers.delete(id);
}

function drawFrame(renderer, payload) {
  const { context: ctx, width, height } = renderer;

  const baseGradient = ctx.createLinearGradient(0, 0, width, height);
  baseGradient.addColorStop(0, "#10151c");
  baseGradient.addColorStop(1, "#172233");
  ctx.fillStyle = baseGradient;
  ctx.fillRect(0, 0, width, height);

  renderer.controls = null;
  renderer.layout = null;

  if (!payload) {
    drawCaption(ctx, width, height, "Play or enter a chord to see harmonic paths.");
    return;
  }

  const nodes = Array.isArray(payload.nodes) ? payload.nodes : [];
  const sampleNode = nodes.length > 0 ? nodes[0] : null;
  const hasV1Shape = sampleNode && Number.isFinite(sampleNode.x) && Number.isFinite(sampleNode.y);
  const hasV2Shape = sampleNode && Number.isFinite(sampleNode.xIndex) && Number.isFinite(sampleNode.worldY);

  if (hasV1Shape || (!sampleNode && !hasV2Shape)) {
    drawLegacyFrame(ctx, width, height, payload);
    return;
  }

  if (!hasV2Shape) {
    drawCaption(ctx, width, height, "Unsupported harmonic canvas payload.");
    return;
  }

  const layout = computeLayout(payload, width, height, renderer);
  renderer.layout = layout;
  drawMoodBackground(ctx, width, height, layout.centerY, layout.currentColor, layout.verticalAmp);
  drawGrid(ctx, width, height, layout.zoom);
  drawCenterline(ctx, width, height, layout.centerY);
  drawEdgesV2(ctx, layout);
  drawNodesV2(ctx, layout, renderer.fontScale);
  drawTooltip(ctx, layout, renderer.hoverNodeId);
  renderer.controls = drawTextControls(ctx, width, renderer.fontScale);

  const caption = payload.caption || `Zoom ${Math.round(layout.zoom * 100)}%`;
  drawCaption(ctx, width, height, caption);
}

function drawLegacyFrame(ctx, width, height, payload) {
  const nodes = Array.isArray(payload.nodes) ? payload.nodes : [];
  const edges = Array.isArray(payload.edges) ? payload.edges : [];

  drawGrid(ctx, width, height, 1);
  drawLegacyEdges(ctx, edges, nodes);
  drawLegacyNodes(ctx, nodes);
  drawCaption(ctx, width, height, payload.caption || "Realtime harmonic assistant");
}

function computeLayout(payload, width, height, renderer) {
  const rawNodes = Array.isArray(payload.nodes) ? payload.nodes : [];
  const rawEdges = Array.isArray(payload.edges) ? payload.edges : [];
  const nodes = rawNodes.map(node => ({ ...node }));
  const minXIndex = nodes.reduce((minValue, node) => Math.min(minValue, Number(node.xIndex) || 0), Number.POSITIVE_INFINITY);
  const maxXIndex = nodes.reduce((maxValue, node) => Math.max(maxValue, Number(node.xIndex) || 0), Number.NEGATIVE_INFINITY);
  const safeMinXIndex = Number.isFinite(minXIndex) ? minXIndex : 0;
  const safeMaxXIndex = Number.isFinite(maxXIndex) ? maxXIndex : 0;
  const spanSteps = Math.max(0, safeMaxXIndex - safeMinXIndex);

  const requiredWidth = V2_MARGINS.left + spanSteps * BASE_STEP_PX + V2_MARGINS.right;
  const zoom = clamp(width / Math.max(1, requiredWidth), MIN_ZOOM, 1.0);
  const stepPx = BASE_STEP_PX * zoom;
  const lanePx = BASE_LANE_PX * zoom;
  const centerY = height * 0.5;
  const verticalScale = width < 720 ? MOBILE_VERTICAL_SCALE : DESKTOP_VERTICAL_SCALE;
  const verticalAmp = height * verticalScale;

  const currentNode = nodes.find(node => !!node.isCurrent || node.kind === "current") ?? null;
  const currentWorldY = currentNode && Number.isFinite(currentNode.worldY) ? currentNode.worldY : 0.5;
  const currentColor = currentNode && isHexColor(currentNode.color) ? currentNode.color : "#5AC8A8";

  const laneOffsets = buildLaneOffsets(nodes, currentWorldY, lanePx);
  const nodeById = new Map();
  const layoutNodes = [];

  for (const node of nodes) {
    const xIndex = Number(node.xIndex) || 0;
    const worldY = clamp(Number(node.worldY) || 0.5, 0.0, 1.0);
    let y = centerY + (worldY - currentWorldY) * verticalAmp;
    if (node.kind === "future" && node.pathId) {
      y += laneOffsets.get(node.pathId) ?? 0;
    }

    y = clamp(y, V2_MARGINS.top, height - V2_MARGINS.bottom);
    const probability = clamp(Number(node.probability) || 0.0, 0.0, 1.0);
    let radius = (14 + probability * 12) * (0.85 + zoom * 0.25);
    if (node.kind === "current" || node.isCurrent) {
      radius += 6;
    }

    const displayNode = {
      ...node,
      x: V2_MARGINS.left + (xIndex - safeMinXIndex) * stepPx,
      y,
      worldY,
      probability,
      radius,
      color: isHexColor(node.color) ? node.color : "#7AA0D6",
      fontPx: Math.max(10, 12 * zoom * renderer.fontScale) + ((node.kind === "current" || node.isCurrent) ? 2 : 0)
    };

    layoutNodes.push(displayNode);
    nodeById.set(displayNode.id, displayNode);
  }

  const layoutEdges = [];
  for (const edge of rawEdges) {
    const fromNode = nodeById.get(edge.fromId);
    const toNode = nodeById.get(edge.toId);
    if (!fromNode || !toNode) {
      continue;
    }

    const edgeProbability = clamp(
      Number.isFinite(edge.probability) ? Number(edge.probability) : Number(edge.weight) || 0.35,
      0.0,
      1.0);
    const strokeWidth = (2.0 + edgeProbability * 4.0) * (0.7 + zoom * 0.3);
    layoutEdges.push({
      kind: edge.kind || edge.label || "prediction",
      probability: edgeProbability,
      strokeWidth,
      from: fromNode,
      to: toNode
    });
  }

  return {
    zoom,
    stepPx,
    lanePx,
    centerY,
    verticalAmp,
    currentWorldY,
    currentColor,
    nodes: layoutNodes,
    edges: layoutEdges
  };
}

function buildLaneOffsets(nodes, currentWorldY, lanePx) {
  const grouped = new Map();
  for (const node of nodes) {
    if (node.kind !== "future" || !node.pathId) {
      continue;
    }

    if (!grouped.has(node.pathId)) {
      grouped.set(node.pathId, []);
    }

    grouped.get(node.pathId).push(node);
  }

  const upper = [];
  const lower = [];
  for (const [pathId, pathNodes] of grouped.entries()) {
    const ordered = [...pathNodes].sort((a, b) => {
      const aStep = Number(a.stepIndex) || Number(a.xIndex) || 0;
      const bStep = Number(b.stepIndex) || Number(b.xIndex) || 0;
      return aStep - bStep;
    });
    const first = ordered[0];
    const delta = (Number(first.worldY) || 0.5) - currentWorldY;
    const entry = { pathId, delta, magnitude: Math.abs(delta) };
    if (delta < 0) {
      upper.push(entry);
    } else {
      lower.push(entry);
    }
  }

  upper.sort((a, b) => b.magnitude - a.magnitude);
  lower.sort((a, b) => b.magnitude - a.magnitude);

  const offsets = new Map();
  upper.forEach((entry, index) => offsets.set(entry.pathId, -lanePx * (index + 1)));
  lower.forEach((entry, index) => offsets.set(entry.pathId, lanePx * (index + 1)));
  return offsets;
}

function drawMoodBackground(ctx, width, height, centerY, currentColor, verticalAmp) {
  const currentColorSoft = toRgba(currentColor, 0.18);
  const currentColorDeep = toRgba(currentColor, 0.10);
  const darkBand = "rgba(6, 10, 15, 0.34)";
  const bandHeight = clamp(verticalAmp * 0.9, 60, 170);

  ctx.fillStyle = currentColorDeep;
  ctx.fillRect(0, centerY - bandHeight * 1.1, width, bandHeight * 2.2);

  const middleGradient = ctx.createLinearGradient(0, centerY - bandHeight, 0, centerY + bandHeight);
  middleGradient.addColorStop(0, toRgba(currentColor, 0.06));
  middleGradient.addColorStop(0.35, currentColorSoft);
  middleGradient.addColorStop(0.5, darkBand);
  middleGradient.addColorStop(0.65, currentColorSoft);
  middleGradient.addColorStop(1, toRgba(currentColor, 0.06));
  ctx.fillStyle = middleGradient;
  ctx.fillRect(0, centerY - bandHeight, width, bandHeight * 2);
}

function drawCenterline(ctx, width, height, centerY) {
  const centerGradient = ctx.createLinearGradient(0, centerY, width, centerY);
  centerGradient.addColorStop(0, "rgba(205, 225, 255, 0.16)");
  centerGradient.addColorStop(0.5, "rgba(240, 250, 255, 0.42)");
  centerGradient.addColorStop(1, "rgba(205, 225, 255, 0.16)");
  ctx.strokeStyle = centerGradient;
  ctx.lineWidth = 1.8;
  ctx.beginPath();
  ctx.moveTo(V2_MARGINS.left * 0.7, centerY);
  ctx.lineTo(width - V2_MARGINS.right * 0.7, centerY);
  ctx.stroke();
}

function drawGrid(ctx, width, height, zoom) {
  const xStep = Math.max(24, Math.round(48 * zoom));
  const yStep = Math.max(24, Math.round(42 * zoom));
  ctx.strokeStyle = "rgba(190, 210, 235, 0.08)";
  ctx.lineWidth = 1;
  for (let x = 0; x < width; x += xStep) {
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, height);
    ctx.stroke();
  }
  for (let y = 0; y < height; y += yStep) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(width, y);
    ctx.stroke();
  }
}

function drawEdgesV2(ctx, layout) {
  for (const edge of layout.edges) {
    const from = edge.from;
    const to = edge.to;
    const gradient = ctx.createLinearGradient(from.x, from.y, to.x, to.y);
    gradient.addColorStop(0, toRgba(from.color, clamp(0.30 + edge.probability * 0.6, 0.20, 0.9)));
    gradient.addColorStop(1, toRgba(to.color, clamp(0.30 + edge.probability * 0.6, 0.20, 0.9)));
    ctx.strokeStyle = gradient;
    ctx.lineWidth = edge.strokeWidth;

    const dx = to.x - from.x;
    const cp1x = from.x + dx * 0.45;
    const cp1y = from.y;
    const cp2x = from.x + dx * 0.55;
    const cp2y = to.y;

    ctx.beginPath();
    ctx.moveTo(from.x, from.y);
    ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, to.x, to.y);
    ctx.stroke();
  }
}

function drawNodesV2(ctx, layout, fontScale) {
  for (const node of layout.nodes) {
    const isCurrent = !!node.isCurrent || node.kind === "current";
    const fillColor = node.color;

    if (isCurrent) {
      ctx.save();
      ctx.shadowColor = toRgba(fillColor, 0.65);
      ctx.shadowBlur = 24;
      ctx.beginPath();
      ctx.fillStyle = toRgba(fillColor, 0.35);
      ctx.arc(node.x, node.y, node.radius + 10, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }

    ctx.beginPath();
    ctx.fillStyle = fillColor;
    ctx.globalAlpha = isCurrent ? 1 : 0.92;
    ctx.arc(node.x, node.y, node.radius, 0, Math.PI * 2);
    ctx.fill();

    ctx.globalAlpha = 1;
    ctx.lineWidth = isCurrent ? 3 : 1.4;
    ctx.strokeStyle = isCurrent ? toRgba("#F9FBFF", 0.85) : toRgba("#F1F6FF", 0.25);
    ctx.stroke();

    const fontPx = Math.max(10, node.fontPx * fontScale);
    ctx.fillStyle = isCurrent ? "#10151c" : "#eef5ff";
    ctx.font = `${isCurrent ? "700" : "600"} ${fontPx}px "Segoe UI", sans-serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    const maxTextWidth = node.radius * 1.78;
    const label = fitText(ctx, node.label ?? "", maxTextWidth);
    ctx.fillText(label, node.x, node.y);
  }
}

function drawLegacyEdges(ctx, edges, nodes) {
  if (!edges.length || !nodes.length) {
    return;
  }

  const nodeById = new Map(nodes.map(node => [node.id, node]));
  for (const edge of edges) {
    const from = nodeById.get(edge.fromId);
    const to = nodeById.get(edge.toId);
    if (!from || !to) {
      continue;
    }

    const alpha = clamp(edge.weight ?? 0.35, 0.12, 0.95);
    ctx.strokeStyle = `rgba(123, 225, 183, ${alpha})`;
    ctx.lineWidth = 1.2 + alpha * 3.0;

    ctx.beginPath();
    ctx.moveTo(from.x, from.y);
    const ctrlX = (from.x + to.x) * 0.5;
    const ctrlY = Math.min(from.y, to.y) - 26;
    ctx.quadraticCurveTo(ctrlX, ctrlY, to.x, to.y);
    ctx.stroke();
  }
}

function drawLegacyNodes(ctx, nodes) {
  for (const node of nodes) {
    const radius = 11 + clamp(node.weight ?? 0.35, 0.1, 0.95) * 14;
    const active = !!node.isCurrent;
    const fill = active ? "#f6bc49" : node.kind === "future" ? "#5ac8a8" : "#8aa5d2";

    ctx.beginPath();
    ctx.fillStyle = fill;
    ctx.globalAlpha = active ? 1 : 0.86;
    ctx.arc(node.x, node.y, radius, 0, Math.PI * 2);
    ctx.fill();

    ctx.globalAlpha = 1;
    ctx.fillStyle = "#eef5ff";
    ctx.font = active ? "bold 14px 'Segoe UI'" : "12px 'Segoe UI'";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(node.label, node.x, node.y);
  }
}

function drawTextControls(ctx, width, fontScale) {
  const panelWidth = 240;
  const panelHeight = 64;
  const panelX = width - panelWidth - 16;
  const panelY = 14;
  const buttonSize = 36;
  const gap = 10;
  const minusRect = {
    x: panelX + 16,
    y: panelY + 14,
    width: buttonSize,
    height: buttonSize
  };
  const plusRect = {
    x: minusRect.x + buttonSize + gap,
    y: minusRect.y,
    width: buttonSize,
    height: buttonSize
  };

  roundRect(ctx, panelX, panelY, panelWidth, panelHeight, 12, "#101b2b", "rgba(122, 160, 214, 0.55)", 1.2, 0.95);
  roundRect(ctx, minusRect.x, minusRect.y, minusRect.width, minusRect.height, 10, "#15243a", "rgba(122, 160, 214, 0.8)", 1.0, 1);
  roundRect(ctx, plusRect.x, plusRect.y, plusRect.width, plusRect.height, 10, "#15243a", "rgba(122, 160, 214, 0.8)", 1.0, 1);

  ctx.fillStyle = "#cfe3ff";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.font = "700 16px 'Segoe UI', sans-serif";
  ctx.fillText("A-", minusRect.x + minusRect.width / 2, minusRect.y + minusRect.height / 2);
  ctx.fillText("A+", plusRect.x + plusRect.width / 2, plusRect.y + plusRect.height / 2);

  ctx.textAlign = "left";
  ctx.font = "600 12px 'Segoe UI', sans-serif";
  ctx.fillText(`Text ${Math.round(fontScale * 100)}%`, plusRect.x + plusRect.width + 12, plusRect.y + plusRect.height / 2);

  return {
    minus: minusRect,
    plus: plusRect
  };
}

function drawTooltip(ctx, layout, hoverNodeId) {
  if (!hoverNodeId) {
    return;
  }

  const node = layout.nodes.find(item => item.id === hoverNodeId);
  if (!node) {
    return;
  }

  const probabilityText = `p=${Math.round(node.probability * 100)}%`;
  const details = node.meta && node.meta.suggestedScale ? `${node.meta.suggestedScale}` : null;
  const lines = details ? [node.label, probabilityText, details] : [node.label, probabilityText];

  ctx.font = "600 12px 'Segoe UI', sans-serif";
  const lineHeight = 16;
  const textWidth = lines.reduce((maxWidth, line) => Math.max(maxWidth, ctx.measureText(line).width), 0);
  const tooltipWidth = textWidth + 16;
  const tooltipHeight = lines.length * lineHeight + 10;

  let x = node.x + node.radius + 10;
  let y = node.y - tooltipHeight - 8;
  if (x + tooltipWidth > layout.nodes.reduce((maxX, item) => Math.max(maxX, item.x), 0) + 100) {
    x = node.x - tooltipWidth - node.radius - 10;
  }
  if (y < 6) {
    y = node.y + node.radius + 8;
  }

  roundRect(ctx, x, y, tooltipWidth, tooltipHeight, 8, "rgba(13, 21, 33, 0.92)", "rgba(205, 225, 255, 0.35)", 1, 1);
  ctx.fillStyle = "#e9f2ff";
  ctx.textAlign = "left";
  ctx.textBaseline = "middle";
  for (let i = 0; i < lines.length; i++) {
    ctx.fillText(lines[i], x + 8, y + 10 + i * lineHeight);
  }
}

function drawCaption(ctx, width, height, text) {
  ctx.fillStyle = "rgba(240, 246, 255, 0.9)";
  ctx.font = "12px 'Segoe UI', sans-serif";
  ctx.textAlign = "left";
  ctx.textBaseline = "middle";
  ctx.fillText(text, 14, height - 14);

  ctx.textAlign = "right";
  ctx.fillStyle = "rgba(190, 210, 235, 0.75)";
  ctx.fillText("Realtime Harmonic Assistant", width - 14, height - 14);
}

function fitText(ctx, text, maxWidth) {
  if (!text) {
    return "";
  }

  if (ctx.measureText(text).width <= maxWidth) {
    return text;
  }

  const ellipsis = "...";
  const ellipsisWidth = ctx.measureText(ellipsis).width;
  if (ellipsisWidth > maxWidth) {
    return "";
  }

  let truncated = text;
  while (truncated.length > 0 && ctx.measureText(truncated).width + ellipsisWidth > maxWidth) {
    truncated = truncated.slice(0, -1);
  }

  return truncated.length === 0 ? "" : `${truncated}${ellipsis}`;
}

function roundRect(ctx, x, y, width, height, radius, fill, stroke, strokeWidth, alpha) {
  const r = Math.min(radius, width / 2, height / 2);
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + width, y, x + width, y + height, r);
  ctx.arcTo(x + width, y + height, x, y + height, r);
  ctx.arcTo(x, y + height, x, y, r);
  ctx.arcTo(x, y, x + width, y, r);
  ctx.closePath();
  ctx.fillStyle = fill;
  ctx.fill();
  if (strokeWidth > 0) {
    ctx.lineWidth = strokeWidth;
    ctx.strokeStyle = stroke;
    ctx.stroke();
  }
  ctx.restore();
}

function toRgba(hex, alpha) {
  if (!isHexColor(hex)) {
    return `rgba(122, 160, 214, ${alpha})`;
  }

  const clean = hex.replace("#", "");
  const r = parseInt(clean.slice(0, 2), 16);
  const g = parseInt(clean.slice(2, 4), 16);
  const b = parseInt(clean.slice(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${clamp(alpha, 0, 1)})`;
}

function isHexColor(value) {
  return typeof value === "string" && /^#[0-9a-fA-F]{6}$/.test(value);
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}
