import * as THREE from "../../../vendor/three.module.min.js";
import { OrbitControls } from "../../../vendor/OrbitControls.js";

const root = window.CanDoItAll = window.CanDoItAll || {};
const projectionModes = Object.freeze({
    orthographic: "orthographic",
    perspective: "perspective"
});

const viewPresets = Object.freeze({
    overview: "overview",
    roles: "roles",
    dependencies: "dependencies",
    branching: "branching",
    focus: "focus"
});

const connectionActions = Object.freeze({
    connect: "connect",
    disconnect: "disconnect"
});

const cameraDefaults = Object.freeze({
    distance: 1180,
    azimuth: -0.72,
    polar: 1.08
});

function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
}

function round(value) {
    return Math.round((Number(value) || 0) * 100) / 100;
}

function resolveFiniteNumber(value, fallback) {
    const resolved = Number(value);
    return Number.isFinite(resolved)
        ? resolved
        : fallback;
}

function toSceneY(value) {
    return -(Number(value) || 0);
}

function fromSceneY(value) {
    return -(Number(value) || 0);
}

function isRoleNode(node) {
    return (node?.kind || "").includes("role");
}

function isBranchNode(node) {
    return (node?.kind || "").includes("branch");
}

function resolveProjectionMode(surface) {
    const configured = surface?.uiState?.camera?.projectionMode || projectionModes.orthographic;
    return configured === projectionModes.perspective
        ? projectionModes.perspective
        : projectionModes.orthographic;
}

function clampPolar(value) {
    return clamp(resolveFiniteNumber(value, cameraDefaults.polar), 0.38, Math.PI - 0.42);
}

function clampDistance(value) {
    return clamp(resolveFiniteNumber(value, cameraDefaults.distance), 260, 4600);
}

function resolvePerspectiveZoom(distance) {
    return round(clamp(cameraDefaults.distance / Math.max(260, distance || cameraDefaults.distance), 0.24, 2.5));
}

function resolvePerspectiveDistance(zoom, fallbackDistance) {
    const normalizedZoom = clamp(resolveFiniteNumber(zoom, resolvePerspectiveZoom(fallbackDistance || cameraDefaults.distance)), 0.24, 2.5);
    return clampDistance(cameraDefaults.distance / normalizedZoom);
}

