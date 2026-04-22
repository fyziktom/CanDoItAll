import { OrbitControls } from "../../../vendor/OrbitControls.js";
import {
    THREE,
    cameraDefaults,
    cameraViewModes,
    clamp,
    clampDistance,
    clampPolar,
    createDefaultCameraState,
    focusHost,
    normalizeCameraViewMode,
    projectionModes,
    resolveCameraViewMode,
    resolveFiniteNumber,
    resolvePerspectiveZoom,
    resolveProjectionMode,
    resolveProjectionModeForViewMode,
    round,
    toSceneY,
    viewPresets
} from "./02-webgl-workbench-core.js";

function resolveSafeViewport(viewport) {
    return {
        width: Math.max(Math.round(viewport?.width || 1), 1),
        height: Math.max(Math.round(viewport?.height || 1), 1)
    };
}

function resolveCameraUp(viewMode) {
    switch (normalizeCameraViewMode(viewMode)) {
        case cameraViewModes.xz:
            return new THREE.Vector3(0, 0, -1);
        default:
            return new THREE.Vector3(0, 1, 0);
    }
}

function resolveOrthographicOffset(viewMode, distance) {
    const safeDistance = clampDistance(distance || cameraDefaults.distance);
    switch (normalizeCameraViewMode(viewMode)) {
        case cameraViewModes.yz:
            return new THREE.Vector3(-safeDistance, 0, 0);
        case cameraViewModes.xz:
            return new THREE.Vector3(0, safeDistance, 0);
        case cameraViewModes.xy:
        default:
            return new THREE.Vector3(0, 0, safeDistance);
    }
}

function resolvePerspectiveOffset(cameraState) {
    return new THREE.Vector3().setFromSpherical(
        new THREE.Spherical(
            clampDistance(cameraState?.distance || cameraDefaults.distance),
            clampPolar(cameraState?.polar),
            resolveFiniteNumber(cameraState?.azimuth, cameraDefaults.azimuth)));
}

function syncControlMode(state) {
    if (!state?.controls) {
        return;
    }

    const perspective = state.cameraState?.viewMode === cameraViewModes.perspective;
    state.controls.enableRotate = perspective;
    state.controls.mouseButtons = perspective
        ? {
            LEFT: THREE.MOUSE.ROTATE,
            MIDDLE: THREE.MOUSE.DOLLY,
            RIGHT: THREE.MOUSE.PAN
        }
        : {
            LEFT: THREE.MOUSE.PAN,
            MIDDLE: THREE.MOUSE.DOLLY,
            RIGHT: THREE.MOUSE.PAN
        };
    state.controls.touches = perspective
        ? {
            ONE: THREE.TOUCH.ROTATE,
            TWO: THREE.TOUCH.DOLLY_PAN
        }
        : {
            ONE: THREE.TOUCH.PAN || THREE.TOUCH.ROTATE,
            TWO: THREE.TOUCH.DOLLY_PAN
        };
    state.controls.update();
}

function recreateCamera(state, nextProjectionMode) {
    const { width, height } = resolveSafeViewport(state.viewport);
    const previousCamera = state.camera;
    const nextCamera = createCamera(nextProjectionMode, width, height);
    if (previousCamera) {
        state.scene?.remove(previousCamera);
    }

    state.camera = nextCamera;
    state.scene?.add(nextCamera);
    state.controls.object = nextCamera;
}

function resolveOrthographicContentSize(viewMode, size) {
    switch (normalizeCameraViewMode(viewMode)) {
        case cameraViewModes.yz:
            return {
                width: Math.max(260, size.z + 260),
                height: Math.max(240, size.y + 240)
            };
        case cameraViewModes.xz:
            return {
                width: Math.max(260, size.x + 260),
                height: Math.max(240, size.z + 240)
            };
        case cameraViewModes.xy:
        default:
            return {
                width: Math.max(260, size.x + 260),
                height: Math.max(240, size.y + 240)
            };
    }
}

