import mermaid from './vendor/mermaid.esm.min.mjs';

const states = new WeakMap();
let renderQueue = Promise.resolve();

export async function render(container, dotNetReference, request) {
  const renderTask = renderQueue.then(() => renderCore(container, dotNetReference, request));
  renderQueue = renderTask.catch(() => undefined);
  return renderTask;
}

async function renderCore(container, dotNetReference, request) {
  destroy(container);

  const source = request?.source ?? '';
  const diagramId = request?.diagramId ?? `cda-mermaid-${crypto.randomUUID()}`;

  if (!container) {
    return failure(diagramId, {
      message: 'The Mermaid container element was not available.',
    });
  }

  if (!source.trim()) {
    container.innerHTML = '';
    return {
      succeeded: true,
      diagramId,
      svgElementId: null,
      nodeCount: 0,
      error: null,
    };
  }

  try {
    mermaid.initialize(buildConfig(request?.options));
    const renderResult = await mermaid.render(`${diagramId}-svg`, source);
    container.innerHTML = renderResult.svg;
    renderResult.bindFunctions?.(container);

    const svg = container.querySelector('svg');
    if (!svg) {
      return failure(diagramId, {
        message: 'Mermaid rendered without an SVG element.',
      });
    }

    svg.setAttribute('data-cda-mermaid-svg', diagramId);
    svg.setAttribute('preserveAspectRatio', 'xMidYMid meet');

    const cleanupCallbacks = [];
    const nodes = attachNodeClicks(svg, diagramId, dotNetReference, cleanupCallbacks);
    const panZoom = request?.panZoomEnabled === false ? null : attachPanZoom(svg, cleanupCallbacks);

    states.set(container, {
      cleanupCallbacks,
      panZoom,
    });

    return {
      succeeded: true,
      diagramId,
      svgElementId: svg.id || null,
      nodeCount: nodes.length,
      error: null,
    };
  } catch (error) {
    container.innerHTML = '';
    return failure(diagramId, normalizeMermaidError(error, source));
  }
}

export function zoom(container, factor) {
  states.get(container)?.panZoom?.zoom(Number(factor) || 1);
}

export function reset(container) {
  states.get(container)?.panZoom?.reset();
}

export function destroy(container) {
  const state = states.get(container);
  if (!state) {
    return;
  }

  for (const cleanup of state.cleanupCallbacks) {
    cleanup();
  }

  states.delete(container);
}

function buildConfig(options) {
  const baseConfig = {
    startOnLoad: false,
    securityLevel: options?.securityLevel ?? 'loose',
    theme: options?.theme ?? 'default',
    flowchart: {
      useMaxWidth: options?.flowchartUseMaxWidth ?? true,
      htmlLabels: options?.htmlLabels ?? true,
    },
    architecture: {
      randomize: options?.architectureRandomize ?? false,
    },
  };

  return deepMerge(baseConfig, options?.additionalConfig ?? {});
}

function deepMerge(target, source) {
  if (!source || typeof source !== 'object') {
    return target;
  }

  for (const [key, value] of Object.entries(source)) {
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      target[key] = deepMerge({ ...(target[key] ?? {}) }, value);
    } else {
      target[key] = value;
    }
  }

  return target;
}

function attachNodeClicks(svg, diagramId, dotNetReference, cleanupCallbacks) {
  const selectors = [
    'g.node',
    '.node',
    'g[id^="flowchart-"]',
    'g[id*="architecture"]',
    'g[class*="node"]',
    'g[class*="service"]',
    'g[class*="junction"]',
  ];
  const nodes = Array.from(new Set([...svg.querySelectorAll(selectors.join(','))]))
    .filter(isNodeCandidate);

  for (const node of nodes) {
    node.style.cursor = 'pointer';
    node.setAttribute('tabindex', node.getAttribute('tabindex') ?? '0');
    node.setAttribute('role', node.getAttribute('role') ?? 'button');
    node.setAttribute('data-cda-mermaid-node', 'true');
    const clickTargets = getNodeClickTargets(node);

    for (const target of clickTargets) {
      target.setAttribute('data-cda-mermaid-node-target', 'true');
    }

    const onClick = (event) => {
      event.stopPropagation();
      const payload = extractNodePayload(node, diagramId);
      dotNetReference?.invokeMethodAsync('HandleNodeClickedAsync', payload)
        .catch((error) => console.warn('Mermaid node click callback failed.', error));
    };

    const onKeyDown = (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        onClick(event);
      }
    };

    for (const target of clickTargets) {
      target.addEventListener('click', onClick);
    }

    node.addEventListener('keydown', onKeyDown);
    cleanupCallbacks.push(() => {
      for (const target of clickTargets) {
        target.removeEventListener('click', onClick);
      }

      node.removeEventListener('keydown', onKeyDown);
    });
  }

  return nodes;
}

