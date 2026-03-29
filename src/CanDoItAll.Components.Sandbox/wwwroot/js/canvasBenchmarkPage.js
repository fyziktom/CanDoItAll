const hostWidth = 1440;
const hostHeight = 920;
const nodeWidth = 168;
const nodeHeight = 96;
const framePadding = 28;

function round(value) {
    return Math.round(value * 100) / 100;
}

function clone(value) {
    return JSON.parse(JSON.stringify(value));
}

function waitForPaint() {
    return new Promise(resolve => {
        requestAnimationFrame(() => {
            requestAnimationFrame(resolve);
        });
    });
}

function getNodePosition(surface, node) {
    const manualPositions = surface?.uiState?.manualPositions || {};
    const manual = manualPositions[node.id];

    return {
        x: typeof manual?.x === "number" ? manual.x : (node.x || 0),
        y: typeof manual?.y === "number" ? manual.y : (node.y || 0)
    };
}

function getSceneMetrics(surface) {
    const nodes = Array.isArray(surface?.surface?.nodes) ? surface.surface.nodes : Array.isArray(surface?.nodes) ? surface.nodes : [];
    const positions = nodes.map(node => ({ node, position: getNodePosition(surface.surface || surface, node) }));
    const minX = positions.length === 0 ? 0 : Math.min(...positions.map(entry => entry.position.x));
    const minY = positions.length === 0 ? 0 : Math.min(...positions.map(entry => entry.position.y));
    const maxX = positions.length === 0 ? hostWidth : Math.max(...positions.map(entry => entry.position.x + nodeWidth));
    const maxY = positions.length === 0 ? hostHeight : Math.max(...positions.map(entry => entry.position.y + nodeHeight));
    const width = Math.max(1, maxX - minX);
    const height = Math.max(1, maxY - minY);

    return {
        minX,
        minY,
        width,
        height
    };
}

function resolvePalette(node) {
    switch ((node?.paletteKey || "").toLowerCase()) {
        case "success":
            return { fill: "#ecfdf5", border: "#10b981", text: "#065f46" };
        case "warning":
            return { fill: "#fffbeb", border: "#f59e0b", text: "#92400e" };
        case "accent":
            return { fill: "#eef2ff", border: "#6366f1", text: "#3730a3" };
        case "info":
            return { fill: "#eff6ff", border: "#3b82f6", text: "#1d4ed8" };
        default:
            return { fill: "#f8fafc", border: "#64748b", text: "#0f172a" };
    }
}

function drawRoundedRect(context, x, y, width, height, radius) {
    context.beginPath();
    context.moveTo(x + radius, y);
    context.lineTo(x + width - radius, y);
    context.quadraticCurveTo(x + width, y, x + width, y + radius);
    context.lineTo(x + width, y + height - radius);
    context.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
    context.lineTo(x + radius, y + height);
    context.quadraticCurveTo(x, y + height, x, y + height - radius);
    context.lineTo(x, y + radius);
    context.quadraticCurveTo(x, y, x + radius, y);
    context.closePath();
}

function buildFrameBounds(surface, frame) {
    const nodes = Array.isArray(surface.nodes) ? surface.nodes : [];
    const anchorIds = Array.isArray(frame?.anchorNodeIds) ? frame.anchorNodeIds : [];
    const anchors = nodes
        .filter(node => anchorIds.includes(node.id))
        .map(node => getNodePosition(surface, node));

    if (anchors.length === 0) {
        return null;
    }

    const minX = Math.min(...anchors.map(position => position.x)) - framePadding;
    const minY = Math.min(...anchors.map(position => position.y)) - framePadding;
    const maxX = Math.max(...anchors.map(position => position.x + nodeWidth)) + framePadding;
    const maxY = Math.max(...anchors.map(position => position.y + nodeHeight)) + framePadding;

    return {
        x: minX,
        y: minY,
        width: maxX - minX,
        height: maxY - minY
    };
}

