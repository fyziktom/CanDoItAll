import { OrbitControls } from "../../../vendor/OrbitControls.js";
import {
    THREE,
    buildAutoFitKey,
    buildRenderSurface,
    cameraDefaults,
    clamp,
    clampDistance,
    clampPolar,
    createDefaultCameraState,
    focusHost,
    isBranchNode,
    isRoleNode,
    normalizeSelectedNodeIds,
    normalizeState,
    resolveFiniteNumber,
    resolvePerspectiveZoom,
    resolveProjectionMode,
    round,
    toSceneY,
    viewPresets,
    projectionModes
} from "./02-webgl-workbench-core.js";
import { syncDomOverlays } from "./03-webgl-workbench-overlays.js";
import { WebGlWorkbenchChromeController } from "./04-webgl-workbench-chrome.js";
import {
    applyChromeAction,
    finishPointerInteraction,
    getAnchorCenter,
    handleClick,
    handleContextMenu,
    handlePointerDown,
    handlePointerMove,
    handlePointerUp,
    simulateConnection,
    simulateDrag
} from "./05-webgl-workbench-interaction.js";

const root = window.CanDoItAll = window.CanDoItAll || {};

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
    if (state.camera.isPerspectiveCamera) {
        state.camera.aspect = state.viewport.width / state.viewport.height;
    } else {
        state.camera.left = -state.viewport.width / 2;
        state.camera.right = state.viewport.width / 2;
        state.camera.top = state.viewport.height / 2;
        state.camera.bottom = -state.viewport.height / 2;
        state.camera.zoom = Math.max(0.2, state.cameraState.zoom || 1);
    }

    state.camera.updateProjectionMatrix();
    state.chromeController?.updateViewport();
}

function syncViewport(state, force = false) {
    const width = Math.max(Math.round(state.host.clientWidth), 1);
    const height = Math.max(Math.round(state.host.clientHeight), 1);
    const changed = force ||
        width !== state.viewport.width ||
        height !== state.viewport.height;
    if (!changed) {
        return false;
    }

    state.viewport.width = width;
    state.viewport.height = height;
    updateCameraFrustum(state);
    state.renderer.setSize(width, height, false);
    return true;
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
    state.cameraState.polar = round(clampPolar(resolveFiniteNumber(spherical.phi, cameraDefaults.polar)));
    state.cameraState.zoom = state.camera.isPerspectiveCamera
        ? resolvePerspectiveZoom(state.cameraState.distance)
        : round(Math.max(0.2, state.camera.zoom || 1));
}

