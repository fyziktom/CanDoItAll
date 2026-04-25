import {
    clamp,
    focusHost,
    fromSceneY,
    resolveFiniteNumber,
    resolveToolMode,
    round,
    toSceneY,
    toolModes
} from "./02-webgl-workbench-core.js";
import {
    applyChromeAction,
    applySelectedNodeIds,
    closeContextMenu,
    updateNodeSelection
} from "./09-webgl-workbench-actions.js";
import {
    findMeshHit,
    resolveWorldPoint
} from "./08-webgl-workbench-hit-testing.js";

function resolveNodeSpacingFactor(state) {
    return clamp(resolveFiniteNumber(state?.surface?.uiState?.nodeSpacingFactor, 1), 0.75, 1.85);
}

function getNodeCollisionPadding(state, node) {
    const spacingFactor = resolveNodeSpacingFactor(state);
    const clearance = 8 + (((spacingFactor - 0.75) / 1.1) * 32);
    return {
        x: round(clearance),
        y: round(clearance * 0.72),
        z: round(clearance * 0.58)
    };
}

function buildNodeBounds(node, x, y, z, padding) {
    const width = (Number(node?.width) || 220) / 2;
    const height = (Number(node?.height) || 128) / 2;
    const depth = (Number(node?.depth) || 28) / 2;
    return {
        minX: x - width - padding.x,
        maxX: x + width + padding.x,
        minY: y - height - padding.y,
        maxY: y + height + padding.y,
        minZ: z - depth - padding.z,
        maxZ: z + depth + padding.z
    };
}

function boundsOverlap(left, right) {
    return left.minX < right.maxX &&
        left.maxX > right.minX &&
        left.minY < right.maxY &&
        left.maxY > right.minY &&
        left.minZ < right.maxZ &&
        left.maxZ > right.minZ;
}

function hasNodeCollision(state, nodeId, x, y, z) {
    const node = state.nodeLookup.get(nodeId);
    if (!node) {
        return false;
    }

    const nodeBounds = buildNodeBounds(node, x, y, z, getNodeCollisionPadding(state, node));
    for (const otherNode of state.surface.nodes || []) {
        if (otherNode.id === nodeId) {
            continue;
        }

        const otherBounds = buildNodeBounds(
            otherNode,
            otherNode.x || 0,
            otherNode.y || 0,
            otherNode.z || 0,
            getNodeCollisionPadding(state, otherNode));
        if (boundsOverlap(nodeBounds, otherBounds)) {
            return true;
        }
    }

    return false;
}

function hasMeaningfulMove(startX, startY, startZ, endX, endY, endZ) {
    return Math.abs((endX || 0) - (startX || 0)) > 0.01 ||
        Math.abs((endY || 0) - (startY || 0)) > 0.01 ||
        Math.abs((endZ || 0) - (startZ || 0)) > 0.01;
}

function resolveCollisionFreePosition(state, nodeId, startX, startY, startZ, targetX, targetY, targetZ) {
    const normalizedTarget = {
        x: round(targetX),
        y: round(targetY),
        z: round(targetZ)
    };
    if (!hasNodeCollision(state, nodeId, normalizedTarget.x, normalizedTarget.y, normalizedTarget.z)) {
        return {
            ...normalizedTarget,
            blocked: false,
            moved: hasMeaningfulMove(startX, startY, startZ, normalizedTarget.x, normalizedTarget.y, normalizedTarget.z)
        };
    }

    let bestX = round(startX);
    let bestY = round(startY);
    let bestZ = round(startZ);
    let low = 0;
    let high = 1;

    for (let iteration = 0; iteration < 14; iteration += 1) {
        const factor = (low + high) / 2;
        const candidateX = round(startX + ((normalizedTarget.x - startX) * factor));
        const candidateY = round(startY + ((normalizedTarget.y - startY) * factor));
        const candidateZ = round(startZ + ((normalizedTarget.z - startZ) * factor));
        if (hasNodeCollision(state, nodeId, candidateX, candidateY, candidateZ)) {
            high = factor;
            continue;
        }

        low = factor;
        bestX = candidateX;
        bestY = candidateY;
        bestZ = candidateZ;
    }

    return {
        x: bestX,
        y: bestY,
        z: bestZ,
        blocked: true,
        moved: hasMeaningfulMove(startX, startY, startZ, bestX, bestY, bestZ)
    };
}

function mirrorNodePositionToSourceSurface(state, nodeId, x, y, z) {
    const sourceNode = state.sourceSurface?.nodes?.find(candidate => candidate.id === nodeId);
    if (!sourceNode) {
        return;
    }

    sourceNode.x = x;
    sourceNode.y = y;
    sourceNode.z = z;
}

function commitMovedNodes(state, positions, deps) {
    if (!positions.length) {
        return;
    }

    state.diagnostics.dragCommitCount += 1;
    deps.syncCameraToSurfaceState(state);
    state.dotNetRef?.invokeMethodAsync("OnNodesMoved", JSON.stringify(positions));
    deps.notifyStateChanged(state);
}