function getNodeClickTargets(node) {
  return Array.from(new Set([
    node,
    ...node.querySelectorAll('foreignObject, foreignObject *'),
  ]));
}

function isNodeCandidate(element) {
  const className = element.getAttribute('class') ?? '';
  if (/edge|edgeLabel|label/.test(className) && !/node|service|junction/.test(className)) {
    return false;
  }

  return Boolean(element.id || element.getAttribute('data-id') || element.querySelector('text'));
}

function extractNodePayload(element, diagramId) {
  const elementId = element.id || element.getAttribute('id') || null;
  const dataId = element.getAttribute('data-id') || element.getAttribute('data-node-id');

  return {
    diagramId,
    nodeId: dataId || normalizeNodeId(elementId),
    text: normalizeText(element.textContent),
    svgElementId: elementId,
    tagName: element.tagName,
    className: element.getAttribute('class') || null,
  };
}

function normalizeNodeId(value) {
  if (!value) {
    return null;
  }

  return value
    .replace(/^flowchart-/, '')
    .replace(/^node-/, '')
    .replace(/-\d+$/, '')
    .replace(/^[-_]+|[-_]+$/g, '') || value;
}

function normalizeText(value) {
  const text = (value ?? '').replace(/\s+/g, ' ').trim();
  return text.length > 0 ? text : null;
}

function attachPanZoom(svg, cleanupCallbacks) {
  const original = readViewBox(svg);
  let current = { ...original };
  let activePointerId = null;
  let lastPointer = null;

  applyViewBox();

  const api = {
    zoom: (factor) => zoomAt(factor, null),
    reset: () => {
      current = { ...original };
      applyViewBox();
    },
  };

  const onWheel = (event) => {
    event.preventDefault();
    const factor = event.deltaY < 0 ? 0.88 : 1.12;
    zoomAt(factor, { x: event.clientX, y: event.clientY });
  };

  const onPointerDown = (event) => {
    if (event.button !== 0) {
      return;
    }

    if (isNodeInteractionEvent(event)) {
      return;
    }

    activePointerId = event.pointerId;
    lastPointer = { x: event.clientX, y: event.clientY };
    svg.classList.add('is-panning');
    svg.setPointerCapture?.(event.pointerId);
  };

  const onPointerMove = (event) => {
    if (activePointerId !== event.pointerId || !lastPointer) {
      return;
    }

    const rect = svg.getBoundingClientRect();
    const dx = rect.width > 0 ? ((event.clientX - lastPointer.x) / rect.width) * current.width : 0;
    const dy = rect.height > 0 ? ((event.clientY - lastPointer.y) / rect.height) * current.height : 0;
    current.x -= dx;
    current.y -= dy;
    lastPointer = { x: event.clientX, y: event.clientY };
    applyViewBox();
  };

  const finishPan = (event) => {
    if (activePointerId !== event.pointerId) {
      return;
    }

    activePointerId = null;
    lastPointer = null;
    svg.classList.remove('is-panning');
    svg.releasePointerCapture?.(event.pointerId);
  };

  svg.addEventListener('wheel', onWheel, { passive: false });
  svg.addEventListener('pointerdown', onPointerDown);
  svg.addEventListener('pointermove', onPointerMove);
  svg.addEventListener('pointerup', finishPan);
  svg.addEventListener('pointercancel', finishPan);

  cleanupCallbacks.push(() => {
    svg.removeEventListener('wheel', onWheel);
    svg.removeEventListener('pointerdown', onPointerDown);
    svg.removeEventListener('pointermove', onPointerMove);
    svg.removeEventListener('pointerup', finishPan);
    svg.removeEventListener('pointercancel', finishPan);
  });

  return api;

  function zoomAt(factor, origin) {
    const point = origin ? clientToSvgPoint(origin.x, origin.y) : centerPoint();
    const nextWidth = Math.max(original.width * 0.15, Math.min(original.width * 8, current.width * factor));
    const nextHeight = Math.max(original.height * 0.15, Math.min(original.height * 8, current.height * factor));
    current.x = point.x - ((point.x - current.x) / current.width) * nextWidth;
    current.y = point.y - ((point.y - current.y) / current.height) * nextHeight;
    current.width = nextWidth;
    current.height = nextHeight;
    applyViewBox();
  }

  function centerPoint() {
    return {
      x: current.x + current.width / 2,
      y: current.y + current.height / 2,
    };
  }

  function clientToSvgPoint(clientX, clientY) {
    const rect = svg.getBoundingClientRect();
    return {
      x: current.x + ((clientX - rect.left) / Math.max(rect.width, 1)) * current.width,
      y: current.y + ((clientY - rect.top) / Math.max(rect.height, 1)) * current.height,
    };
  }

  function applyViewBox() {
    svg.setAttribute(
      'viewBox',
      `${current.x} ${current.y} ${current.width} ${current.height}`);
    svg.setAttribute('data-cda-panzoom-viewbox', svg.getAttribute('viewBox'));
  }
}