function resolveViewNodes(state, preset) {
    const nodes = state.surface.nodes || [];
    switch (preset) {
        case viewPresets.roles:
            return nodes.filter(node => (node?.kind || "").includes("role"));
        case viewPresets.branching:
            return nodes.filter(node => (node?.kind || "").includes("branch"));
        case viewPresets.dependencies:
            return nodes.filter(node => !(node?.kind || "").includes("role"));
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

    if (state.cameraState.viewMode === cameraViewModes.perspective) {
        const verticalFov = THREE.MathUtils.degToRad(state.camera.fov);
        const horizontalFov = 2 * Math.atan(Math.tan(verticalFov / 2) * Math.max(state.camera.aspect, 1));
        const distanceFromHeight = (Math.max(size.y, 220) * 0.62) / Math.tan(verticalFov / 2);
        const distanceFromWidth = (Math.max(size.x, 260) * 0.58) / Math.tan(horizontalFov / 2);
        const depthPadding = Math.max(size.z * 0.42, 180);
        state.cameraState.distance = clampDistance(Math.max(distanceFromHeight, distanceFromWidth) + depthPadding + 160);
        state.cameraState.zoom = resolvePerspectiveZoom(state.cameraState.distance);
    } else {
        const viewport = resolveSafeViewport(state.viewport);
        const orthographicSize = resolveOrthographicContentSize(state.cameraState.viewMode, size);
        const zoomX = viewport.width / orthographicSize.width;
        const zoomY = viewport.height / orthographicSize.height;
        state.cameraState.zoom = clamp(Math.min(zoomX, zoomY), 0.28, 1.65);
    }

    commitCameraState(state, true);
}

export function createCamera(mode, width, height) {
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

export function syncViewport(state, force = false) {
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
    state.chromeController?.updateViewport();
    state.renderer.setSize(width, height, false);
    return true;
}

export function createControls(camera, domElement, keyTarget) {
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
    controls.keyPanSpeed = 36;
    controls.listenToKeyEvents(keyTarget);
    return controls;
}

export function applyCameraState(state) {
    const target = new THREE.Vector3(
        state.cameraState.targetX || 0,
        state.cameraState.targetY || 0,
        state.cameraState.targetZ || 0);
    const viewMode = normalizeCameraViewMode(state.cameraState.viewMode, state.cameraState.projectionMode);
    const offset = viewMode === cameraViewModes.perspective
        ? resolvePerspectiveOffset(state.cameraState)
        : resolveOrthographicOffset(viewMode, state.cameraState.distance);

    state.suppressControlEvents = true;
    state.cameraState.viewMode = viewMode;
    state.cameraState.projectionMode = resolveProjectionModeForViewMode(viewMode);
    state.controls.target.copy(target);
    state.camera.up.copy(resolveCameraUp(viewMode));
    state.camera.position.copy(target.clone().add(offset));
    if (state.camera.isOrthographicCamera) {
        state.camera.zoom = Math.max(0.2, state.cameraState.zoom || 1);
    }

    syncControlMode(state);
    state.camera.lookAt(target);
    state.camera.updateProjectionMatrix();
    state.controls.update();
    state.suppressControlEvents = false;
}

export function updateCameraStateFromControls(state) {
    state.cameraState.viewMode = normalizeCameraViewMode(state.cameraState.viewMode, state.cameraState.projectionMode);
    state.cameraState.projectionMode = state.camera.isPerspectiveCamera
        ? projectionModes.perspective
        : projectionModes.orthographic;
    state.cameraState.targetX = round(state.controls.target.x);
    state.cameraState.targetY = round(state.controls.target.y);
    state.cameraState.targetZ = round(state.controls.target.z);

    if (state.camera.isPerspectiveCamera) {
        const offset = state.camera.position.clone().sub(state.controls.target);
        const spherical = new THREE.Spherical().setFromVector3(offset);
        state.cameraState.distance = round(clampDistance(spherical.radius));
        state.cameraState.azimuth = round(resolveFiniteNumber(spherical.theta, cameraDefaults.azimuth));
        state.cameraState.polar = round(clampPolar(resolveFiniteNumber(spherical.phi, cameraDefaults.polar)));
        state.cameraState.zoom = resolvePerspectiveZoom(state.cameraState.distance);
        return;
    }

    state.cameraState.distance = round(clampDistance(state.camera.position.distanceTo(state.controls.target)));
    state.cameraState.zoom = round(Math.max(0.2, state.camera.zoom || 1));
}

export function syncCameraToSurfaceState(state) {
    const updateSurfaceCamera = surface => {
        if (!surface) {
            return;
        }

        const uiState = surface.uiState = surface.uiState || {};
        const camera = uiState.camera = uiState.camera || {};
        camera.viewMode = state.cameraState.viewMode;
        camera.projectionMode = state.cameraState.projectionMode;
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
    state.diagnostics.projectionMode = state.cameraState.projectionMode;
    state.diagnostics.viewMode = state.cameraState.viewMode;
}

export function commitCameraState(state, notifyDotNet) {
    applyCameraState(state);
    syncCameraToSurfaceState(state);
    if (notifyDotNet) {
        state.dotNetRef?.invokeMethodAsync("OnStateChanged", JSON.stringify(state.sourceSurface?.uiState || state.surface?.uiState || {}));
    }

    state.scheduleRender(state);
}

export function syncCameraModeFromSurface(state) {
    const surface = state.sourceSurface || state.surface;
    const nextProjectionMode = resolveProjectionMode(surface);
    const nextViewMode = resolveCameraViewMode(surface, nextProjectionMode);
    const needsProjectionSwap = (state.camera.isPerspectiveCamera && nextProjectionMode !== projectionModes.perspective) ||
        (state.camera.isOrthographicCamera && nextProjectionMode !== projectionModes.orthographic);
    if (needsProjectionSwap) {
        recreateCamera(state, nextProjectionMode);
    }

    state.cameraState.viewMode = nextViewMode;
    state.cameraState.projectionMode = nextProjectionMode;
    syncControlMode(state);
}

export function setCameraViewMode(state, viewMode, notifyDotNet = true) {
    const nextViewMode = normalizeCameraViewMode(viewMode, state.cameraState.projectionMode);
    const nextProjectionMode = resolveProjectionModeForViewMode(nextViewMode);
    const needsProjectionSwap = (state.camera.isPerspectiveCamera && nextProjectionMode !== projectionModes.perspective) ||
        (state.camera.isOrthographicCamera && nextProjectionMode !== projectionModes.orthographic);
    if (needsProjectionSwap) {
        recreateCamera(state, nextProjectionMode);
    }

    state.cameraState.viewMode = nextViewMode;
    state.cameraState.projectionMode = nextProjectionMode;
    commitCameraState(state, notifyDotNet);
}

export function fitView(state) {
    fitNodes(state, resolveViewNodes(state, state.surface.uiState?.activeViewPreset || viewPresets.overview));
}

export function focusNode(state, nodeId) {
    const node = state.nodeLookup.get(nodeId);
    if (!node) {
        return;
    }

    state.cameraState.targetX = round(node.x || 0);
    state.cameraState.targetY = round(toSceneY(node.y));
    state.cameraState.targetZ = round(node.z || 0);
    if (state.cameraState.viewMode === cameraViewModes.perspective) {
        const focusDistance = Math.max(420, ((Math.max(Number(node.width) || 220, Number(node.height) || 128) + (Number(node.depth) || 28)) * 2.2));
        state.cameraState.distance = clampDistance(Math.min(state.cameraState.distance || cameraDefaults.distance, focusDistance));
        state.cameraState.zoom = resolvePerspectiveZoom(state.cameraState.distance);
    } else {
        state.cameraState.zoom = clamp(Math.max(state.cameraState.zoom || 1, 1.12), 0.28, 1.85);
    }

    commitCameraState(state, true);
}

export function orbitView(state, deltaAzimuth, deltaPolar) {
    if (state.cameraState.viewMode !== cameraViewModes.perspective) {
        return;
    }

    focusHost(state);
    state.cameraState.azimuth = resolveFiniteNumber(state.cameraState.azimuth, cameraDefaults.azimuth) + (Number(deltaAzimuth) || 0);
    state.cameraState.polar = clampPolar(resolveFiniteNumber(state.cameraState.polar, cameraDefaults.polar) + (Number(deltaPolar) || 0));
    commitCameraState(state, true);
}

export function panView(state, deltaX, deltaY) {
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

export function zoomView(state, factor) {
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

export function resetView(state) {
    focusHost(state);
    state.cameraState = createDefaultCameraState(state.sourceSurface || state.surface);
    syncCameraModeFromSurface(state);
    fitView(state);
}
