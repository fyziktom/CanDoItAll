import * as THREE from "../../../vendor/three.module.min.js";

export { THREE };

export const projectionModes = Object.freeze({
    orthographic: "orthographic",
    perspective: "perspective"
});

export const cameraViewModes = Object.freeze({
    perspective: "perspective",
    xy: "xy",
    xz: "xz",
    yz: "yz"
});

export const viewPresets = Object.freeze({
    overview: "overview",
    roles: "roles",
    dependencies: "dependencies",
    branching: "branching",
    focus: "focus"
});

export const connectionActions = Object.freeze({
    connect: "connect",
    disconnect: "disconnect",
    reconnectTarget: "reconnect-target"
});

export const toolModes = Object.freeze({
    select: "select",
    delete: "delete",
    connect: "connect",
    reconnect: "reconnect"
});

export const nodeInfoModes = Object.freeze({
    detailed: "detailed",
    miniature: "miniature",
    hidden: "hidden"
});

export const cameraDefaults = Object.freeze({
    distance: 1180,
    azimuth: -0.72,
    polar: 1.08
});

export function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}

export function round(value) {
    return Math.round((Number(value) || 0) * 100) / 100;
}

export function resolveFiniteNumber(value, fallback) {
    const resolved = Number(value);
    return Number.isFinite(resolved)
        ? resolved
        : fallback;
}

export function toSceneY(value) {
    return -(Number(value) || 0);
}

export function fromSceneY(value) {
    return -(Number(value) || 0);
}

export function isRoleNode(node) {
    return (node?.kind || "").includes("role");
}

export function isBranchNode(node) {
    return (node?.kind || "").includes("branch");
}

export function isVisualGuideNode(node) {
    const kind = (node?.kind || "").toLowerCase();
    return kind.includes("arrow") ||
        kind.includes("axis") ||
        kind.includes("neutral") ||
        kind.includes("plane") ||
        kind.includes("pointer");
}

export function isProcessStepNode(node) {
    return !isVisualGuideNode(node) && !isRoleNode(node) && !isBranchNode(node);
}

function resolveAnchorFrame(node) {
    const width = Number(node?.width) || 220;
    const height = Number(node?.height) || 128;
    const depth = Number(node?.depth) || 28;

    if (isRoleNode(node)) {
        return {
            sideOffsetX: Math.max(28, Math.min(width * 0.18, 42)),
            frontOffsetZ: Math.max(8, Math.min(depth * 0.18, 18)),
            verticalPadding: 22,
            horizontalPadding: 34
        };
    }

    if (isBranchNode(node)) {
        return {
            sideOffsetX: Math.max(42, Math.min(width * 0.24, 58)),
            frontOffsetZ: Math.max(12, Math.min(depth * 0.22, 24)),
            verticalPadding: 24,
            horizontalPadding: 42
        };
    }

    return {
        sideOffsetX: Math.max(54, Math.min(width * 0.28, 74)),
        frontOffsetZ: Math.max(14, Math.min(depth * 0.24, 28)),
        verticalPadding: 20,
        horizontalPadding: 44
    };
}

export function normalizeCameraViewMode(value, projectionMode) {
    const configured = (value || "").trim().toLowerCase();
    switch (configured) {
        case cameraViewModes.perspective:
        case cameraViewModes.xy:
        case cameraViewModes.xz:
        case cameraViewModes.yz:
            return configured;
        default:
            return projectionMode === projectionModes.perspective
                ? cameraViewModes.perspective
                : cameraViewModes.xy;
    }
}

export function resolveProjectionModeForViewMode(viewMode) {
    return normalizeCameraViewMode(viewMode) === cameraViewModes.perspective
        ? projectionModes.perspective
        : projectionModes.orthographic;
}

export function resolveCameraViewMode(surface, fallbackProjectionMode) {
    const configuredProjectionMode = (surface?.uiState?.camera?.projectionMode || fallbackProjectionMode || "").trim().toLowerCase();
    return normalizeCameraViewMode(surface?.uiState?.camera?.viewMode, configuredProjectionMode);
}

export function resolveProjectionMode(surface) {
    return resolveProjectionModeForViewMode(
        resolveCameraViewMode(surface, surface?.uiState?.camera?.projectionMode || projectionModes.orthographic));
}

export function resolveToolMode(surface) {
    const configured = (surface?.uiState?.toolMode || "").trim().toLowerCase();
    switch (configured) {
        case toolModes.delete:
        case toolModes.connect:
        case toolModes.reconnect:
            return configured;
        default:
            return toolModes.select;
    }
}

export function resolveNodeInfoMode(surface) {
    const configured = (surface?.uiState?.nodeInfoMode || "").trim().toLowerCase();
    switch (configured) {
        case nodeInfoModes.miniature:
        case nodeInfoModes.hidden:
            return configured;
        default:
            return nodeInfoModes.detailed;
    }
}

export function clampPolar(value) {
    return clamp(resolveFiniteNumber(value, cameraDefaults.polar), 0.38, Math.PI - 0.42);
}