function isNodeInteractionEvent(event) {
  const path = typeof event.composedPath === 'function' ? event.composedPath() : [];
  return path.some((target) => target instanceof Element
    && (target.hasAttribute('data-cda-mermaid-node') || target.hasAttribute('data-cda-mermaid-node-target')));
}

function readViewBox(svg) {
  const viewBox = svg.getAttribute('viewBox');
  if (viewBox) {
    const values = viewBox.split(/[\s,]+/).map(Number).filter(Number.isFinite);
    if (values.length === 4 && values[2] > 0 && values[3] > 0) {
      return {
        x: values[0],
        y: values[1],
        width: values[2],
        height: values[3],
      };
    }
  }

  const width = parseFloat(svg.getAttribute('width')) || 800;
  const height = parseFloat(svg.getAttribute('height')) || 600;
  return {
    x: 0,
    y: 0,
    width,
    height,
  };
}

function failure(diagramId, error) {
  return {
    succeeded: false,
    diagramId,
    svgElementId: null,
    nodeCount: 0,
    error,
  };
}

function normalizeMermaidError(error, source) {
  const hash = error?.hash ?? {};
  const loc = hash.loc ?? hash.location ?? error?.location ?? {};
  const message = error?.str || error?.message || 'Mermaid could not parse this diagram.';
  const messageLine = parseMessageLine(message);
  const line = messageLine
    ?? normalizeLine(loc.first_line, false)
    ?? normalizeLine(loc.start?.line, false)
    ?? normalizeLine(error?.line, false);
  const column = normalizeColumn(loc.first_column, true) ?? normalizeColumn(loc.start?.character, true) ?? normalizeColumn(error?.column, false);
  const expectedTokens = Array.isArray(hash.expected)
    ? hash.expected.map(cleanExpectedToken).filter(Boolean)
    : [];

  return {
    message,
    line,
    column,
    token: hash.token || error?.token || null,
    text: hash.text || error?.text || null,
    excerpt: buildExcerpt(source, line, column),
    expectedTokens,
    raw: error?.stack || safeJson(error),
  };
}

function parseMessageLine(message) {
  const match = String(message ?? '').match(/\bline\s+(\d+)\b/i);
  if (!match) {
    return null;
  }

  const line = Number(match[1]);
  return Number.isFinite(line) && line > 0 ? line : null;
}

function normalizeLine(value, addOne) {
  if (!Number.isFinite(value)) {
    return null;
  }

  const line = Number(value) + (addOne ? 1 : 0);
  return line > 0 ? line : 1;
}

function normalizeColumn(value, addOne) {
  if (!Number.isFinite(value)) {
    return null;
  }

  const column = Number(value) + (addOne ? 1 : 0);
  return column > 0 ? column : 1;
}

function buildExcerpt(source, line, column) {
  if (!line) {
    return null;
  }

  const lines = source.split(/\r?\n/);
  const text = lines[line - 1];
  if (text === undefined) {
    return null;
  }

  const lineLabel = `${line}: `;
  const caretOffset = lineLabel.length + Math.max((column ?? 1) - 1, 0);
  return `${lineLabel}${text}\n${' '.repeat(caretOffset)}^`;
}

function cleanExpectedToken(value) {
  return String(value ?? '')
    .replace(/^"|"$/g, '')
    .replace(/^'|'$/g, '')
    .trim();
}

function safeJson(value) {
  try {
    return JSON.stringify(value, Object.getOwnPropertyNames(value));
  } catch {
    return String(value);
  }
}
