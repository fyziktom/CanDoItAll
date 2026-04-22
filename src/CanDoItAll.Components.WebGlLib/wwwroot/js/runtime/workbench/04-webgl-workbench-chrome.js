import {
    THREE,
    cameraViewModes,
    clamp,
    createCanvasTexture,
    drawRoundedRect,
    nodeInfoModes,
    resolveCameraViewMode,
    resolveHostPoint,
    resolveNodeInfoMode,
    resolveToolMode,
    toolModes
} from "./02-webgl-workbench-core.js";
import {
    resolveConnectionHintText
} from "./11-webgl-workbench-anchor-flow.js";

function resolveTonePalette(tone, active, toggled) {
    const base = tone || "neutral";
    if (active || toggled) {
        switch (base) {
            case "danger":
                return {
                    fillTop: "rgba(185, 28, 28, 0.96)",
                    fillBottom: "rgba(127, 29, 29, 0.96)",
                    stroke: "rgba(254, 202, 202, 0.68)",
                    text: "#fef2f2",
                    secondaryText: "rgba(254, 226, 226, 0.86)"
                };
            case "positive":
                return {
                    fillTop: "rgba(13, 148, 136, 0.96)",
                    fillBottom: "rgba(15, 118, 110, 0.96)",
                    stroke: "rgba(153, 246, 228, 0.64)",
                    text: "#ecfeff",
                    secondaryText: "rgba(204, 251, 241, 0.82)"
                };
            case "warning":
                return {
                    fillTop: "rgba(217, 119, 6, 0.96)",
                    fillBottom: "rgba(180, 83, 9, 0.96)",
                    stroke: "rgba(253, 224, 71, 0.68)",
                    text: "#fff7ed",
                    secondaryText: "rgba(254, 243, 199, 0.82)"
                };
            default:
                return {
                    fillTop: "rgba(37, 99, 235, 0.96)",
                    fillBottom: "rgba(29, 78, 216, 0.96)",
                    stroke: "rgba(147, 197, 253, 0.64)",
                    text: "#eff6ff",
                    secondaryText: "rgba(191, 219, 254, 0.82)"
                };
        }
    }

    switch (base) {
        case "danger":
            return {
                fillTop: "rgba(69, 10, 10, 0.88)",
                fillBottom: "rgba(38, 10, 10, 0.88)",
                stroke: "rgba(248, 113, 113, 0.34)",
                text: "#fecaca",
                secondaryText: "rgba(254, 202, 202, 0.72)"
            };
        case "positive":
            return {
                fillTop: "rgba(17, 94, 89, 0.88)",
                fillBottom: "rgba(19, 78, 74, 0.88)",
                stroke: "rgba(45, 212, 191, 0.3)",
                text: "#ccfbf1",
                secondaryText: "rgba(153, 246, 228, 0.72)"
            };
        case "warning":
            return {
                fillTop: "rgba(120, 53, 15, 0.9)",
                fillBottom: "rgba(92, 36, 10, 0.9)",
                stroke: "rgba(251, 191, 36, 0.34)",
                text: "#fde68a",
                secondaryText: "rgba(253, 224, 71, 0.72)"
            };
        default:
            return {
                fillTop: "rgba(15, 23, 42, 0.9)",
                fillBottom: "rgba(15, 23, 42, 0.82)",
                stroke: "rgba(148, 163, 184, 0.24)",
                text: "#e2e8f0",
                secondaryText: "rgba(148, 163, 184, 0.76)"
            };
    }
}

function createPlaneGeometry(width, height) {
    return new THREE.PlaneGeometry(Math.max(1, width), Math.max(1, height));
}

function createHudMesh(width, height, texture, opacity = 1) {
    return new THREE.Mesh(
        createPlaneGeometry(width, height),
        new THREE.MeshBasicMaterial({
            map: texture || null,
            transparent: true,
            opacity,
            depthWrite: false
        }));
}

function applyHudPosition(mesh, viewportWidth, viewportHeight, x, y, z) {
    mesh.position.set(
        x + (mesh.geometry.parameters.width / 2) - (viewportWidth / 2),
        (viewportHeight / 2) - y - (mesh.geometry.parameters.height / 2),
        z || 0);
}