function startDrag(state, node, event) {
    const worldPoint = resolveWorldPoint(state, event, node.z || 0);
    if (!worldPoint) {
        return;
    }

    state.controls.enabled = false;
    state.suppressClick = true;
    state.interaction = {
        kind: "drag",
        nodeId: node.id,
        zPlane: node.z || 0,
        offsetX: worldPoint.x - (node.x || 0),
        offsetY: worldPoint.y - toSceneY(node.y),
        startX: node.x || 0,
        startY: node.y || 0
    };
}

export function handlePointerDown(state, event, deps) {
    focusHost(state);

    const chromeHit = state.chromeController?.hitTest(event.clientX, event.clientY);
    if (chromeHit) {
        state.suppressClick = true;
        event.preventDefault?.();
        event.stopPropagation?.();
        applyChromeAction(state, chromeHit.id, deps);
        return;
    }

    if (event.button === 0 && state.chromeState?.contextMenu) {
        closeContextMenu(state, deps);
    }

    if (event.button !== 0 || !event.shiftKey || resolveToolMode(state.surface) !== toolModes.select) {
        return;
    }

    const hit = findMeshHit(state, event);
    if (!hit) {
        return;
    }

    const nodeId = hit.object?.userData?.nodeId || hit.object?.parent?.userData?.nodeId || "";
    const node = state.nodeLookup.get(nodeId);
    if (!node) {
        return;
    }

    updateNodeSelection(state, node.id, deps);
    if (!node.isReadOnly) {
        event.preventDefault?.();
        event.stopPropagation?.();
        startDrag(state, node, event);
    }
}

export function handlePointerMove(state, event, deps) {
    if (!state.interaction || state.interaction.kind !== "drag") {
        return;
    }

    const node = state.nodeLookup.get(state.interaction.nodeId);
    const object = state.nodeObjects.get(state.interaction.nodeId);
    if (!node || !object) {
        return;
    }

    const worldPoint = resolveWorldPoint(state, event, state.interaction.zPlane);
    if (!worldPoint) {
        return;
    }

    const nextPosition = resolveCollisionFreePosition(
        state,
        node.id,
        node.x || 0,
        node.y || 0,
        node.z || 0,
        worldPoint.x - state.interaction.offsetX,
        fromSceneY(worldPoint.y - state.interaction.offsetY),
        node.z || 0);
    if (!nextPosition.moved) {
        return;
    }

    node.x = nextPosition.x;
    node.y = nextPosition.y;
    object.position.x = node.x;
    object.position.y = toSceneY(node.y);
    mirrorNodePositionToSourceSurface(state, node.id, node.x, node.y, node.z || 0);
    deps.rebuildScene(state);
    deps.scheduleRender(state);
}

export function finishPointerInteraction(state, deps) {
    if (!state.interaction) {
        return false;
    }

    if (state.interaction.kind === "drag") {
        state.controls.enabled = true;
        const node = state.nodeLookup.get(state.interaction.nodeId);
        if (node && hasMeaningfulMove(state.interaction.startX, state.interaction.startY, node.z || 0, node.x, node.y, node.z || 0)) {
            commitMovedNodes(state, [
                {
                    nodeId: node.id,
                    x: round(node.x),
                    y: round(node.y),
                    z: round(node.z)
                }
            ], deps);
        }
    }

    state.interaction = null;
    return true;
}

export function handlePointerUp(state, deps) {
    if (state.interaction?.kind === "synthetic-drag") {
        commitMovedNodes(state, state.interaction.pendingPositions || [], deps);
        state.interaction = null;
        return;
    }

    finishPointerInteraction(state, deps);
}

export function simulateDrag(state, request, deps) {
    const node = state.nodeLookup.get(request?.nodeId || "");
    if (!node) {
        return false;
    }

    const nextPosition = resolveCollisionFreePosition(
        state,
        node.id,
        node.x || 0,
        node.y || 0,
        node.z || 0,
        (node.x || 0) + (Number(request?.deltaX) || 0),
        (node.y || 0) + (Number(request?.deltaY) || 0),
        node.z || 0);
    if (!nextPosition.moved) {
        return false;
    }

    node.x = nextPosition.x;
    node.y = nextPosition.y;
    mirrorNodePositionToSourceSurface(state, node.id, node.x, node.y, node.z || 0);
    applySelectedNodeIds(state, [node.id]);
    deps.syncRuntimeState(state, state.sourceSurface);

    if (request?.release === false) {
        state.interaction = {
            kind: "synthetic-drag",
            pendingPositions: [
                {
                    nodeId: node.id,
                    x: node.x,
                    y: node.y,
                    z: node.z || 0
                }
            ]
        };
        deps.scheduleRender(state);
        return true;
    }

    commitMovedNodes(state, [
        {
            nodeId: node.id,
            x: node.x,
            y: node.y,
            z: node.z || 0
        }
    ], deps);
    deps.scheduleRender(state);
    return true;
}