function drawSurface(surface, canvas) {
    const context = canvas.getContext("2d");
    if (!context) {
        throw new Error("Canvas 2D context is unavailable.");
    }

    const devicePixelRatio = window.devicePixelRatio || 1;
    const scene = getSceneMetrics(surface);
    const padding = 32;
    const scale = Math.min(
        (canvas.clientWidth - (padding * 2)) / scene.width,
        (canvas.clientHeight - (padding * 2)) / scene.height,
        1);
    const translateX = padding - (scene.minX * scale);
    const translateY = padding - (scene.minY * scale);

    canvas.width = Math.max(1, Math.round(canvas.clientWidth * devicePixelRatio));
    canvas.height = Math.max(1, Math.round(canvas.clientHeight * devicePixelRatio));
    context.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
    context.clearRect(0, 0, canvas.clientWidth, canvas.clientHeight);
    context.fillStyle = "#020617";
    context.fillRect(0, 0, canvas.clientWidth, canvas.clientHeight);

    const nodes = Array.isArray(surface.nodes) ? surface.nodes : [];
    const nodeLookup = new Map(nodes.map(node => [node.id, node]));
    const frames = Array.isArray(surface?.uiState?.groupFrames) ? surface.uiState.groupFrames : [];
    const links = Array.isArray(surface.links) ? surface.links : [];

    context.save();
    context.translate(translateX, translateY);
    context.scale(scale, scale);

    for (const frame of frames) {
        const bounds = buildFrameBounds(surface, frame);
        if (!bounds) {
            continue;
        }

        context.save();
        context.fillStyle = "rgba(148, 163, 184, 0.08)";
        context.strokeStyle = "rgba(148, 163, 184, 0.48)";
        context.lineWidth = 2;
        drawRoundedRect(context, bounds.x, bounds.y, bounds.width, bounds.height, 22);
        context.fill();
        context.stroke();
        context.fillStyle = "#cbd5e1";
        context.font = "600 13px ui-sans-serif, system-ui, sans-serif";
        context.fillText(frame.label || "Group", bounds.x + 18, bounds.y + 22);
        context.restore();
    }

    for (const link of links) {
        const source = nodeLookup.get(link.sourceId);
        const target = nodeLookup.get(link.targetId);
        if (!source || !target) {
            continue;
        }

        const sourcePosition = getNodePosition(surface, source);
        const targetPosition = getNodePosition(surface, target);
        const sourceX = sourcePosition.x + (nodeWidth / 2);
        const sourceY = sourcePosition.y + nodeHeight;
        const targetX = targetPosition.x + (nodeWidth / 2);
        const targetY = targetPosition.y;

        context.beginPath();
        context.moveTo(sourceX, sourceY);
        context.bezierCurveTo(sourceX, sourceY + 42, targetX, targetY - 42, targetX, targetY);
        context.strokeStyle = link.kind === "supports" ? "rgba(96, 165, 250, 0.72)" : "rgba(226, 232, 240, 0.68)";
        context.lineWidth = link.kind === "supports" ? 2 : 1.5;
        context.stroke();
    }

    for (const node of nodes) {
        const palette = resolvePalette(node);
        const position = getNodePosition(surface, node);
        drawRoundedRect(context, position.x, position.y, nodeWidth, nodeHeight, 18);
        context.fillStyle = palette.fill;
        context.fill();
        context.strokeStyle = palette.border;
        context.lineWidth = 2;
        context.stroke();

        context.fillStyle = palette.text;
        context.font = "600 14px ui-sans-serif, system-ui, sans-serif";
        context.fillText((node.title || "").slice(0, 24), position.x + 14, position.y + 28);
        context.font = "400 12px ui-sans-serif, system-ui, sans-serif";
        context.fillStyle = "rgba(15, 23, 42, 0.72)";
        context.fillText((node.subtitle || "").slice(0, 28), position.x + 14, position.y + 50);
        context.fillText((node.statusPill || node.status || "").slice(0, 22), position.x + 14, position.y + 72);
    }

    context.restore();

    return {
        primitiveCount: nodes.length + links.length + frames.length
    };
}

function createHiddenRetainedWrapper() {
    const wrapper = document.createElement("div");
    wrapper.style.position = "fixed";
    wrapper.style.left = "-10000px";
    wrapper.style.top = "0";
    wrapper.style.width = `${hostWidth}px`;
    wrapper.style.height = `${hostHeight}px`;
    wrapper.style.pointerEvents = "none";
    wrapper.style.opacity = "0";
    document.body.appendChild(wrapper);
    return wrapper;
}