function disposeObject(object) {
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

function createButtonTexture(width, height, options) {
    const palette = resolveTonePalette(options.tone, options.active, options.toggled);
    return createCanvasTexture(width, height, (context, safeWidth, safeHeight) => {
        const gradient = context.createLinearGradient(0, 0, 0, safeHeight);
        gradient.addColorStop(0, palette.fillTop);
        gradient.addColorStop(1, palette.fillBottom);

        drawRoundedRect(context, 0.5, 0.5, safeWidth - 1, safeHeight - 1, Math.min(18, safeHeight / 2), gradient, palette.stroke, 1.2);

        context.fillStyle = "rgba(255, 255, 255, 0.04)";
        drawRoundedRect(context, 5, 5, safeWidth - 10, Math.max(10, (safeHeight * 0.38)), Math.min(14, safeHeight / 2), null, null);

        if (options.caption) {
            context.font = "600 10px 'Segoe UI Variable Display', 'Segoe UI', sans-serif";
            context.fillStyle = palette.secondaryText;
            context.textAlign = "left";
            context.textBaseline = "top";
            context.fillText(options.caption, 12, 8);
        }

        context.font = `${options.emphasis ? "700" : "600"} ${options.compact ? 12 : 13}px 'Segoe UI Variable Display', 'Segoe UI', sans-serif`;
        context.fillStyle = palette.text;
        context.textAlign = "center";
        context.textBaseline = "middle";
        context.fillText(options.label || "Action", safeWidth / 2, options.caption ? (safeHeight / 2) + 6 : safeHeight / 2);
    });
}

function createPanelTexture(width, height, options) {
    return createCanvasTexture(width, height, (context, safeWidth, safeHeight) => {
        const gradient = context.createLinearGradient(0, 0, 0, safeHeight);
        gradient.addColorStop(0, "rgba(15, 23, 42, 0.94)");
        gradient.addColorStop(1, "rgba(15, 23, 42, 0.84)");
        drawRoundedRect(context, 0.5, 0.5, safeWidth - 1, safeHeight - 1, 22, gradient, "rgba(148, 163, 184, 0.28)", 1.2);

        context.fillStyle = "rgba(56, 189, 248, 0.12)";
        drawRoundedRect(context, 10, 10, safeWidth - 20, Math.max(22, safeHeight * 0.22), 16, null, null);

        if (options.title) {
            context.font = "700 13px 'Segoe UI Variable Display', 'Segoe UI', sans-serif";
            context.fillStyle = "#f8fafc";
            context.textAlign = "left";
            context.textBaseline = "top";
            context.fillText(options.title, 18, 16);
        }

        if (options.subtitle) {
            context.font = "500 11px 'Segoe UI', sans-serif";
            context.fillStyle = "rgba(191, 219, 254, 0.76)";
            context.textAlign = "left";
            context.textBaseline = "top";
            context.fillText(options.subtitle, 18, 34);
        }
    });
}

function buildToolbarButtons(state) {
    const toolMode = resolveToolMode(state.surface);
    const cameraViewMode = resolveCameraViewMode(state.surface, state.cameraState?.projectionMode);
    return [
        { id: "tool:select", label: "Select", caption: "Tool", tone: "accent", active: toolMode === toolModes.select },
        { id: "tool:delete", label: "Delete", caption: "Tool", tone: "danger", active: toolMode === toolModes.delete },
        { id: "tool:connect", label: "Connect", caption: "Tool", tone: "positive", active: toolMode === toolModes.connect },
        { id: "tool:reconnect", label: "Reconnect", caption: "Tool", tone: "warning", active: toolMode === toolModes.reconnect },
        { id: "view:fit", label: "Fit", caption: "View", tone: "neutral" },
        { id: "view:reset", label: "Reset", caption: "View", tone: "neutral" },
        { id: "camera:perspective", label: "Perspective", caption: "Camera", tone: "neutral", active: cameraViewMode === cameraViewModes.perspective },
        { id: "camera:xy", label: "XY", caption: "Camera", tone: "neutral", active: cameraViewMode === cameraViewModes.xy },
        { id: "camera:xz", label: "XZ", caption: "Camera", tone: "neutral", active: cameraViewMode === cameraViewModes.xz },
        { id: "camera:yz", label: "YZ", caption: "Camera", tone: "neutral", active: cameraViewMode === cameraViewModes.yz },
        { id: "chrome:settings", label: state.chromeState?.settingsOpen ? "Close" : "Settings", caption: "Panel", tone: "neutral", active: !!state.chromeState?.settingsOpen }
    ];
}

function buildSettingsItems(state) {
    const nodeInfoMode = resolveNodeInfoMode(state.surface);
    return [
        { id: "info:detailed", label: "Detailed labels", tone: "accent", active: nodeInfoMode === nodeInfoModes.detailed },
        { id: "info:miniature", label: "Mini labels", tone: "accent", active: nodeInfoMode === nodeInfoModes.miniature },
        { id: "info:hidden", label: "Hide labels", tone: "accent", active: nodeInfoMode === nodeInfoModes.hidden },
        { id: "toggle:grid", label: "Scene grid", tone: "neutral", toggled: state.surface.uiState?.showGrid !== false },
        { id: "toggle:transparent-ground", label: "Transparent ground", tone: "neutral", toggled: state.surface.uiState?.transparentGround !== false },
        { id: "toggle:anchors", label: "Anchors", tone: "neutral", toggled: state.surface.uiState?.showAnchors !== false },
        { id: "toggle:edge-labels", label: "Connection labels", tone: "neutral", toggled: state.surface.uiState?.showEdgeLabels !== false },
        { id: "toggle:diagnostics", label: "Diagnostics", tone: "neutral", toggled: !!state.surface.uiState?.showDiagnostics },
        { id: "toggle:roles", label: "Role nodes", tone: "neutral", toggled: state.chromeState?.showRoleNodes !== false },
        { id: "toggle:branches", label: "Branch routers", tone: "neutral", toggled: state.chromeState?.showBranchNodes !== false }
    ];
}

function resolveHintText(state) {
    const authoringHint = resolveConnectionHintText(state);
    if (authoringHint) {
        return authoringHint;
    }

    const toolMode = resolveToolMode(state.surface);
    if (toolMode === toolModes.delete) {
        return "Delete mode | click a node or connection to remove it";
    }

    return state.surface.chrome?.hintText || "Select mode | click to inspect, Shift + drag to move";
}

function buildChromeRenderKey(state) {
    const contextMenu = state.chromeState?.contextMenu;
    return JSON.stringify({
        viewportWidth: state.viewport.width,
        viewportHeight: state.viewport.height,
        toolMode: resolveToolMode(state.surface),
        viewMode: resolveCameraViewMode(state.surface, state.cameraState?.projectionMode),
        nodeInfoMode: resolveNodeInfoMode(state.surface),
        settingsOpen: !!state.chromeState?.settingsOpen,
        showRoleNodes: state.chromeState?.showRoleNodes !== false,
        showBranchNodes: state.chromeState?.showBranchNodes !== false,
        connectSourceNodeId: state.chromeState?.connectSourceNodeId || "",
        connectSourceAnchorId: state.chromeState?.connectSourceAnchorId || "",
        reconnectEdgeId: state.chromeState?.reconnectEdgeId || "",
        selectedEdgeId: state.chromeState?.selectedEdgeId || "",
        showGrid: state.surface.uiState?.showGrid !== false,
        transparentGround: state.surface.uiState?.transparentGround !== false,
        showAnchors: state.surface.uiState?.showAnchors !== false,
        showEdgeLabels: state.surface.uiState?.showEdgeLabels !== false,
        showDiagnostics: !!state.surface.uiState?.showDiagnostics,
        hintText: resolveHintText(state),
        contextMenu: contextMenu
            ? {
                title: contextMenu.title || "",
                subtitle: contextMenu.subtitle || "",
                x: Math.round(contextMenu.x || 0),
                y: Math.round(contextMenu.y || 0),
                nodeId: contextMenu.nodeId || "",
                edgeId: contextMenu.edgeId || "",
                anchorId: contextMenu.anchorId || "",
                items: (contextMenu.items || []).map(item => ({
                    id: item.id || "",
                    label: item.label || "",
                    tone: item.tone || "",
                    active: !!item.active,
                    toggled: !!item.toggled
                }))
            }
            : null
    });
}

export class WebGlWorkbenchChromeController {
    constructor(state) {
        this.state = state;
        this.scene = new THREE.Scene();
        this.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 10);
        this.camera.position.z = 5;
        this.snapshot = {
            viewportWidth: 0,
            viewportHeight: 0,
            toolMode: toolModes.select,
            nodeInfoMode: nodeInfoModes.detailed,
            settingsOpen: false,
            actions: [],
            contextMenu: null
        };
        this.objects = [];
        this.renderKey = "";
        this.updateViewport();
    }

    updateViewport() {
        const width = Math.max(1, this.state.viewport.width);
        const height = Math.max(1, this.state.viewport.height);
        this.camera.left = -width / 2;
        this.camera.right = width / 2;
        this.camera.top = height / 2;
        this.camera.bottom = -height / 2;
        this.camera.updateProjectionMatrix();
    }

    clearScene() {
        for (const object of this.objects) {
            this.scene.remove(object);
            disposeObject(object);
        }

        this.objects.length = 0;
    }

    addMesh(mesh) {
        this.scene.add(mesh);
        this.objects.push(mesh);
    }

    addCard(x, y, width, height, options, metadata) {
        const texture = createButtonTexture(width, height, options);
        const mesh = createHudMesh(width, height, texture, options.opacity ?? 1);
        applyHudPosition(mesh, this.state.viewport.width, this.state.viewport.height, x, y, metadata?.z ?? 0);
        this.addMesh(mesh);

        if (metadata?.interactive !== false) {
            this.snapshot.actions.push({
                id: metadata.id,
                label: options.label || metadata.id || "action",
                section: metadata.section || "toolbar",
                x,
                y,
                width,
                height
            });
        }
    }

    addPanel(x, y, width, height, options, z = -0.2) {
        const texture = createPanelTexture(width, height, options);
        const mesh = createHudMesh(width, height, texture);
        applyHudPosition(mesh, this.state.viewport.width, this.state.viewport.height, x, y, z);
        this.addMesh(mesh);
    }

    syncToolbar() {
        const compact = this.state.viewport.width < 1040;
        const buttonWidth = compact ? 92 : 108;
        const buttonHeight = compact ? 34 : 38;
        const gap = 10;
        const padding = compact ? 12 : 14;
        const buttons = buildToolbarButtons(this.state);
        const itemsPerRow = Math.min(compact ? 4 : 6, Math.max(buttons.length, 1));
        const rowCount = Math.ceil(buttons.length / itemsPerRow);
        const rowWidth = Math.min(itemsPerRow, buttons.length) * buttonWidth + (Math.min(itemsPerRow, buttons.length) - 1) * gap;
        const width = rowWidth + (padding * 2);
        const height = rowCount * buttonHeight + ((rowCount - 1) * gap) + (padding * 2);
        const x = Math.max(12, (this.state.viewport.width - width) / 2);
        const y = 12;

        this.addPanel(x, y, width, height, {
            title: "Workbench tools",
            subtitle: compact ? "Scene tools" : "In-scene toolbar"
        });

        buttons.forEach((button, index) => {
            const row = Math.floor(index / itemsPerRow);
            const column = index % itemsPerRow;
            this.addCard(
                x + padding + (column * (buttonWidth + gap)),
                y + padding + (row * (buttonHeight + gap)),
                buttonWidth,
                buttonHeight,
                {
                    label: button.label,
                    caption: button.caption,
                    tone: button.tone,
                    active: button.active,
                    toggled: button.toggled,
                    compact
                },
                {
                    id: button.id,
                    section: "toolbar",
                    z: 0.05
                });
        });
    }

    syncHintLine() {
        const hintText = resolveHintText(this.state);
        const width = Math.min(this.state.viewport.width - 24, this.state.viewport.width < 900 ? 340 : 520);
        const height = 34;
        const x = 12;
        const y = this.state.viewport.height - height - 12;
        this.addCard(x, y, width, height, {
            label: hintText,
            tone: "neutral",
            compact: this.state.viewport.width < 900,
            emphasis: true
        }, {
            id: "hint",
            section: "hint",
            interactive: false,
            z: 0.02
        });
    }

    syncSettingsPanel() {
        if (!this.state.chromeState?.settingsOpen) {
            return;
        }

        const compact = this.state.viewport.width < 820;
        const width = Math.min(compact ? this.state.viewport.width - 24 : 330, this.state.viewport.width - 24);
        const itemHeight = 34;
        const gap = 8;
        const padding = 14;
        const items = buildSettingsItems(this.state);
        const height = 76 + (items.length * itemHeight) + ((items.length - 1) * gap) + padding;
        const x = compact
            ? 12
            : this.state.viewport.width - width - 12;
        const y = 78;

        this.addPanel(x, y, width, height, {
            title: "Display settings",
            subtitle: "Labels, helpers, and scene filters"
        }, -0.1);

        items.forEach((item, index) => {
            this.addCard(
                x + padding,
                y + 56 + (index * (itemHeight + gap)),
                width - (padding * 2),
                itemHeight,
                {
                    label: item.label,
                    tone: item.tone,
                    active: item.active,
                    toggled: item.toggled,
                    compact: false
                },
                {
                    id: item.id,
                    section: "settings",
                    z: 0.04
                });
        });
    }

    syncContextMenu() {
        const menu = this.state.chromeState?.contextMenu;
        if (!menu || !Array.isArray(menu.items) || menu.items.length === 0) {
            this.snapshot.contextMenu = null;
            return;
        }

        const width = Math.min(280, this.state.viewport.width - 24);
        const itemHeight = 34;
        const gap = 8;
        const padding = 12;
        const height = 60 + (menu.items.length * itemHeight) + ((menu.items.length - 1) * gap) + padding;
        const x = clamp(menu.x, 12, this.state.viewport.width - width - 12);
        const y = clamp(menu.y, 12, this.state.viewport.height - height - 12);

        this.addPanel(x, y, width, height, {
            title: menu.title || "Scene actions",
            subtitle: menu.subtitle || "WebGL context menu"
        }, 0.1);

        const bounds = [];
        menu.items.forEach((item, index) => {
            const itemX = x + padding;
            const itemY = y + 48 + (index * (itemHeight + gap));
            this.addCard(
                itemX,
                itemY,
                width - (padding * 2),
                itemHeight,
                {
                    label: item.label || item.id,
                    tone: item.tone || "neutral",
                    active: item.active,
                    toggled: item.toggled
                },
                {
                    id: item.id,
                    section: "context",
                    z: 0.12
                });
            bounds.push({
                id: item.id,
                label: item.label || item.id,
                x: itemX,
                y: itemY,
                width: width - (padding * 2),
                height: itemHeight
            });
        });

        this.snapshot.contextMenu = {
            title: menu.title || "Scene actions",
            x,
            y,
            width,
            height,
            items: bounds
        };
    }

    sync() {
        const nextRenderKey = buildChromeRenderKey(this.state);
        if (nextRenderKey === this.renderKey) {
            return;
        }

        this.clearScene();
        this.updateViewport();
        this.snapshot = {
            viewportWidth: this.state.viewport.width,
            viewportHeight: this.state.viewport.height,
            toolMode: resolveToolMode(this.state.surface),
            viewMode: resolveCameraViewMode(this.state.surface, this.state.cameraState?.projectionMode),
            nodeInfoMode: resolveNodeInfoMode(this.state.surface),
            settingsOpen: !!this.state.chromeState?.settingsOpen,
            actions: [],
            contextMenu: null
        };

        this.syncToolbar();
        this.syncSettingsPanel();
        this.syncContextMenu();
        this.syncHintLine();
        this.renderKey = nextRenderKey;
    }

    render(renderer) {
        renderer.clearDepth();
        renderer.render(this.scene, this.camera);
    }

    getSnapshot() {
        return {
            ...this.snapshot,
            showRoleNodes: this.state.chromeState?.showRoleNodes !== false,
            showBranchNodes: this.state.chromeState?.showBranchNodes !== false
        };
    }

    hitTest(clientX, clientY) {
        const point = resolveHostPoint(this.state.host, clientX, clientY);
        if (!point) {
            return null;
        }

        for (let index = this.snapshot.actions.length - 1; index >= 0; index -= 1) {
            const action = this.snapshot.actions[index];
            const withinX = point.x >= action.x && point.x <= action.x + action.width;
            const withinY = point.y >= action.y && point.y <= action.y + action.height;
            if (withinX && withinY) {
                return action;
            }
        }

        return null;
    }

    dispose() {
        this.clearScene();
    }
}