function destroyObject3D(object) {
    if (!object) {
        return;
    }

    object.traverse(child => {
        child.geometry?.dispose?.();

        if (Array.isArray(child.material)) {
            for (const material of child.material) {
                material?.map?.dispose?.();
                material?.dispose?.();
            }
            return;
        }

        child.material?.map?.dispose?.();
        child.material?.dispose?.();
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
    state.edgeHitMeshes.length = 0;
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

function createNodeObject(state, node) {
    const colors = resolveNodeColors(node);
    const group = new THREE.Group();
    const width = Number(node.width) || 220;
    const height = Number(node.height) || 128;
    const depth = Number(node.depth) || 28;
    const isSelected = state.selectedNodeIds.has(node.id) || state.chromeState.connectSourceNodeId === node.id;
    const geometry = new THREE.BoxGeometry(width, height, depth);
    const material = new THREE.MeshPhongMaterial({
        color: colors.fill,
        emissive: new THREE.Color(isSelected ? colors.accent : "#000000"),
        emissiveIntensity: isSelected ? 0.12 : 0,
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

function resolveEdgeEmphasis(edge) {
    const emphasis = clamp(resolveFiniteNumber(edge?.emphasis, edge?.isPrimaryPath ? 1.7 : 0.82), 0.55, 2.4);
    return statefulEdgeSelected(edge)
        ? Math.max(emphasis, 1.9)
        : emphasis;
}

function resolveEdgeOpacity(edge) {
    return clamp(resolveFiniteNumber(edge?.opacity, edge?.isPrimaryPath ? 0.96 : 0.58), 0.18, 1);
}

function statefulEdgeSelected(edge) {
    return !!(edge && (edge.id === edge.__selectedEdgeId || edge.id === edge.__reconnectEdgeId));
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

    const sourcePoint = state.resolveAnchorPosition(sourceNode, sourceAnchor);
    const targetPoint = state.resolveAnchorPosition(targetNode, targetAnchor);
    const depth = resolveEdgeDepth(edge);
    const control = new THREE.Vector3(
        (sourcePoint.x + targetPoint.x) / 2,
        (sourcePoint.y + targetPoint.y) / 2,
        Math.max(sourcePoint.z, targetPoint.z) + depth);
    const curve = new THREE.QuadraticBezierCurve3(sourcePoint, control, targetPoint);
    const points = curve.getPoints(32);
    const isSelected = state.chromeState.selectedEdgeId === edge.id || state.chromeState.reconnectEdgeId === edge.id;
    const emphasis = isSelected
        ? Math.max(clamp(resolveFiniteNumber(edge?.emphasis, 1), 0.55, 2.4), 1.9)
        : clamp(resolveFiniteNumber(edge?.emphasis, edge?.isPrimaryPath ? 1.7 : 0.82), 0.55, 2.4);
    const opacity = isSelected
        ? 1
        : resolveEdgeOpacity(edge);
    const group = new THREE.Group();

    if (edge.isPrimaryPath || emphasis > 1.05 || isSelected) {
        const halo = new THREE.Mesh(
            new THREE.TubeGeometry(curve, 42, 5.8 * emphasis, 12, false),
            new THREE.MeshBasicMaterial({
                color: edge.accentColor || "#2563eb",
                transparent: true,
                opacity: isSelected
                    ? 0.32
                    : Math.min(0.16 + (opacity * 0.18), 0.38)
            }));
        const tube = new THREE.Mesh(
            new THREE.TubeGeometry(curve, 42, 2.2 * emphasis, 12, false),
            new THREE.MeshPhongMaterial({
                color: edge.accentColor || "#2563eb",
                emissive: new THREE.Color(edge.accentColor || "#2563eb"),
                emissiveIntensity: isSelected
                    ? 0.24
                    : edge.isPrimaryPath ? 0.18 : 0.08,
                shininess: 85,
                transparent: true,
                opacity: isSelected
                    ? 0.96
                    : edge.isPrimaryPath
                        ? Math.min(0.76 + (opacity * 0.18), 0.94)
                        : Math.min(0.34 + (opacity * 0.2), 0.66)
            }));
        group.add(halo, tube);
    }

    const geometry = new THREE.BufferGeometry().setFromPoints(points);
    const material = new THREE.LineBasicMaterial({
        color: edge.accentColor || "#2563eb",
        transparent: true,
        opacity
    });
    const line = new THREE.Line(geometry, material);
    group.add(line);

    const hitMesh = new THREE.Mesh(
        new THREE.TubeGeometry(curve, 28, Math.max(12, 8 * emphasis), 10, false),
        new THREE.MeshBasicMaterial({
            transparent: true,
            opacity: 0,
            depthWrite: false
        }));
    hitMesh.userData = {
        edgeId: edge.id
    };
    group.add(hitMesh);

    group.userData = {
        edgeId: edge.id,
        sourceNodeId: edge.sourceNodeId,
        targetNodeId: edge.targetNodeId
    };

    state.edgeObjects.set(edge.id, group);
    state.edgeHitMeshes.push(hitMesh);
    state.scene.add(group);
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
    state.cameraState = createDefaultCameraState(state.sourceSurface || state.surface);
    fitView(state);
}

function syncCameraToSurfaceState(state) {
    const updateSurfaceCamera = surface => {
        if (!surface) {
            return;
        }

        const uiState = surface.uiState = surface.uiState || {};
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
    };

    updateSurfaceCamera(state.surface);
    updateSurfaceCamera(state.sourceSurface);
    state.cameraState.projectionMode = state.camera.isPerspectiveCamera
        ? projectionModes.perspective
        : projectionModes.orthographic;
    state.diagnostics.projectionMode = state.cameraState.projectionMode;
}

function notifyStateChanged(state) {
    state.dotNetRef?.invokeMethodAsync("OnStateChanged", JSON.stringify(state.sourceSurface?.uiState || state.surface?.uiState || {}));
}

function render(state) {
    syncViewport(state);
    updateCameraStateFromControls(state);
    syncCameraToSurfaceState(state);
    applyCameraState(state);

    const showGrid = state.surface.uiState?.showGrid !== false;
    state.sceneDecorations.grid.visible = showGrid;
    state.sceneDecorations.floor.material.opacity = showGrid ? 0.84 : 0.46;

    state.renderer.clear();
    state.renderer.render(state.scene, state.camera);
    state.chromeController.sync();
    state.chromeController.render(state.renderer);
    syncDomOverlays(state);
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

function commitCameraState(state, notifyDotNet) {
    applyCameraState(state);
    syncCameraToSurfaceState(state);
    if (notifyDotNet) {
        notifyStateChanged(state);
    }

    scheduleRender(state);
}

function syncRuntimeState(state, surface) {
    state.sourceSurface = structuredClone(surface);
    state.surface = buildRenderSurface(state.sourceSurface, state.chromeState);
    state.surface.uiState = state.sourceSurface.uiState || {};
    state.surface.chrome = state.sourceSurface.chrome || {};
    state.selectedNodeIds = normalizeSelectedNodeIds(state.surface);
    state.cameraState = normalizeState(state.sourceSurface, state.cameraState);
    state.diagnostics.deterministicMode = !!state.surface.uiState?.deterministicMode;
    state.diagnostics.projectionMode = state.cameraState.projectionMode;

    const nextProjectionMode = resolveProjectionMode(state.sourceSurface);
    if ((state.camera.isPerspectiveCamera && nextProjectionMode !== projectionModes.perspective) ||
        (!state.camera.isPerspectiveCamera && nextProjectionMode !== projectionModes.orthographic)) {
        state.scene.remove(state.camera);
        state.camera = createCamera(nextProjectionMode, state.viewport.width, state.viewport.height);
        state.controls.object = state.camera;
    }

    rebuildScene(state);

    if (state.chromeState.connectSourceNodeId && !state.nodeLookup.has(state.chromeState.connectSourceNodeId)) {
        state.chromeState.connectSourceNodeId = null;
    }

    if (state.chromeState.reconnectEdgeId && !(state.surface.edges || []).some(edge => edge.id === state.chromeState.reconnectEdgeId)) {
        state.chromeState.reconnectEdgeId = null;
    }

    if (state.chromeState.selectedEdgeId && !(state.surface.edges || []).some(edge => edge.id === state.chromeState.selectedEdgeId)) {
        state.chromeState.selectedEdgeId = null;
    }

    commitCameraState(state, false);
}

function collectSceneSnapshot(state) {
    return {
        surfaceId: state.surface.surfaceId || "",
        sceneKey: state.surface.sceneKey || "",
        projectionMode: state.cameraState.projectionMode,
        activeViewPreset: state.surface.uiState?.activeViewPreset || viewPresets.overview,
        layoutMode: state.surface.uiState?.layoutMode || "center-lane",
        toolMode: state.surface.uiState?.toolMode || "select",
        nodeInfoMode: state.surface.uiState?.nodeInfoMode || "detailed",
        nodeSpacingFactor: round(resolveFiniteNumber(state.surface.uiState?.nodeSpacingFactor, 1)),
        deterministicMode: !!state.surface.uiState?.deterministicMode,
        showGrid: state.surface.uiState?.showGrid !== false,
        showAnchors: state.surface.uiState?.showAnchors !== false,
        showEdgeLabels: state.surface.uiState?.showEdgeLabels !== false,
        showRoleNodes: state.chromeState.showRoleNodes !== false,
        showBranchNodes: state.chromeState.showBranchNodes !== false,
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
                sceneWidth: round(node.width || 0),
                sceneHeight: round(node.height || 0),
                sceneDepth: round(node.depth || 0),
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
                isPrimaryPath: !!edge.isPrimaryPath,
                emphasis: round(edge.emphasis ?? 1),
                opacity: round(edge.opacity ?? 0.82),
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
        selectedNodeIds: Array.from(state.selectedNodeIds),
        selectedEdgeId: state.chromeState.selectedEdgeId || ""
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
    state.chromeController?.dispose();
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
    renderer.autoClear = false;
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.domElement.style.touchAction = "none";
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
        edgeHitMeshes: [],
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
        sceneDecorations: {
            grid,
            floor
        },
        cameraState: normalizeState(surface),
        sourceSurface: structuredClone(surface),
        surface: buildRenderSurface(surface, {
            showRoleNodes: true,
            showBranchNodes: true
        }),
        selectedNodeIds: new Set(),
        lastAutoFitKey: "",
        handlers: {},
        chromeState: {
            settingsOpen: false,
            contextMenu: null,
            showRoleNodes: true,
            showBranchNodes: true,
            connectSourceNodeId: null,
            reconnectEdgeId: null,
            selectedEdgeId: null
        },
        resolveAnchorPosition: null,
        chromeController: null,
        interactionDeps: null
    };

    syncViewport(state, true);

    state.surface.uiState = state.sourceSurface.uiState || {};
    state.surface.chrome = state.sourceSurface.chrome || {};
    state.selectedNodeIds = normalizeSelectedNodeIds(state.surface);
    state.resolveAnchorPosition = (node, anchor) => {
        const width = Number(node.width) || 220;
        const height = Number(node.height) || 128;
        const depth = Number(node.depth) || 28;
        const side = anchor?.side || (anchor?.role === "output" ? "right" : "left");
        const totalOnSide = Math.max(1, Number(anchor?.totalOnSide) || 1);
        const order = clamp(Number(anchor?.order) || 0, 0, totalOnSide - 1);
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
    };

    state.chromeController = new WebGlWorkbenchChromeController(state);
    state.interactionDeps = {
        scheduleRender,
        notifyStateChanged,
        syncCameraToSurfaceState,
        syncRuntimeState,
        fitView,
        focusNode,
        resetView,
        rebuildScene
    };

    state.handlers.pointerDown = event => handlePointerDown(state, event, state.interactionDeps);
    state.handlers.pointerMove = event => handlePointerMove(state, event, state.interactionDeps);
    state.handlers.click = event => handleClick(state, event, state.interactionDeps);
    state.handlers.contextMenu = event => handleContextMenu(state, event, state.interactionDeps);
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
    state.handlers.pointerUp = () => handlePointerUp(state, state.interactionDeps);

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
        return JSON.stringify(state.sourceSurface?.uiState || state.surface?.uiState || {});
    },
    getSceneSnapshot(host) {
        const state = resolveState(host);
        return state
            ? collectSceneSnapshot(state)
            : null;
    },
    getChromeState(host) {
        const state = resolveState(host);
        return state?.chromeController
            ? state.chromeController.getSnapshot()
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
            ? simulateDrag(state, request || {}, state.interactionDeps)
            : false;
    },
    simulateConnection(host, request) {
        const state = resolveState(host);
        return state
            ? simulateConnection(state, request || {})
            : false;
    },
    invokeChromeAction(host, actionId) {
        const state = resolveState(host);
        return state
            ? applyChromeAction(state, actionId, state.interactionDeps)
            : false;
    },
    finishInteraction(host) {
        const state = resolveState(host);
        if (!state) {
            return false;
        }

        if (state.interaction?.kind === "synthetic-drag") {
            handlePointerUp(state, state.interactionDeps);
            return true;
        }

        return finishPointerInteraction(state, state.interactionDeps);
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
