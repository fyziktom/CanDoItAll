import {
    THREE
} from "./02-webgl-workbench-core.js";

function resolvePointerNdc(state, clientX, clientY) {
    const rect = state.renderer.domElement.getBoundingClientRect();
    return {
        x: ((clientX - rect.left) / rect.width) * 2 - 1,
        y: -(((clientY - rect.top) / rect.height) * 2 - 1)
    };
}

export function findMeshHit(state, event) {
    const ndc = resolvePointerNdc(state, event.clientX, event.clientY);
    state.raycaster.setFromCamera(ndc, state.camera);
    const intersections = state.raycaster.intersectObjects(state.nodeMeshes, false);
    return intersections[0] || null;
}

function findEdgeHit(state, event) {
    if (!Array.isArray(state.edgeHitMeshes) || state.edgeHitMeshes.length === 0) {
        return null;
    }

    const ndc = resolvePointerNdc(state, event.clientX, event.clientY);
    state.raycaster.setFromCamera(ndc, state.camera);
    const intersections = state.raycaster.intersectObjects(state.edgeHitMeshes, false);
    return intersections[0] || null;
}

function findAnchorHit(state, event) {
    if (!state.projectedAnchors?.size) {
        return null;
    }

    const rect = state.renderer.domElement.getBoundingClientRect();
    const pointerX = event.clientX - rect.left;
    const pointerY = event.clientY - rect.top;
    let closestHit = null;
    let closestDistanceSquared = Number.POSITIVE_INFINITY;

    for (const [anchorId, anchor] of state.projectedAnchors.entries()) {
        if (anchor.isVisible === false) {
            continue;
        }

        const radius = anchor.labelVisible || anchor.isActive || anchor.isCompatible
            ? 18
            : 14;
        const deltaX = pointerX - anchor.x;
        const deltaY = pointerY - anchor.y;
        const distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
        if (distanceSquared > (radius * radius) || distanceSquared >= closestDistanceSquared) {
            continue;
        }

        closestDistanceSquared = distanceSquared;
        closestHit = {
            anchorId,
            ...anchor
        };
    }

    return closestHit;
}

export function resolveHitTarget(state, event) {
    const anchorHit = findAnchorHit(state, event);
    if (anchorHit) {
        return {
            type: "anchor",
            anchorId: anchorHit.anchorId,
            nodeId: anchorHit.nodeId || "",
            portId: anchorHit.portId || "",
            role: anchorHit.role || "",
            side: anchorHit.side || "",
            categoryKey: anchorHit.categoryKey || ""
        };
    }

    const nodeHit = findMeshHit(state, event);
    if (nodeHit) {
        return {
            type: "node",
            nodeId: nodeHit.object?.userData?.nodeId || nodeHit.object?.parent?.userData?.nodeId || ""
        };
    }

    const edgeHit = findEdgeHit(state, event);
    if (edgeHit) {
        return {
            type: "edge",
            edgeId: edgeHit.object?.userData?.edgeId || edgeHit.object?.parent?.userData?.edgeId || ""
        };
    }

    return null;
}

export function resolveWorldPoint(state, event, zPlane) {
    const ndc = resolvePointerNdc(state, event.clientX, event.clientY);
    state.raycaster.setFromCamera(ndc, state.camera);
    const plane = new THREE.Plane(new THREE.Vector3(0, 0, 1), -zPlane);
    const target = new THREE.Vector3();
    return state.raycaster.ray.intersectPlane(plane, target)
        ? target
        : null;
}