function createDefaultCameraState(surface) {
    const projectionMode = resolveProjectionMode(surface);
    return {
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

function buildHostShell(host) {
    host.innerHTML = "";
    host.classList.add("wgl-runtime-host");

    const stage = document.createElement("div");
    stage.className = "wgl-stage-surface";

    const labelLayer = document.createElement("div");
    labelLayer.className = "wgl-label-layer";

    const mirrorLayer = document.createElement("div");
    mirrorLayer.className = "wgl-dom-mirror";

    const emptyState = document.createElement("div");
    emptyState.className = "wgl-empty-state";

    const emptyCard = document.createElement("div");
    emptyCard.className = "wgl-empty-state__card";
    const emptyEyebrow = document.createElement("p");
    emptyEyebrow.className = "wgl-empty-state__eyebrow";
    emptyEyebrow.textContent = "WebGL concept";
    const emptyTitle = document.createElement("h3");
    emptyTitle.className = "wgl-empty-state__title";
    const emptyBody = document.createElement("p");
    emptyBody.className = "wgl-empty-state__body";
    emptyCard.append(emptyEyebrow, emptyTitle, emptyBody);
    emptyState.appendChild(emptyCard);

    const diagnosticsPanel = document.createElement("div");
    diagnosticsPanel.className = "wgl-diagnostics-panel";

    const diagnosticsCard = document.createElement("div");
    diagnosticsCard.className = "wgl-diagnostics-panel__card";
    const diagnosticsTitle = document.createElement("p");
    diagnosticsTitle.className = "wgl-diagnostics-panel__title";
    diagnosticsTitle.textContent = "Runtime";
    const diagnosticsMeta = document.createElement("p");
    diagnosticsMeta.className = "wgl-diagnostics-panel__meta";
    diagnosticsCard.append(diagnosticsTitle, diagnosticsMeta);
    diagnosticsPanel.appendChild(diagnosticsCard);

    host.append(stage, labelLayer, mirrorLayer, emptyState, diagnosticsPanel);

    return {
        stage,
        labelLayer,
        mirrorLayer,
        emptyState,
        emptyTitle,
        emptyBody,
        diagnosticsPanel,
        diagnosticsMeta
    };
}

function createCamera(mode, width, height) {
    const safeWidth = Math.max(width, 1);
    const safeHeight = Math.max(height, 1);
    const aspect = safeWidth / safeHeight;
    if (mode === projectionModes.perspective) {
        const camera = new THREE.PerspectiveCamera(48, aspect, 0.1, 12000);
        camera.position.set(0, 240, 960);
        return camera;
    }

    const camera = new THREE.OrthographicCamera(
        -safeWidth / 2,
        safeWidth / 2,
        safeHeight / 2,
        -safeHeight / 2,
        -6000,
        8000);
    camera.position.set(0, 240, 960);
    return camera;
}

function updateCameraFrustum(state) {
    const width = Math.max(state.host.clientWidth, 1);
    const height = Math.max(state.host.clientHeight, 1);
    state.viewport = {
        width,
        height
    };

    if (state.camera.isPerspectiveCamera) {
        state.camera.aspect = width / height;
    } else {
        state.camera.left = -width / 2;
        state.camera.right = width / 2;
        state.camera.top = height / 2;
        state.camera.bottom = -height / 2;
        state.camera.zoom = Math.max(0.2, state.cameraState.zoom || 1);
    }

    state.camera.updateProjectionMatrix();
}

function createControls(camera, domElement, keyTarget) {
    const controls = new OrbitControls(camera, domElement);
    controls.enableDamping = false;
    controls.enableZoom = true;
    controls.enablePan = true;
    controls.enableRotate = true;
    controls.rotateSpeed = 0.92;
    controls.zoomSpeed = 1.08;
    controls.panSpeed = 0.82;
    controls.screenSpacePanning = true;
    controls.minDistance = 260;
    controls.maxDistance = 4600;
    controls.minPolarAngle = 0.38;
    controls.maxPolarAngle = Math.PI - 0.42;
    controls.mouseButtons = {
        LEFT: THREE.MOUSE.ROTATE,
        MIDDLE: THREE.MOUSE.DOLLY,
        RIGHT: THREE.MOUSE.PAN
    };
    controls.touches = {
        ONE: THREE.TOUCH.ROTATE,
        TWO: THREE.TOUCH.DOLLY_PAN
    };
    controls.keyPanSpeed = 36;
    controls.listenToKeyEvents(keyTarget);
    return controls;
}

function focusHost(state) {
    if (!state?.host || typeof state.host.focus !== "function") {
        return;
    }

    try {
        state.host.focus({ preventScroll: true });
    } catch {
        state.host.focus();
    }
}

function applyCameraState(state) {
    const target = new THREE.Vector3(
        state.cameraState.targetX || 0,
        state.cameraState.targetY || 0,
        state.cameraState.targetZ || 0);
    const distance = clampDistance(state.cameraState.distance || cameraDefaults.distance);
    const polar = clampPolar(state.cameraState.polar);
    const azimuth = resolveFiniteNumber(state.cameraState.azimuth, cameraDefaults.azimuth);
    const offset = new THREE.Vector3().setFromSpherical(new THREE.Spherical(distance, polar, azimuth));

    state.suppressControlEvents = true;
    state.controls.target.copy(target);
    state.camera.position.copy(target.clone().add(offset));
    if (state.camera.isOrthographicCamera) {
        state.camera.zoom = Math.max(0.2, state.cameraState.zoom || 1);
    }

    state.camera.lookAt(target);
    state.camera.updateProjectionMatrix();
    state.controls.update();
    state.suppressControlEvents = false;
}

function updateCameraStateFromControls(state) {
    const offset = state.camera.position.clone().sub(state.controls.target);
    const spherical = new THREE.Spherical().setFromVector3(offset);
    state.cameraState.targetX = round(state.controls.target.x);
    state.cameraState.targetY = round(state.controls.target.y);
    state.cameraState.targetZ = round(state.controls.target.z);
    state.cameraState.distance = round(clampDistance(spherical.radius));
    state.cameraState.azimuth = round(resolveFiniteNumber(spherical.theta, cameraDefaults.azimuth));
    state.cameraState.polar = round(clampPolar(spherical.phi));
    state.cameraState.zoom = state.camera.isPerspectiveCamera
        ? resolvePerspectiveZoom(state.cameraState.distance)
        : round(Math.max(0.2, state.camera.zoom || 1));
}

function commitCameraState(state, notifyDotNet) {
    applyCameraState(state);
    syncCameraToSurfaceState(state);
    if (notifyDotNet) {
        notifyStateChanged(state);
    }

    scheduleRender(state);
}

function destroyObject3D(object) {
    if (!object) {
        return;
    }

    object.traverse(child => {
        if (child.geometry) {
            child.geometry.dispose();
        }

        if (Array.isArray(child.material)) {
            for (const material of child.material) {
                material?.dispose?.();
            }
        } else {
            child.material?.dispose?.();
        }
    });
}

function clearScene(state) {
    for (const nodeGroup of state.nodeObjects.values()) {
        state.scene.remove(nodeGroup);
        destroyObject3D(nodeGroup);
    }

    for (const edgeObject of state.edgeObjects.values()) {
        state.scene.remove(edgeObject);
        destroyObject3D(edgeObject);
    }

    state.nodeObjects.clear();
    state.edgeObjects.clear();
    state.nodeMeshes.length = 0;
    state.projectedNodes.clear();
    state.projectedEdges.clear();
    state.projectedAnchors.clear();
}

function resolveNodeColors(node) {
    return {
        fill: node.fillColor || "#ffffff",
        border: node.borderColor || "#cbd5e1",
        accent: node.accentColor || "#2563eb"
    };
}

function resolveAnchorSide(anchor) {
    if (anchor?.side) {
        return anchor.side;
    }

    return anchor?.role === "output"
        ? "right"
        : "left";
}

function resolveAnchorPosition(node, anchor) {
    const width = Number(node.width) || 220;
    const height = Number(node.height) || 128;
    const depth = Number(node.depth) || 28;
    const side = resolveAnchorSide(anchor);
    const totalOnSide = Math.max(1, Number(anchor.totalOnSide) || 1);
    const order = clamp(Number(anchor.order) || 0, 0, totalOnSide - 1);
    const offsetRatio = totalOnSide === 1
        ? 0.5
        : order / (totalOnSide - 1);
    const verticalTravel = Math.max(24, height - 36);
    const horizontalTravel = Math.max(24, width - 40);
    const distributedY = toSceneY(node.y) + (height / 2) - 18 - (offsetRatio * verticalTravel);
    const distributedX = (node.x - (width / 2)) + 20 + (offsetRatio * horizontalTravel);
    switch (side) {
        case "right":
            return new THREE.Vector3(node.x + (width / 2), distributedY, node.z + (depth / 2));
        case "top":
            return new THREE.Vector3(distributedX, toSceneY(node.y) + (height / 2), node.z + (depth / 2));
        case "bottom":
            return new THREE.Vector3(distributedX, toSceneY(node.y) - (height / 2), node.z + (depth / 2));
        default:
            return new THREE.Vector3(node.x - (width / 2), distributedY, node.z + (depth / 2));
    }
}

function createNodeObject(state, node) {
    const colors = resolveNodeColors(node);
    const group = new THREE.Group();
    const width = Number(node.width) || 220;
    const height = Number(node.height) || 128;
    const depth = Number(node.depth) || 28;
    const geometry = new THREE.BoxGeometry(width, height, depth);
    const material = new THREE.MeshPhongMaterial({
        color: colors.fill,
        emissive: node.isSelected ? new THREE.Color(colors.accent) : new THREE.Color("#000000"),
        emissiveIntensity: node.isSelected ? 0.12 : 0,
        shininess: 55,
        transparent: true,
        opacity: 0.96
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.userData = {
        nodeId: node.id
    };
    const edges = new THREE.LineSegments(
        new THREE.EdgesGeometry(geometry),
        new THREE.LineBasicMaterial({
            color: colors.border,
            transparent: true,
            opacity: 0.92
        }));

    const accentBand = new THREE.Mesh(
        new THREE.BoxGeometry(width * 0.98, 8, 4),
        new THREE.MeshBasicMaterial({
            color: colors.accent
        }));
    accentBand.position.set(0, (height / 2) - 6, (depth / 2) + 2);

    group.add(mesh, edges, accentBand);
    group.position.set(node.x || 0, toSceneY(node.y), node.z || 0);
    group.userData = {
        nodeId: node.id
    };

    state.nodeObjects.set(node.id, group);
    state.nodeMeshes.push(mesh);
    state.scene.add(group);
}

function resolveEdgeDepth(edge) {
    const explicitDepth = Number(edge.depthOffset);
    if (Number.isFinite(explicitDepth) && explicitDepth !== 0) {
        return explicitDepth;
    }

    const category = edge.categoryKey || "";
    if (category.includes("branch")) {
        return 34;
    }

    if (category.includes("decision")) {
        return 26;
    }

    if (category.includes("messaging")) {
        return 18;
    }

    if (category.includes("artifact")) {
        return 12;
    }

    return 8;
}

function createEdgeObject(state, edge) {
    const sourceNode = state.nodeLookup.get(edge.sourceNodeId);
    const targetNode = state.nodeLookup.get(edge.targetNodeId);
    if (!sourceNode || !targetNode) {
        return;
    }

    const sourceAnchor = state.anchorLookup.get(edge.sourceAnchorId);
    const targetAnchor = state.anchorLookup.get(edge.targetAnchorId);
    if (!sourceAnchor || !targetAnchor) {
        return;
    }

    const sourcePoint = resolveAnchorPosition(sourceNode, sourceAnchor);
    const targetPoint = resolveAnchorPosition(targetNode, targetAnchor);
    const depth = resolveEdgeDepth(edge);
    const control = new THREE.Vector3(
        (sourcePoint.x + targetPoint.x) / 2,
        (sourcePoint.y + targetPoint.y) / 2,
        Math.max(sourcePoint.z, targetPoint.z) + depth);
    const curve = new THREE.QuadraticBezierCurve3(sourcePoint, control, targetPoint);
    const points = curve.getPoints(24);
    const geometry = new THREE.BufferGeometry().setFromPoints(points);
    const material = new THREE.LineBasicMaterial({
        color: edge.accentColor || "#2563eb",
        transparent: true,
        opacity: 0.86
    });
    const line = new THREE.Line(geometry, material);
    line.userData = {
        edgeId: edge.id,
        sourceNodeId: edge.sourceNodeId,
        targetNodeId: edge.targetNodeId
    };

    state.edgeObjects.set(edge.id, line);
    state.scene.add(line);
}

function createNodeAndAnchorLookups(state) {
    state.nodeLookup = new Map();
    state.anchorLookup = new Map();

    for (const node of state.surface.nodes || []) {
        state.nodeLookup.set(node.id, node);
        for (const anchor of node.anchors || []) {
            state.anchorLookup.set(anchor.id, anchor);
        }
    }
}

function rebuildScene(state) {
    clearScene(state);
    createNodeAndAnchorLookups(state);

    for (const node of state.surface.nodes || []) {
        createNodeObject(state, node);
    }

    for (const edge of state.surface.edges || []) {
        createEdgeObject(state, edge);
    }

    state.diagnostics.nodeCount = state.surface.nodes?.length || 0;
    state.diagnostics.edgeCount = state.surface.edges?.length || 0;
}

function projectPoint(state, vector) {
    const projected = vector.clone().project(state.camera);
    return {
        x: ((projected.x + 1) / 2) * state.viewport.width,
        y: ((1 - projected.y) / 2) * state.viewport.height
    };
}

function resolveNodeScreenBounds(state, node) {
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

function ensureNodeLabel(state, node) {
    let label = state.labelElements.get(node.id);
    if (label) {
        return label;
    }

    label = document.createElement("div");
    label.className = "wgl-node-label";
    label.setAttribute("data-webgl-node-id", node.id);
    label.setAttribute("aria-label", `${node.title || node.id} node`);

    const kicker = document.createElement("p");
    kicker.className = "wgl-node-label__kicker";
    const title = document.createElement("h3");
    title.className = "wgl-node-label__title";
    const subtitle = document.createElement("p");
    subtitle.className = "wgl-node-label__subtitle";
    const tags = document.createElement("div");
    tags.className = "wgl-node-label__tags";

    label.append(kicker, title, subtitle, tags);
    state.shell.labelLayer.appendChild(label);
    state.labelElements.set(node.id, label);

    return label;
}

function ensurePortElement(state, anchor) {
    let element = state.anchorElements.get(anchor.id);
    if (element) {
        return element;
    }

    element = document.createElement("div");
    element.className = "wgl-port-anchor";
    element.setAttribute("data-webgl-port-id", anchor.id);
    element.setAttribute("data-webgl-anchor-role", anchor.role || "");
    element.setAttribute("aria-label", `${anchor.label || anchor.portId || anchor.id} anchor`);
    state.shell.mirrorLayer.appendChild(element);
    state.anchorElements.set(anchor.id, element);

    return element;
}

function ensureEdgeElement(state, edge) {
    let element = state.edgeElements.get(edge.id);
    if (element) {
        return element;
    }

    element = document.createElement("div");
    element.className = "wgl-edge-anchor";
    element.setAttribute("data-webgl-edge-id", edge.id);
    element.setAttribute("aria-label", `${edge.label || edge.kind || "connection"} edge`);
    state.shell.mirrorLayer.appendChild(element);
    state.edgeElements.set(edge.id, element);

    return element;
}

function syncNodeLabels(state) {
    const activeNodeIds = new Set();
    for (const node of state.surface.nodes || []) {
        activeNodeIds.add(node.id);
        const label = ensureNodeLabel(state, node);
        const bounds = resolveNodeScreenBounds(state, node);
        state.projectedNodes.set(node.id, {
            left: round(bounds.left),
            top: round(bounds.top),
            width: round(bounds.width),
            height: round(bounds.height)
        });

        label.style.left = `${round(bounds.centerX)}px`;
        label.style.top = `${round(bounds.centerY)}px`;
        label.classList.toggle("is-selected", state.selectedNodeIds.has(node.id));
        label.querySelector(".wgl-node-label__kicker").textContent = node.kind || node.family || "Node";
        label.querySelector(".wgl-node-label__title").textContent = node.title || node.id;
        label.querySelector(".wgl-node-label__subtitle").textContent = node.subtitle || node.description || "";

        const tags = label.querySelector(".wgl-node-label__tags");
        tags.innerHTML = "";
        for (const tag of node.tags || []) {
            const tagElement = document.createElement("span");
            tagElement.className = "wgl-node-label__tag";
            tagElement.textContent = tag;
            tags.appendChild(tagElement);
        }
    }

    for (const [nodeId, element] of state.labelElements.entries()) {
        if (!activeNodeIds.has(nodeId)) {
            element.remove();
            state.labelElements.delete(nodeId);
        }
    }
}

function syncAnchors(state) {
    const activeAnchorIds = new Set();
    for (const node of state.surface.nodes || []) {
        for (const anchor of node.anchors || []) {
            activeAnchorIds.add(anchor.id);
            const position = resolveAnchorPosition(node, anchor);
            const projected = projectPoint(state, position);
            const element = ensurePortElement(state, anchor);
            element.style.left = `${round(projected.x)}px`;
            element.style.top = `${round(projected.y)}px`;
            element.style.backgroundColor = anchor.accentColor || "#2563eb";
            state.projectedAnchors.set(anchor.id, {
                nodeId: node.id,
                portId: anchor.portId,
                role: anchor.role,
                side: anchor.side,
                x: round(projected.x),
                y: round(projected.y)
            });
        }
    }

    for (const [anchorId, element] of state.anchorElements.entries()) {
        if (!activeAnchorIds.has(anchorId)) {
            element.remove();
            state.anchorElements.delete(anchorId);
        }
    }
}

function syncEdges(state) {
    const activeEdgeIds = new Set();
    for (const edge of state.surface.edges || []) {
        activeEdgeIds.add(edge.id);
        const sourceAnchor = state.projectedAnchors.get(edge.sourceAnchorId);
        const targetAnchor = state.projectedAnchors.get(edge.targetAnchorId);
        if (!sourceAnchor || !targetAnchor) {
            continue;
        }

        const element = ensureEdgeElement(state, edge);
        const x = (sourceAnchor.x + targetAnchor.x) / 2;
        const y = (sourceAnchor.y + targetAnchor.y) / 2;
        element.style.left = `${round(x)}px`;
        element.style.top = `${round(y)}px`;
        element.textContent = edge.label || "";
        state.projectedEdges.set(edge.id, {
            x: round(x),
            y: round(y),
            sourceNodeId: edge.sourceNodeId,
            sourceAnchorId: edge.sourceAnchorId,
            sourcePortId: edge.sourcePortId,
            targetNodeId: edge.targetNodeId,
            targetAnchorId: edge.targetAnchorId,
            targetPortId: edge.targetPortId,
            kind: edge.kind,
            categoryKey: edge.categoryKey
        });
    }

    for (const [edgeId, element] of state.edgeElements.entries()) {
        if (!activeEdgeIds.has(edgeId)) {
            element.remove();
            state.edgeElements.delete(edgeId);
        }
    }
}

function syncEmptyState(state) {
    const hasNodes = (state.surface.nodes?.length || 0) > 0;
    state.shell.emptyState.classList.toggle("is-visible", !hasNodes);
    state.shell.emptyTitle.textContent = state.surface.chrome?.emptyStateTitle || "No process geometry";
    state.shell.emptyBody.textContent = state.surface.chrome?.emptyStateDescription || "";
}

function syncDiagnostics(state) {
    state.shell.diagnosticsPanel.style.display = state.surface.uiState?.showDiagnostics
        ? "flex"
        : "none";
    state.shell.diagnosticsMeta.textContent =
        `${state.diagnostics.nodeCount} nodes, ${state.diagnostics.edgeCount} edges, ` +
        `${state.diagnostics.renderCount} renders, ${state.cameraState.projectionMode}`;
}

function syncCameraToSurfaceState(state) {
    const uiState = state.surface.uiState = state.surface.uiState || {};
    const camera = uiState.camera = uiState.camera || {};
    camera.projectionMode = state.camera.isPerspectiveCamera
        ? projectionModes.perspective
        : projectionModes.orthographic;
    camera.zoom = round(state.cameraState.zoom || 1);
    camera.targetX = round(state.cameraState.targetX || 0);
    camera.targetY = round(state.cameraState.targetY || 0);
    camera.targetZ = round(state.cameraState.targetZ || 0);
    camera.distance = round(state.cameraState.distance || cameraDefaults.distance);
    camera.azimuth = round(state.cameraState.azimuth || cameraDefaults.azimuth);
    camera.polar = round(state.cameraState.polar || cameraDefaults.polar);
    state.cameraState.projectionMode = camera.projectionMode;
    state.diagnostics.projectionMode = camera.projectionMode;
}

function notifyStateChanged(state) {
    state.dotNetRef?.invokeMethodAsync("OnStateChanged", JSON.stringify(state.surface.uiState || {}));
}

function render(state) {
    updateCameraStateFromControls(state);
    syncCameraToSurfaceState(state);
    updateCameraFrustum(state);
    applyCameraState(state);
    state.renderer.setSize(state.viewport.width, state.viewport.height, false);
    state.renderer.render(state.scene, state.camera);
    syncNodeLabels(state);
    syncAnchors(state);
    syncEdges(state);
    syncEmptyState(state);
    syncDiagnostics(state);
    state.diagnostics.renderCount += 1;
}

function scheduleRender(state) {
    if (state.renderHandle) {
        return;
    }

    state.renderHandle = window.requestAnimationFrame(() => {
        state.renderHandle = 0;
        render(state);
    });
}

function normalizeSelectedNodeIds(surface) {
    return new Set(surface?.uiState?.selectedNodeIds || []);
}

function updateNodeSelection(state, nodeId) {
    state.selectedNodeIds = nodeId
        ? new Set([nodeId])
        : new Set();
    if (state.surface?.uiState) {
        state.surface.uiState.selectedNodeIds = nodeId ? [nodeId] : [];
    }

    scheduleRender(state);
    state.dotNetRef?.invokeMethodAsync(
        "OnSelectionChanged",
        nodeId || null,
        JSON.stringify(Array.from(state.selectedNodeIds)));
}

function handleSelectionClick(state, event) {
    if (state.suppressClick) {
        state.suppressClick = false;
        return;
    }

    const hit = findMeshHit(state, event);
    if (!hit) {
        updateNodeSelection(state, null);
        return;
    }

    const nodeId = hit.object?.userData?.nodeId || hit.object?.parent?.userData?.nodeId || "";
    updateNodeSelection(state, nodeId || null);
}

function resolveWorldPoint(state, event, zPlane) {
    const rect = state.renderer.domElement.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    const y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
    state.raycaster.setFromCamera({ x, y }, state.camera);
    const plane = new THREE.Plane(new THREE.Vector3(0, 0, 1), -zPlane);
    const target = new THREE.Vector3();
    return state.raycaster.ray.intersectPlane(plane, target)
        ? target
        : null;
}

function findMeshHit(state, event) {
    const rect = state.renderer.domElement.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    const y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
    state.raycaster.setFromCamera({ x, y }, state.camera);
    const intersections = state.raycaster.intersectObjects(state.nodeMeshes, false);
    return intersections[0] || null;
}

function commitMovedNodes(state, positions) {
    if (!positions.length) {
        return;
    }

    state.diagnostics.dragCommitCount += 1;
    syncCameraToSurfaceState(state);
    state.dotNetRef?.invokeMethodAsync("OnNodesMoved", JSON.stringify(positions));
    notifyStateChanged(state);
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

function handlePointerDown(state, event) {
    focusHost(state);
    if (event.button !== 0 || !event.shiftKey) {
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

    updateNodeSelection(state, node.id);
    if (!node.isReadOnly) {
        event.preventDefault();
        event.stopPropagation();
        startDrag(state, node, event);
    }
}

function handlePointerMove(state, event) {
    if (!state.interaction) {
        return;
    }

    if (state.interaction.kind !== "drag") {
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

    node.x = round(worldPoint.x - state.interaction.offsetX);
    node.y = round(fromSceneY(worldPoint.y - state.interaction.offsetY));
    object.position.x = node.x;
    object.position.y = toSceneY(node.y);
    rebuildScene(state);
    scheduleRender(state);
}

function finishPointerInteraction(state) {
    if (!state.interaction) {
        return false;
    }

    if (state.interaction.kind === "drag") {
        state.controls.enabled = true;
        const node = state.nodeLookup.get(state.interaction.nodeId);
        if (node) {
            commitMovedNodes(state, [
                {
                    nodeId: node.id,
                    x: round(node.x),
                    y: round(node.y),
                    z: round(node.z)
                }
            ]);
        }
    }

    state.interaction = null;
    return true;
}

function resolveViewNodes(state, preset) {
    const nodes = state.surface.nodes || [];
    switch (preset) {
        case viewPresets.roles:
            return nodes.filter(isRoleNode);
        case viewPresets.branching:
            return nodes.filter(isBranchNode);
        case viewPresets.dependencies:
            return nodes.filter(node => !isRoleNode(node));
        case viewPresets.focus:
            return nodes.filter(node => state.selectedNodeIds.has(node.id));
        default:
            return nodes;
    }
}

function fitNodes(state, nodes) {
    if (!nodes.length) {
        return;
    }

    const bounds = new THREE.Box3();
    for (const node of nodes) {
        const halfWidth = (Number(node.width) || 220) / 2;
        const halfHeight = (Number(node.height) || 128) / 2;
        const halfDepth = (Number(node.depth) || 28) / 2;
        const sceneCenterY = toSceneY(node.y);
        bounds.expandByPoint(new THREE.Vector3((node.x || 0) - halfWidth, sceneCenterY - halfHeight, (node.z || 0) - halfDepth));
        bounds.expandByPoint(new THREE.Vector3((node.x || 0) + halfWidth, sceneCenterY + halfHeight, (node.z || 0) + halfDepth));
    }

    const center = bounds.getCenter(new THREE.Vector3());
    const size = bounds.getSize(new THREE.Vector3());
    state.cameraState.targetX = round(center.x);
    state.cameraState.targetY = round(center.y);
    state.cameraState.targetZ = round(center.z);

    if (state.camera.isPerspectiveCamera) {
        const verticalFov = THREE.MathUtils.degToRad(state.camera.fov);
        const horizontalFov = 2 * Math.atan(Math.tan(verticalFov / 2) * Math.max(state.camera.aspect, 1));
        const distanceFromHeight = (Math.max(size.y, 220) * 0.62) / Math.tan(verticalFov / 2);
        const distanceFromWidth = (Math.max(size.x, 260) * 0.58) / Math.tan(horizontalFov / 2);
        const depthPadding = Math.max(size.z * 0.42, 180);
        state.cameraState.distance = clampDistance(Math.max(distanceFromHeight, distanceFromWidth) + depthPadding + 160);
        state.cameraState.zoom = resolvePerspectiveZoom(state.cameraState.distance);
    } else {
        const contentWidth = Math.max(260, size.x + 260);
        const contentHeight = Math.max(240, size.y + 240);
        const zoomX = state.viewport.width / contentWidth;
        const zoomY = state.viewport.height / contentHeight;
        state.cameraState.zoom = clamp(Math.min(zoomX, zoomY), 0.28, 1.65);
    }

    commitCameraState(state, true);
}

function fitView(state) {
    fitNodes(state, resolveViewNodes(state, state.surface.uiState?.activeViewPreset || viewPresets.overview));
}

function focusNode(state, nodeId) {
    const node = state.nodeLookup.get(nodeId);
    if (!node) {
        return;
    }

    state.cameraState.targetX = round(node.x || 0);
    state.cameraState.targetY = round(toSceneY(node.y));
    state.cameraState.targetZ = round(node.z || 0);
    if (state.camera.isPerspectiveCamera) {
        const focusDistance = Math.max(420, ((Math.max(Number(node.width) || 220, Number(node.height) || 128) + (Number(node.depth) || 28)) * 2.2));
        state.cameraState.distance = clampDistance(Math.min(state.cameraState.distance || cameraDefaults.distance, focusDistance));
        state.cameraState.zoom = resolvePerspectiveZoom(state.cameraState.distance);
    } else {
        state.cameraState.zoom = clamp(Math.max(state.cameraState.zoom || 1, 1.12), 0.28, 1.85);
    }

    commitCameraState(state, true);
}

function orbitView(state, deltaAzimuth, deltaPolar) {
    focusHost(state);
    state.cameraState.azimuth = resolveFiniteNumber(state.cameraState.azimuth, cameraDefaults.azimuth) + (Number(deltaAzimuth) || 0);
    state.cameraState.polar = clampPolar(resolveFiniteNumber(state.cameraState.polar, cameraDefaults.polar) + (Number(deltaPolar) || 0));
    commitCameraState(state, true);
}

function panView(state, deltaX, deltaY) {
    focusHost(state);
    const forward = new THREE.Vector3();
    state.camera.getWorldDirection(forward);
    const right = new THREE.Vector3().crossVectors(forward, state.camera.up).normalize();
    const up = new THREE.Vector3().copy(state.camera.up).normalize();
    const scale = state.camera.isPerspectiveCamera
        ? Math.max(48, (state.cameraState.distance || cameraDefaults.distance) * 0.055)
        : Math.max(42, 120 / Math.max(state.cameraState.zoom || 1, 0.2));
    const translation = right.multiplyScalar((Number(deltaX) || 0) / 84 * scale)
        .add(up.multiplyScalar((Number(deltaY) || 0) / 72 * scale));
    state.cameraState.targetX = round((state.cameraState.targetX || 0) + translation.x);
    state.cameraState.targetY = round((state.cameraState.targetY || 0) + translation.y);
    state.cameraState.targetZ = round((state.cameraState.targetZ || 0) + translation.z);
    commitCameraState(state, true);
}

function zoomView(state, factor) {
    focusHost(state);
    const normalizedFactor = Math.max(0.1, Number(factor) || 1);
    if (state.camera.isPerspectiveCamera) {
        state.cameraState.distance = clampDistance((state.cameraState.distance || cameraDefaults.distance) / normalizedFactor);
        state.cameraState.zoom = resolvePerspectiveZoom(state.cameraState.distance);
    } else {
        state.cameraState.zoom = clamp((state.cameraState.zoom || 1) * normalizedFactor, 0.24, 2.5);
    }

    commitCameraState(state, true);
}

function resetView(state) {
    focusHost(state);
    state.cameraState = createDefaultCameraState(state.surface);
    fitView(state);
}

function normalizeState(surface, existingCameraState) {
    const uiState = surface.uiState || {};
    surface.uiState = uiState;
    uiState.camera = uiState.camera || {};
    const defaults = createDefaultCameraState(surface);
    const existing = existingCameraState || defaults;
    const projectionMode = resolveProjectionMode(surface);
    const zoom = clamp(resolveFiniteNumber(uiState.camera.zoom, resolveFiniteNumber(existing.zoom, defaults.zoom)), 0.2, 2.5);
    const explicitDistance = resolveFiniteNumber(uiState.camera.distance, Number.NaN);
    const distance = projectionMode === projectionModes.perspective
        ? Number.isFinite(explicitDistance)
            ? clampDistance(explicitDistance)
            : resolvePerspectiveDistance(zoom, resolveFiniteNumber(existing.distance, defaults.distance))
        : clampDistance(resolveFiniteNumber(existing.distance, defaults.distance));
    return {
        projectionMode,
        zoom: projectionMode === projectionModes.perspective
            ? resolvePerspectiveZoom(distance)
            : zoom,
        targetX: resolveFiniteNumber(uiState.camera.targetX, resolveFiniteNumber(existing.targetX, defaults.targetX)),
        targetY: resolveFiniteNumber(uiState.camera.targetY, resolveFiniteNumber(existing.targetY, defaults.targetY)),
        targetZ: resolveFiniteNumber(uiState.camera.targetZ, resolveFiniteNumber(existing.targetZ, defaults.targetZ)),
        distance,
        azimuth: resolveFiniteNumber(uiState.camera.azimuth, resolveFiniteNumber(existing.azimuth, defaults.azimuth)),
        polar: clampPolar(resolveFiniteNumber(uiState.camera.polar, resolveFiniteNumber(existing.polar, defaults.polar)))
    };
}

function buildAutoFitKey(surface) {
    return [
        surface?.sceneKey || surface?.surfaceId || "",
        resolveProjectionMode(surface),
        surface?.uiState?.activeViewPreset || viewPresets.overview
    ].join("::");
}

function syncRuntimeState(state, surface) {
    state.surface = structuredClone(surface);
    state.selectedNodeIds = normalizeSelectedNodeIds(state.surface);
    state.cameraState = normalizeState(state.surface, state.cameraState);
    state.diagnostics.deterministicMode = !!state.surface.uiState?.deterministicMode;
    state.diagnostics.projectionMode = state.cameraState.projectionMode;

    const nextProjectionMode = resolveProjectionMode(state.surface);
    if ((state.camera.isPerspectiveCamera && nextProjectionMode !== projectionModes.perspective) ||
        (!state.camera.isPerspectiveCamera && nextProjectionMode !== projectionModes.orthographic)) {
        state.scene.remove(state.camera);
        state.camera = createCamera(nextProjectionMode, state.viewport.width, state.viewport.height);
        state.controls.object = state.camera;
    }

    rebuildScene(state);
    commitCameraState(state, false);
}

function collectSceneSnapshot(state) {
    return {
        surfaceId: state.surface.surfaceId || "",
        sceneKey: state.surface.sceneKey || "",
        projectionMode: state.cameraState.projectionMode,
        activeViewPreset: state.surface.uiState?.activeViewPreset || viewPresets.overview,
        deterministicMode: !!state.surface.uiState?.deterministicMode,
        viewportWidth: state.viewport.width,
        viewportHeight: state.viewport.height,
        nodes: (state.surface.nodes || []).map(node => {
            const bounds = state.projectedNodes.get(node.id) || {
                left: 0,
                top: 0,
                width: 0,
                height: 0
            };
            return {
                id: node.id,
                kind: node.kind,
                family: node.family,
                title: node.title,
                subtitle: node.subtitle,
                x: round(node.x),
                y: round(node.y),
                z: round(node.z),
                left: bounds.left,
                top: bounds.top,
                width: bounds.width,
                height: bounds.height,
                selected: state.selectedNodeIds.has(node.id)
            };
        }),
        edges: (state.surface.edges || []).map(edge => {
            const projected = state.projectedEdges.get(edge.id) || {
                x: 0,
                y: 0
            };
            return {
                id: edge.id,
                sourceNodeId: edge.sourceNodeId,
                sourceAnchorId: edge.sourceAnchorId,
                sourcePortId: edge.sourcePortId,
                targetNodeId: edge.targetNodeId,
                targetAnchorId: edge.targetAnchorId,
                targetPortId: edge.targetPortId,
                kind: edge.kind,
                categoryKey: edge.categoryKey,
                x: projected.x,
                y: projected.y
            };
        }),
        anchors: Array.from(state.projectedAnchors.entries()).map(([id, value]) => ({
            id,
            nodeId: value.nodeId,
            portId: value.portId,
            role: value.role,
            side: value.side,
            x: value.x,
            y: value.y
        }))
    };
}

function getDiagnostics(state) {
    return {
        createCount: state.diagnostics.createCount,
        updateCount: state.diagnostics.updateCount,
        renderCount: state.diagnostics.renderCount,
        dragCommitCount: state.diagnostics.dragCommitCount,
        connectionCommitCount: state.diagnostics.connectionCommitCount,
        exportCount: state.diagnostics.exportCount,
        nodeCount: state.diagnostics.nodeCount,
        edgeCount: state.diagnostics.edgeCount,
        deterministicMode: state.diagnostics.deterministicMode,
        projectionMode: state.diagnostics.projectionMode,
        selectedNodeIds: Array.from(state.selectedNodeIds)
    };
}

function exportImageData(state) {
    state.diagnostics.exportCount += 1;
    return state.renderer.domElement
        .toDataURL("image/png")
        .replace(/^data:image\/png;base64,/, "");
}

function exportImageLength(state) {
    const imageData = exportImageData(state);
    return imageData ? imageData.length : 0;
}

function simulateDrag(state, request) {
    const node = state.nodeLookup.get(request?.nodeId || "");
    if (!node) {
        return false;
    }

    node.x = round((node.x || 0) + (Number(request?.deltaX) || 0));
    node.y = round((node.y || 0) + (Number(request?.deltaY) || 0));
    state.surface.uiState.selectedNodeIds = [node.id];
    state.selectedNodeIds = new Set([node.id]);
    syncRuntimeState(state, state.surface);

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
        scheduleRender(state);
        return true;
    }

    commitMovedNodes(state, [
        {
            nodeId: node.id,
            x: node.x,
            y: node.y,
            z: node.z || 0
        }
    ]);
    scheduleRender(state);
    return true;
}

function simulateConnection(state, request) {
    const normalized = {
        actionId: request?.actionId === connectionActions.disconnect
            ? connectionActions.disconnect
            : connectionActions.connect,
        edgeId: request?.edgeId || null,
        sourceNodeId: request?.sourceNodeId || "",
        sourceAnchorId: request?.sourceAnchorId || "",
        sourcePortId: request?.sourcePortId || null,
        targetNodeId: request?.targetNodeId || "",
        targetAnchorId: request?.targetAnchorId || "",
        targetPortId: request?.targetPortId || null,
        kind: request?.kind || "",
        categoryKey: request?.categoryKey || ""
    };
    if (!normalized.sourceNodeId || !normalized.targetNodeId) {
        return false;
    }

    state.diagnostics.connectionCommitCount += 1;
    state.dotNetRef?.invokeMethodAsync("OnConnectionChangeRequested", JSON.stringify(normalized));
    return true;
}

function getAnchorCenter(state, request) {
    if (request?.edgeId) {
        const edge = state.projectedEdges.get(request.edgeId);
        return edge
            ? {
                x: edge.x,
                y: edge.y
            }
            : null;
    }

    const anchorId = request?.anchorId || "";
    if (anchorId && state.projectedAnchors.has(anchorId)) {
        const anchor = state.projectedAnchors.get(anchorId);
        return {
            x: anchor.x,
            y: anchor.y
        };
    }

    if (request?.nodeId) {
        const node = state.projectedNodes.get(request.nodeId);
        if (!node) {
            return null;
        }

        return {
            x: node.left + (node.width / 2),
            y: node.top + (node.height / 2)
        };
    }

    return null;
}

function dispose(state) {
    if (!state) {
        return;
    }

    if (state.renderHandle) {
        window.cancelAnimationFrame(state.renderHandle);
        state.renderHandle = 0;
    }

    state.resizeObserver?.disconnect();
    state.renderer.domElement.removeEventListener("pointerdown", state.handlers.pointerDown);
    state.renderer.domElement.removeEventListener("click", state.handlers.click);
    state.renderer.domElement.removeEventListener("contextmenu", state.handlers.contextMenu);
    window.removeEventListener("pointermove", state.handlers.pointerMove);
    window.removeEventListener("pointerup", state.handlers.pointerUp);
    state.controls.removeEventListener("change", state.handlers.controlsChange);
    state.controls.removeEventListener("end", state.handlers.controlsEnd);
    state.controls.dispose();
    state.host.innerHTML = "";
    clearScene(state);
    state.renderer.dispose();
    delete state.host.__webglWorkbenchState;
}

function createState(host, dotNetRef, surface) {
    const shell = buildHostShell(host);
    const scene = new THREE.Scene();
    scene.background = new THREE.Color("#050816");
    scene.fog = new THREE.Fog("#050816", 1200, 4800);
    const renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
        preserveDrawingBuffer: true
    });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    shell.stage.appendChild(renderer.domElement);

    const viewport = {
        width: Math.max(host.clientWidth, 1),
        height: Math.max(host.clientHeight, 1)
    };
    const camera = createCamera(resolveProjectionMode(surface), viewport.width, viewport.height);
    const controls = createControls(camera, renderer.domElement, host);
    const ambient = new THREE.AmbientLight("#f8fafc", 0.28);
    const hemisphere = new THREE.HemisphereLight("#e2e8f0", "#020617", 1.2);
    const directional = new THREE.DirectionalLight("#f8fafc", 1.05);
    directional.position.set(520, 760, 620);
    const rimLight = new THREE.DirectionalLight("#38bdf8", 0.35);
    rimLight.position.set(-640, 220, -880);
    const grid = new THREE.GridHelper(4200, 32, "#334155", "#1e293b");
    grid.position.y = -260;
    if (Array.isArray(grid.material)) {
        for (const material of grid.material) {
            material.transparent = true;
            material.opacity = 0.42;
        }
    } else {
        grid.material.transparent = true;
        grid.material.opacity = 0.42;
    }
    const floor = new THREE.Mesh(
        new THREE.PlaneGeometry(4600, 4600),
        new THREE.MeshPhongMaterial({
            color: "#020617",
            transparent: true,
            opacity: 0.84,
            side: THREE.DoubleSide
        }));
    floor.rotation.x = -Math.PI / 2;
    floor.position.y = -261;
    scene.add(camera, ambient, hemisphere, directional, rimLight, grid, floor);

    const state = {
        host,
        dotNetRef,
        shell,
        scene,
        renderer,
        camera,
        controls,
        viewport,
        raycaster: new THREE.Raycaster(),
        nodeObjects: new Map(),
        edgeObjects: new Map(),
        nodeLookup: new Map(),
        anchorLookup: new Map(),
        labelElements: new Map(),
        anchorElements: new Map(),
        edgeElements: new Map(),
        projectedNodes: new Map(),
        projectedEdges: new Map(),
        projectedAnchors: new Map(),
        nodeMeshes: [],
        renderHandle: 0,
        interaction: null,
        suppressClick: false,
        suppressControlEvents: false,
        diagnostics: {
            createCount: 1,
            updateCount: 0,
            renderCount: 0,
            dragCommitCount: 0,
            connectionCommitCount: 0,
            exportCount: 0,
            nodeCount: 0,
            edgeCount: 0,
            deterministicMode: true,
            projectionMode: resolveProjectionMode(surface)
        },
        cameraState: normalizeState(surface),
        surface: {
            surfaceId: "",
            nodes: [],
            edges: [],
            uiState: {
                selectedNodeIds: [],
                activeViewPreset: viewPresets.overview
            },
            chrome: {}
        },
        selectedNodeIds: new Set(),
        lastAutoFitKey: "",
        handlers: {}
    };

    state.handlers.pointerDown = event => handlePointerDown(state, event);
    state.handlers.pointerMove = event => handlePointerMove(state, event);
    state.handlers.click = event => handleSelectionClick(state, event);
    state.handlers.contextMenu = event => event.preventDefault();
    state.handlers.controlsChange = () => {
        if (state.suppressControlEvents || state.interaction?.kind === "drag") {
            return;
        }

        updateCameraStateFromControls(state);
        scheduleRender(state);
    };
    state.handlers.controlsEnd = () => {
        if (state.suppressControlEvents || state.interaction?.kind === "drag") {
            return;
        }

        updateCameraStateFromControls(state);
        syncCameraToSurfaceState(state);
        notifyStateChanged(state);
    };
    state.handlers.pointerUp = () => {
        if (state.interaction?.kind === "synthetic-drag") {
            commitMovedNodes(state, state.interaction.pendingPositions || []);
            state.interaction = null;
            return;
        }

        finishPointerInteraction(state);
    };

    renderer.domElement.addEventListener("pointerdown", state.handlers.pointerDown);
    renderer.domElement.addEventListener("click", state.handlers.click);
    renderer.domElement.addEventListener("contextmenu", state.handlers.contextMenu);
    window.addEventListener("pointermove", state.handlers.pointerMove);
    window.addEventListener("pointerup", state.handlers.pointerUp);
    controls.addEventListener("change", state.handlers.controlsChange);
    controls.addEventListener("end", state.handlers.controlsEnd);

    state.resizeObserver = new window.ResizeObserver(() => scheduleRender(state));
    state.resizeObserver.observe(host);

    syncRuntimeState(state, surface);
    fitView(state);
    state.lastAutoFitKey = buildAutoFitKey(surface);
    host.__webglWorkbenchState = state;
    return state;
}

function resolveState(host) {
    if (!host || typeof host !== "object") {
        return null;
    }

    return host.__webglWorkbenchState || null;
}

root.webglWorkbench = {
    create(host, dotNetRef, surface) {
        if (!host) {
            return false;
        }

        const state = createState(host, dotNetRef, surface);
        scheduleRender(state);
        return true;
    },
    update(host, surface) {
        const state = resolveState(host);
        if (!state) {
            return false;
        }

        state.diagnostics.updateCount += 1;
        const nextAutoFitKey = buildAutoFitKey(surface);
        const shouldAutoFit = state.lastAutoFitKey !== nextAutoFitKey;
        syncRuntimeState(state, surface);
        if (shouldAutoFit) {
            fitView(state);
            state.lastAutoFitKey = nextAutoFitKey;
        } else {
            scheduleRender(state);
        }
        return true;
    },
    fitView(host) {
        const state = resolveState(host);
        if (!state) {
            return;
        }

        fitView(state);
    },
    focusNode(host, nodeId) {
        const state = resolveState(host);
        if (!state) {
            return;
        }

        focusNode(state, nodeId);
    },
    getState(host) {
        const state = resolveState(host);
        if (!state) {
            return JSON.stringify({});
        }

        updateCameraStateFromControls(state);
        syncCameraToSurfaceState(state);
        return JSON.stringify(state.surface.uiState || {});
    },
    getSceneSnapshot(host) {
        const state = resolveState(host);
        return state
            ? collectSceneSnapshot(state)
            : null;
    },
    getDiagnostics(host) {
        const state = resolveState(host);
        return state
            ? getDiagnostics(state)
            : null;
    },
    exportImageData(host) {
        const state = resolveState(host);
        return state
            ? exportImageData(state)
            : null;
    },
    exportImageLength(host) {
        const state = resolveState(host);
        return state
            ? exportImageLength(state)
            : 0;
    },
    simulateDrag(host, request) {
        const state = resolveState(host);
        return state
            ? simulateDrag(state, request || {})
            : false;
    },
    simulateConnection(host, request) {
        const state = resolveState(host);
        return state
            ? simulateConnection(state, request || {})
            : false;
    },
    finishInteraction(host) {
        const state = resolveState(host);
        if (!state) {
            return false;
        }

        if (state.interaction?.kind === "synthetic-drag") {
            commitMovedNodes(state, state.interaction.pendingPositions || []);
            state.interaction = null;
            return true;
        }

        return finishPointerInteraction(state);
    },
    orbitView(host, deltaAzimuth, deltaPolar) {
        const state = resolveState(host);
        if (!state) {
            return;
        }

        orbitView(state, deltaAzimuth, deltaPolar);
    },
    panView(host, deltaX, deltaY) {
        const state = resolveState(host);
        if (!state) {
            return;
        }

        panView(state, deltaX, deltaY);
    },
    zoomView(host, factor) {
        const state = resolveState(host);
        if (!state) {
            return;
        }

        zoomView(state, factor);
    },
    resetView(host) {
        const state = resolveState(host);
        if (!state) {
            return;
        }

        resetView(state);
    },
    getAnchorCenter(host, request) {
        const state = resolveState(host);
        return state
            ? getAnchorCenter(state, request || {})
            : null;
    },
    dispose(host) {
        const state = resolveState(host);
        dispose(state);
    }
};
