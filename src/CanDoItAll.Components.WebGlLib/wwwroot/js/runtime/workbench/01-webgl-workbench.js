import {
    THREE,
    buildAutoFitKey,
    buildRenderSurface,
    normalizeSelectedNodeIds,
    normalizeState,
    resolveFiniteNumber,
    resolveProjectionMode,
    round,
    viewPresets
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
import {
    applyCameraState,
    commitCameraState,
    createCamera,
    createControls,
    fitView,
    focusNode,
    orbitView,
    panView,
    resetView,
    setCameraViewMode,
    syncCameraModeFromSurface,
    syncCameraToSurfaceState,
    syncViewport,
    updateCameraStateFromControls,
    zoomView
} from "./06-webgl-workbench-camera.js";
import { clearScene, rebuildScene } from "./07-webgl-workbench-scene-graph.js";

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

function resolveStageFrame(host) {
    return host?.closest?.("[data-testid='webgl-sandbox-stage']")
        || host?.parentElement
        || null;
}

function syncStageBackdrop(isStageMaximized) {
    const runtimeBackdropSelector = "[data-webgl-stage-backdrop='runtime']";
    const existingRuntimeBackdrop = document.querySelector(runtimeBackdropSelector);
    if (!isStageMaximized) {
        existingRuntimeBackdrop?.remove();
        return;
    }

    if (existingRuntimeBackdrop || document.querySelector(".webgl-stage-backdrop")) {
        return;
    }

    const backdrop = document.createElement("div");
    backdrop.className = "webgl-stage-backdrop";
    backdrop.setAttribute("data-webgl-stage-backdrop", "runtime");
    backdrop.setAttribute("aria-hidden", "true");
    document.body?.appendChild(backdrop);
}

function syncStageShell(state) {
    const isStageMaximized = !!state.surface.uiState?.isStageMaximized;
    resolveStageFrame(state.host)?.classList.toggle("webgl-stage-frame--maximized", isStageMaximized);
    syncStageBackdrop(isStageMaximized);
}

function clearStageShell(state) {
    resolveStageFrame(state?.host)?.classList.remove("webgl-stage-frame--maximized");
    document.querySelector("[data-webgl-stage-backdrop='runtime']")?.remove();
}

function notifyStateChanged(state) {
    state.dotNetRef?.invokeMethodAsync("OnStateChanged", JSON.stringify(state.sourceSurface?.uiState || state.surface?.uiState || {}));
}

function notifyChromeActionRequested(state, actionId) {
    if (!state.dotNetRef || !actionId) {
        return;
    }

    state.dotNetRef
        .invokeMethodAsync("OnChromeActionRequested", actionId)
        .catch(error => console.warn("WebGL chrome action callback failed.", error));
}

function render(state) {
    syncStageShell(state);
    syncViewport(state);
    updateCameraStateFromControls(state);
    syncCameraToSurfaceState(state);
    applyCameraState(state);

    const showGrid = state.surface.uiState?.showGrid !== false;
    const transparentGround = state.surface.uiState?.transparentGround !== false;
    state.sceneDecorations.grid.visible = showGrid;
    state.sceneDecorations.floor.visible = true;
    state.sceneDecorations.floor.renderOrder = transparentGround ? -6 : -2;
    if (state.sceneDecorations.floor.userData.transparentGround !== transparentGround) {
        state.sceneDecorations.floor.userData.transparentGround = transparentGround;
        state.sceneDecorations.floor.material.depthWrite = !transparentGround;
        state.sceneDecorations.floor.material.needsUpdate = true;
    }

    state.sceneDecorations.floor.material.opacity = transparentGround
        ? showGrid ? 0.12 : 0.06
        : showGrid ? 0.84 : 0.46;

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

function syncRuntimeState(state, surface, preserveCameraState = false) {
    state.sourceSurface = structuredClone(surface);
    state.surface = buildRenderSurface(state.sourceSurface, state.chromeState);
    state.surface.uiState = state.sourceSurface.uiState || {};
    state.surface.chrome = state.sourceSurface.chrome || {};
    state.selectedNodeIds = normalizeSelectedNodeIds(state.surface);
    state.cameraState = normalizeState(state.sourceSurface, state.cameraState, preserveCameraState);
    state.diagnostics.deterministicMode = !!state.surface.uiState?.deterministicMode;
    state.diagnostics.projectionMode = state.cameraState.projectionMode;
    state.diagnostics.viewMode = state.cameraState.viewMode;

    syncCameraModeFromSurface(state);
    rebuildScene(state);

    if (state.chromeState.connectSourceNodeId && !state.nodeLookup.has(state.chromeState.connectSourceNodeId)) {
        state.chromeState.connectSourceNodeId = null;
    }

    if (state.chromeState.connectSourceAnchorId && !state.anchorLookup.has(state.chromeState.connectSourceAnchorId)) {
        state.chromeState.connectSourceAnchorId = null;
    }

    if (state.chromeState.connectSourceAnchorId) {
        const sourceAnchor = state.anchorLookup.get(state.chromeState.connectSourceAnchorId);
        state.chromeState.connectSourceNodeId = sourceAnchor?.nodeId || null;
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
        viewMode: state.cameraState.viewMode,
        activeViewPreset: state.surface.uiState?.activeViewPreset || viewPresets.overview,
        layoutMode: state.surface.uiState?.layoutMode || "center-lane",
        toolMode: state.surface.uiState?.toolMode || "select",
        nodeInfoMode: state.surface.uiState?.nodeInfoMode || "detailed",
        nodeSpacingFactor: round(resolveFiniteNumber(state.surface.uiState?.nodeSpacingFactor, 1)),
        deterministicMode: !!state.surface.uiState?.deterministicMode,
        showGrid: state.surface.uiState?.showGrid !== false,
        showAnchors: state.surface.uiState?.showAnchors !== false,
        showEdgeLabels: state.surface.uiState?.showEdgeLabels !== false,
        transparentGround: state.surface.uiState?.transparentGround !== false,
        isStageMaximized: !!state.surface.uiState?.isStageMaximized,
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
            label: value.label,
            role: value.role,
            side: value.side,
            categoryKey: value.categoryKey,
            isRequired: !!value.isRequired,
            isVisible: value.isVisible !== false,
            labelVisible: !!value.labelVisible,
            isActive: !!value.isActive,
            isCompatible: !!value.isCompatible,
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
        viewMode: state.diagnostics.viewMode,
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

function isPointInsideCanvas(state, clientX, clientY) {
    const rect = state.renderer?.domElement?.getBoundingClientRect?.();
    return !!rect &&
        clientX >= rect.left &&
        clientX <= rect.right &&
        clientY >= rect.top &&
        clientY <= rect.bottom;
}

function rememberPointer(state, event) {
    if (!state || !event || !isPointInsideCanvas(state, event.clientX, event.clientY)) {
        return;
    }

    state.lastPointer = {
        clientX: event.clientX,
        clientY: event.clientY
    };
}

function createSyntheticPointerEvent(state, button = 0) {
    const rect = state.renderer?.domElement?.getBoundingClientRect?.();
    const saved = state.lastPointer;
    const useSaved = saved && isPointInsideCanvas(state, saved.clientX, saved.clientY);
    return {
        button,
        clientX: useSaved ? saved.clientX : (rect.left + (rect.width / 2)),
        clientY: useSaved ? saved.clientY : (rect.top + (rect.height / 2)),
        preventDefault() {
        },
        stopPropagation() {
        }
    };
}

function triggerPrimaryClick(state) {
    if (!state) {
        return false;
    }

    handleClick(state, createSyntheticPointerEvent(state, 0), state.interactionDeps);
    return true;
}

function triggerDoubleClick(state) {
    if (!triggerPrimaryClick(state)) {
        return false;
    }

    const selectedNodeId = Array.from(state.selectedNodeIds || [])[0] || null;
    if (selectedNodeId) {
        focusNode(state, selectedNodeId);
    } else {
        fitView(state);
    }

    return true;
}

function triggerContextMenu(state) {
    if (!state) {
        return false;
    }

    handleContextMenu(state, createSyntheticPointerEvent(state, 2), state.interactionDeps);
    return true;
}

function dispose(state) {
    if (!state) {
        return;
    }

    clearStageShell(state);
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
    const initialCameraState = normalizeState(surface);
    const camera = createCamera(initialCameraState.projectionMode, viewport.width, viewport.height);
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
            opacity: 0.12,
            depthWrite: false,
            side: THREE.DoubleSide
        }));
    floor.rotation.x = -Math.PI / 2;
    floor.position.y = -261;
    floor.renderOrder = -6;
    floor.userData.transparentGround = true;
    scene.add(camera, ambient, hemisphere, directional, rimLight, grid, floor);

    const chromeState = {
        settingsOpen: false,
        contextMenu: null,
        showRoleNodes: true,
        showBranchNodes: true,
        connectSourceNodeId: null,
        connectSourceAnchorId: null,
        reconnectEdgeId: null,
        selectedEdgeId: null
    };

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
        anchorLabelElements: new Map(),
        edgeElements: new Map(),
        projectedNodes: new Map(),
        projectedEdges: new Map(),
        projectedAnchors: new Map(),
        nodeMeshes: [],
        markerObjects: [],
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
            projectionMode: initialCameraState.projectionMode,
            viewMode: initialCameraState.viewMode
        },
        sceneDecorations: {
            grid,
            floor
        },
        cameraState: initialCameraState,
        sourceSurface: structuredClone(surface),
        surface: buildRenderSurface(surface, chromeState),
        selectedNodeIds: new Set(),
        lastPointer: null,
        lastAutoFitKey: "",
        handlers: {},
        chromeState,
        chromeController: null,
        interactionDeps: null,
        scheduleRender
    };

    syncViewport(state, true);

    state.surface.uiState = state.sourceSurface.uiState || {};
    state.surface.chrome = state.sourceSurface.chrome || {};
    state.selectedNodeIds = normalizeSelectedNodeIds(state.surface);

    state.chromeController = new WebGlWorkbenchChromeController(state);
    state.interactionDeps = {
        scheduleRender,
        notifyStateChanged,
        syncCameraToSurfaceState,
        syncRuntimeState,
        fitView,
        focusNode,
        resetView,
        rebuildScene,
        setCameraViewMode,
        requestChromeAction: actionId => notifyChromeActionRequested(state, actionId)
    };

    state.handlers.pointerDown = event => {
        rememberPointer(state, event);
        handlePointerDown(state, event, state.interactionDeps);
    };
    state.handlers.pointerMove = event => {
        rememberPointer(state, event);
        handlePointerMove(state, event, state.interactionDeps);
    };
    state.handlers.click = event => {
        rememberPointer(state, event);
        handleClick(state, event, state.interactionDeps);
    };
    state.handlers.contextMenu = event => {
        rememberPointer(state, event);
        handleContextMenu(state, event, state.interactionDeps);
    };
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
        const currentCameraRevision = Number(state.sourceSurface?.uiState?.cameraRevision) || 0;
        const nextCameraRevision = Number(surface?.uiState?.cameraRevision) || 0;
        const preserveCameraState = !shouldAutoFit && currentCameraRevision === nextCameraRevision;
        syncRuntimeState(state, surface, preserveCameraState);
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
    setCameraView(host, viewMode) {
        const state = resolveState(host);
        if (!state) {
            return false;
        }

        setCameraViewMode(state, viewMode, true);
        return true;
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
    triggerPrimaryClick(host) {
        return triggerPrimaryClick(resolveState(host));
    },
    triggerDoubleClick(host) {
        return triggerDoubleClick(resolveState(host));
    },
    triggerContextMenu(host) {
        return triggerContextMenu(resolveState(host));
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