function createDotNetRefStub() {
    return {
        invokeMethodAsync() {
            return Promise.resolve(null);
        }
    };
}

async function measureRetained(definition, iterations) {
    const workbench = window.CanDoItAll?.canvasWorkbench;
    if (!workbench?.create || !workbench?.dispose) {
        throw new Error("CanDoItAll.canvasWorkbench is not available for the retained benchmark.");
    }

    const wrapper = createHiddenRetainedWrapper();
    const dotNetRef = createDotNetRefStub();
    let totalMs = 0;
    let maxMs = 0;
    let domNodeCount = 0;

    try {
        for (let iteration = 0; iteration < iterations; iteration++) {
            const host = document.createElement("div");
            host.style.width = `${hostWidth}px`;
            host.style.height = `${hostHeight}px`;
            wrapper.appendChild(host);

            const start = performance.now();
            workbench.create(host, dotNetRef, clone(definition.surface));
            await waitForPaint();
            const duration = performance.now() - start;

            totalMs += duration;
            maxMs = Math.max(maxMs, duration);
            domNodeCount = Math.max(domNodeCount, host.querySelectorAll("*").length);

            workbench.dispose(host);
            host.remove();
        }
    }
    finally {
        wrapper.remove();
    }

    return {
        averageMs: round(totalMs / iterations),
        maxMs: round(maxMs),
        domNodeCount
    };
}

async function measureCanvas(definition, canvas, iterations) {
    let totalMs = 0;
    let maxMs = 0;
    let primitiveCount = 0;

    for (let iteration = 0; iteration < iterations; iteration++) {
        const start = performance.now();
        const drawResult = drawSurface(clone(definition.surface), canvas);
        await waitForPaint();
        const duration = performance.now() - start;

        totalMs += duration;
        maxMs = Math.max(maxMs, duration);
        primitiveCount = Math.max(primitiveCount, drawResult.primitiveCount);
    }

    return {
        averageMs: round(totalMs / iterations),
        maxMs: round(maxMs),
        primitiveCount
    };
}

function buildRecommendation(measurements) {
    const stressTier = measurements[measurements.length - 1];
    if (!stressTier) {
        return {
            recommendation: "No-go",
            summary: "No benchmark data was captured."
        };
    }

    const gainPercent = round(stressTier.improvementRatio * 100);
    if (stressTier.improvementRatio <= 0) {
        return {
            recommendation: "No-go",
            summary: "The narrow 2D canvas prototype did not beat the retained DOM-SVG baseline on the largest measured tier, so there is no evidence-based reason to replace the shipped renderer."
        };
    }

    return {
        recommendation: "No-go",
        summary: `The prototype was ${gainPercent}% faster on raw scene materialization at the largest tier, but it still drops accessibility mirrors, overlay composition, and export-path reuse. Keep the retained renderer as the canonical shipped path.`
    };
}

export function renderPrototype(canvas, definition) {
    if (!(canvas instanceof HTMLCanvasElement) || !definition?.surface) {
        return;
    }

    drawSurface(definition.surface, canvas);
}

export async function runBenchmarkSuite(canvas, definitions, iterations) {
    if (!(canvas instanceof HTMLCanvasElement)) {
        throw new Error("Benchmark canvas element is missing.");
    }

    const normalizedIterations = Math.max(2, Number(iterations) || 4);
    const measurements = [];

    for (const definition of definitions || []) {
        const retained = await measureRetained(definition, normalizedIterations);
        const prototype = await measureCanvas(definition, canvas, normalizedIterations);
        const improvementRatio = retained.averageMs <= 0
            ? 0
            : (retained.averageMs - prototype.averageMs) / retained.averageMs;

        measurements.push({
            key: definition.key,
            label: definition.label,
            nodeCount: definition.nodeCount,
            linkCount: definition.linkCount,
            retainedAverageMs: retained.averageMs,
            canvasAverageMs: prototype.averageMs,
            retainedDomNodeCount: retained.domNodeCount,
            improvementRatio: round(improvementRatio)
        });
    }

    const decision = buildRecommendation(measurements);
    const result = {
        tiers: measurements,
        recommendation: decision.recommendation,
        summary: decision.summary
    };

    window.__canvasBenchmarkLastRun = result;
    return result;
}