export function clampDistance(value) {
    return clamp(resolveFiniteNumber(value, cameraDefaults.distance), 260, 4600);
}

export function resolvePerspectiveZoom(distance) {
    return round(clamp(cameraDefaults.distance / Math.max(260, distance || cameraDefaults.distance), 0.24, 2.5));
}

export function resolvePerspectiveDistance(zoom, fallbackDistance) {
    const normalizedZoom = clamp(resolveFiniteNumber(zoom, resolvePerspectiveZoom(fallbackDistance || cameraDefaults.distance)), 0.24, 2.5);
    return clampDistance(cameraDefaults.distance / normalizedZoom);
}

export function createDefaultCameraState(surface) {
    const viewMode = resolveCameraViewMode(surface, surface?.uiState?.camera?.projectionMode || projectionModes.orthographic);
    const projectionMode = resolveProjectionModeForViewMode(viewMode);
    return {
        viewMode,
        projectionMode,
        zoom: 1,
        targetX: 0,
        targetY: 0,
        targetZ: 0,
        distance: cameraDefaults.distance,
        azimuth: cameraDefaults.azimuth,
        polar: cameraDefaults.polar
    };
}

export function normalizeState(surface, existingCameraState, preserveCameraState = false) {
    const uiState = surface.uiState || {};
    surface.uiState = uiState;
    uiState.camera = uiState.camera || {};
    const defaults = createDefaultCameraState(surface);
    const existing = existingCameraState || defaults;
    const camera = preserveCameraState && existingCameraState
        ? existing
        : uiState.camera;
    const viewMode = preserveCameraState && existingCameraState
        ? normalizeCameraViewMode(existing.viewMode, existing.projectionMode || defaults.projectionMode)
        : resolveCameraViewMode(surface, existing?.projectionMode || defaults.projectionMode);
    const projectionMode = resolveProjectionModeForViewMode(viewMode);
    const zoom = clamp(resolveFiniteNumber(camera.zoom, resolveFiniteNumber(existing.zoom, defaults.zoom)), 0.2, 2.5);
    const explicitDistance = resolveFiniteNumber(camera.distance, Number.NaN);
    const distance = projectionMode === projectionModes.perspective
        ? Number.isFinite(explicitDistance)
            ? clampDistance(explicitDistance)
            : resolvePerspectiveDistance(zoom, resolveFiniteNumber(existing.distance, defaults.distance))
        : clampDistance(resolveFiniteNumber(existing.distance, defaults.distance));
    return {
        viewMode,
        projectionMode,
        zoom: projectionMode === projectionModes.perspective
            ? resolvePerspectiveZoom(distance)
            : zoom,
        targetX: resolveFiniteNumber(camera.targetX, resolveFiniteNumber(existing.targetX, defaults.targetX)),
        targetY: resolveFiniteNumber(camera.targetY, resolveFiniteNumber(existing.targetY, defaults.targetY)),
        targetZ: resolveFiniteNumber(camera.targetZ, resolveFiniteNumber(existing.targetZ, defaults.targetZ)),
        distance,
        azimuth: resolveFiniteNumber(camera.azimuth, resolveFiniteNumber(existing.azimuth, defaults.azimuth)),
        polar: clampPolar(resolveFiniteNumber(camera.polar, resolveFiniteNumber(existing.polar, defaults.polar)))
    };
}

export function buildAutoFitKey(surface) {
    return [
        surface?.sceneKey || surface?.surfaceId || "",
        resolveCameraViewMode(surface, surface?.uiState?.camera?.projectionMode || projectionModes.orthographic),
        surface?.uiState?.activeViewPreset || viewPresets.overview,
        surface?.uiState?.layoutMode || "center-lane",
        round(resolveFiniteNumber(surface?.uiState?.nodeSpacingFactor, 1))
    ].join("::");
}

export function normalizeSelectedNodeIds(surface) {
    return new Set(surface?.uiState?.selectedNodeIds || []);
}

export function focusHost(state) {
    if (!state?.host || typeof state.host.focus !== "function") {
        return;
    }

    try {
        state.host.focus({ preventScroll: true });
    } catch {
        state.host.focus();
    }
}

export function resolveAnchorSide(anchor) {
    if (anchor?.side) {
        return anchor.side;
    }

    return anchor?.role === "output"
        ? "right"
        : "left";
}

export function resolveAnchorPosition(node, anchor) {
    const width = Number(node.width) || 220;
    const height = Number(node.height) || 128;
    const frame = resolveAnchorFrame(node);
    const side = resolveAnchorSide(anchor);
    const totalOnSide = Math.max(1, Number(anchor.totalOnSide) || 1);
    const order = clamp(Number(anchor.order) || 0, 0, totalOnSide - 1);
    const offsetRatio = totalOnSide === 1
        ? 0.5
        : order / (totalOnSide - 1);
    const verticalTravel = Math.max(24, height - (frame.verticalPadding * 2));
    const horizontalTravel = Math.max(24, width - (frame.horizontalPadding * 2));
    const distributedY = toSceneY(node.y) + (height / 2) - frame.verticalPadding - (offsetRatio * verticalTravel);
    const distributedX = (node.x - (horizontalTravel / 2)) + (offsetRatio * horizontalTravel);
    switch (side) {
        case "right":
            return new THREE.Vector3(node.x + frame.sideOffsetX, distributedY, node.z + frame.frontOffsetZ);
        case "top":
            return new THREE.Vector3(distributedX, toSceneY(node.y) + (height / 2), node.z + frame.frontOffsetZ);
        case "bottom":
            return new THREE.Vector3(distributedX, toSceneY(node.y) - (height / 2), node.z + frame.frontOffsetZ);
        default:
            return new THREE.Vector3(node.x - frame.sideOffsetX, distributedY, node.z + frame.frontOffsetZ);
    }
}

export function projectPoint(state, vector) {
    const projected = vector.clone().project(state.camera);
    return {
        x: ((projected.x + 1) / 2) * state.viewport.width,
        y: ((1 - projected.y) / 2) * state.viewport.height
    };
}

export function resolveNodeScreenBounds(state, node) {
    const width = Number(node.width) || 220;
    const height = Number(node.height) || 128;
    const center = projectPoint(state, new THREE.Vector3(node.x, toSceneY(node.y), node.z || 0));
    const topLeft = projectPoint(state, new THREE.Vector3(node.x - (width / 2), toSceneY(node.y) + (height / 2), node.z || 0));
    const bottomRight = projectPoint(state, new THREE.Vector3(node.x + (width / 2), toSceneY(node.y) - (height / 2), node.z || 0));

    return {
        centerX: center.x,
        centerY: center.y,
        left: Math.min(topLeft.x, bottomRight.x),
        top: Math.min(topLeft.y, bottomRight.y),
        width: Math.abs(bottomRight.x - topLeft.x),
        height: Math.abs(bottomRight.y - topLeft.y)
    };
}

export function resolveHostPoint(host, clientX, clientY) {
    const rect = host?.getBoundingClientRect?.();
    if (!rect) {
        return null;
    }

    return {
        rect,
        x: clientX - rect.left,
        y: clientY - rect.top
    };
}

export function drawRoundedRect(context, x, y, width, height, radius, fillStyle, strokeStyle, lineWidth = 1) {
    const safeRadius = Math.max(0, Math.min(radius, width / 2, height / 2));
    context.beginPath();
    context.moveTo(x + safeRadius, y);
    context.lineTo(x + width - safeRadius, y);
    context.quadraticCurveTo(x + width, y, x + width, y + safeRadius);
    context.lineTo(x + width, y + height - safeRadius);
    context.quadraticCurveTo(x + width, y + height, x + width - safeRadius, y + height);
    context.lineTo(x + safeRadius, y + height);
    context.quadraticCurveTo(x, y + height, x, y + height - safeRadius);
    context.lineTo(x, y + safeRadius);
    context.quadraticCurveTo(x, y, x + safeRadius, y);
    context.closePath();

    if (fillStyle) {
        context.fillStyle = fillStyle;
        context.fill();
    }

    if (strokeStyle) {
        context.lineWidth = lineWidth;
        context.strokeStyle = strokeStyle;
        context.stroke();
    }
}

export function createCanvasTexture(width, height, draw) {
    const safeWidth = Math.max(1, Math.ceil(width));
    const safeHeight = Math.max(1, Math.ceil(height));
    const scale = Math.max(1, Math.min(2, window.devicePixelRatio || 1));
    const canvas = document.createElement("canvas");
    canvas.width = Math.ceil(safeWidth * scale);
    canvas.height = Math.ceil(safeHeight * scale);

    const context = canvas.getContext("2d");
    if (!context) {
        return null;
    }

    context.scale(scale, scale);
    draw(context, safeWidth, safeHeight);

    const texture = new THREE.CanvasTexture(canvas);
    texture.anisotropy = 2;
    texture.needsUpdate = true;
    return texture;
}

export function buildRenderSurface(sourceSurface, chromeState) {
    const surface = structuredClone(sourceSurface || {});
    surface.nodes = Array.isArray(surface.nodes) ? surface.nodes : [];
    surface.edges = Array.isArray(surface.edges) ? surface.edges : [];
    surface.uiState = surface.uiState || {};
    surface.chrome = surface.chrome || {};

    const showRoleNodes = chromeState?.showRoleNodes !== false;
    const showBranchNodes = chromeState?.showBranchNodes !== false;
    if (showRoleNodes && showBranchNodes) {
        return surface;
    }

    const visibleNodes = surface.nodes.filter(node => {
        if (!showRoleNodes && isRoleNode(node)) {
            return false;
        }

        if (!showBranchNodes && isBranchNode(node)) {
            return false;
        }

        return true;
    });
    const visibleNodeIds = new Set(visibleNodes.map(node => node.id));
    surface.nodes = visibleNodes;
    surface.edges = surface.edges.filter(edge =>
        visibleNodeIds.has(edge.sourceNodeId) &&
        visibleNodeIds.has(edge.targetNodeId));
    return surface;
}
