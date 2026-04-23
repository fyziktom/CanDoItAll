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

function strokeGlyph(context, centerX, centerY, size, color, lineWidth, draw) {
    context.save();
    context.translate(centerX, centerY);
    context.strokeStyle = color;
    context.lineWidth = lineWidth;
    context.lineCap = "round";
    context.lineJoin = "round";
    context.beginPath();
    draw(context, size);
    context.stroke();
    context.restore();
}

function fillGlyph(context, centerX, centerY, size, color, draw, strokeColor = null, lineWidth = 1) {
    context.save();
    context.translate(centerX, centerY);
    context.fillStyle = color;
    context.beginPath();
    draw(context, size);
    context.fill();
    if (strokeColor) {
        context.strokeStyle = strokeColor;
        context.lineWidth = lineWidth;
        context.lineJoin = "round";
        context.stroke();
    }
    context.restore();
}

function drawToolbarGlyph(context, glyph, centerX, centerY, size, palette) {
    if (!glyph) {
        return;
    }

    const strokeColor = palette.text;
    const accentColor = palette.secondaryText;
    const lineWidth = Math.max(1.7, size * 0.12);

    switch (glyph) {
        case "cursor":
            fillGlyph(context, centerX, centerY, size, strokeColor, (path, scale) => {
                path.moveTo(-scale * 0.34, -scale * 0.46);
                path.lineTo(scale * 0.24, -scale * 0.04);
                path.lineTo(scale * 0.02, scale * 0.04);
                path.lineTo(scale * 0.2, scale * 0.42);
                path.lineTo(scale * 0.04, scale * 0.48);
                path.lineTo(-scale * 0.12, scale * 0.14);
                path.lineTo(-scale * 0.32, scale * 0.34);
                path.closePath();
            }, accentColor, Math.max(1, lineWidth * 0.45));
            return;
        case "delete":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.moveTo(-scale * 0.28, -scale * 0.22);
                path.lineTo(scale * 0.28, -scale * 0.22);
                path.moveTo(-scale * 0.2, -scale * 0.34);
                path.lineTo(scale * 0.2, -scale * 0.34);
                path.moveTo(-scale * 0.12, -scale * 0.42);
                path.lineTo(scale * 0.12, -scale * 0.42);
                path.rect(-scale * 0.22, -scale * 0.2, scale * 0.44, scale * 0.56);
                path.moveTo(-scale * 0.08, -scale * 0.08);
                path.lineTo(-scale * 0.08, scale * 0.24);
                path.moveTo(0, -scale * 0.08);
                path.lineTo(0, scale * 0.24);
                path.moveTo(scale * 0.08, -scale * 0.08);
                path.lineTo(scale * 0.08, scale * 0.24);
            });
            return;
        case "connect":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.arc(-scale * 0.14, 0, scale * 0.2, Math.PI * 0.45, Math.PI * 1.55);
                path.moveTo(scale * 0.02, -scale * 0.14);
                path.arc(scale * 0.14, 0, scale * 0.2, Math.PI * 1.45, Math.PI * 0.55, true);
                path.moveTo(-scale * 0.02, 0);
                path.lineTo(scale * 0.02, 0);
            });
            return;
        case "reconnect":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.arc(0, 0, scale * 0.3, Math.PI * 0.2, Math.PI * 1.55);
                path.moveTo(-scale * 0.2, -scale * 0.08);
                path.lineTo(-scale * 0.34, -scale * 0.2);
                path.moveTo(-scale * 0.2, -scale * 0.08);
                path.lineTo(-scale * 0.38, -scale * 0.02);
                path.moveTo(scale * 0.12, scale * 0.08);
                path.lineTo(scale * 0.34, scale * 0.08);
            });
            return;
        case "fit":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.moveTo(-scale * 0.34, -scale * 0.12);
                path.lineTo(-scale * 0.34, -scale * 0.34);
                path.lineTo(-scale * 0.12, -scale * 0.34);
                path.moveTo(scale * 0.12, -scale * 0.34);
                path.lineTo(scale * 0.34, -scale * 0.34);
                path.lineTo(scale * 0.34, -scale * 0.12);
                path.moveTo(scale * 0.34, scale * 0.12);
                path.lineTo(scale * 0.34, scale * 0.34);
                path.lineTo(scale * 0.12, scale * 0.34);
                path.moveTo(-scale * 0.12, scale * 0.34);
                path.lineTo(-scale * 0.34, scale * 0.34);
                path.lineTo(-scale * 0.34, scale * 0.12);
            });
            return;
        case "reset":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.arc(0, 0, scale * 0.28, Math.PI * 0.08, Math.PI * 1.72);
                path.moveTo(-scale * 0.12, -scale * 0.26);
                path.lineTo(-scale * 0.34, -scale * 0.22);
                path.moveTo(-scale * 0.12, -scale * 0.26);
                path.lineTo(-scale * 0.24, -scale * 0.42);
            });
            return;
        case "perspective":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.rect(-scale * 0.18, -scale * 0.22, scale * 0.34, scale * 0.34);
                path.moveTo(-scale * 0.18, -scale * 0.22);
                path.lineTo(-scale * 0.02, -scale * 0.38);
                path.lineTo(scale * 0.32, -scale * 0.38);
                path.lineTo(scale * 0.16, -scale * 0.22);
                path.moveTo(scale * 0.16, -scale * 0.22);
                path.lineTo(scale * 0.32, -scale * 0.38);
                path.lineTo(scale * 0.32, -scale * 0.04);
                path.lineTo(scale * 0.16, scale * 0.12);
            });
            return;
        case "wrench":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.arc(-scale * 0.1, -scale * 0.16, scale * 0.16, Math.PI * 0.15, Math.PI * 1.35);
                path.moveTo(scale * 0.02, -scale * 0.04);
                path.lineTo(scale * 0.28, scale * 0.22);
                path.moveTo(scale * 0.18, scale * 0.14);
                path.lineTo(scale * 0.3, scale * 0.02);
            });
            strokeGlyph(context, centerX, centerY, size, accentColor, Math.max(1, lineWidth * 0.5), (path, scale) => {
                path.moveTo(scale * 0.22, scale * 0.16);
                path.lineTo(scale * 0.32, scale * 0.26);
            });
            return;
        case "maximize":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.moveTo(-scale * 0.04, -scale * 0.12);
                path.lineTo(-scale * 0.32, -scale * 0.12);
                path.lineTo(-scale * 0.32, -scale * 0.4);
                path.moveTo(scale * 0.04, -scale * 0.12);
                path.lineTo(scale * 0.32, -scale * 0.12);
                path.lineTo(scale * 0.32, -scale * 0.4);
                path.moveTo(scale * 0.04, scale * 0.12);
                path.lineTo(scale * 0.32, scale * 0.12);
                path.lineTo(scale * 0.32, scale * 0.4);
                path.moveTo(-scale * 0.04, scale * 0.12);
                path.lineTo(-scale * 0.32, scale * 0.12);
                path.lineTo(-scale * 0.32, scale * 0.4);
            });
            return;
        case "dock":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.rect(-scale * 0.32, -scale * 0.34, scale * 0.64, scale * 0.56);
                path.moveTo(-scale * 0.32, -scale * 0.16);
                path.lineTo(scale * 0.32, -scale * 0.16);
                path.moveTo(0, -scale * 0.46);
                path.lineTo(0, 0);
                path.moveTo(0, 0);
                path.lineTo(-scale * 0.14, -scale * 0.12);
                path.moveTo(0, 0);
                path.lineTo(scale * 0.14, -scale * 0.12);
            });
            return;
        case "inspector":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.rect(-scale * 0.34, -scale * 0.28, scale * 0.68, scale * 0.56);
                path.moveTo(-scale * 0.12, -scale * 0.28);
                path.lineTo(-scale * 0.12, scale * 0.28);
                path.moveTo(-scale * 0.24, -scale * 0.12);
                path.lineTo(-scale * 0.2, -scale * 0.12);
                path.moveTo(scale * 0.02, -scale * 0.12);
                path.lineTo(scale * 0.22, -scale * 0.12);
                path.moveTo(scale * 0.02, scale * 0.04);
                path.lineTo(scale * 0.22, scale * 0.04);
            });
            return;
        case "toolbox":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.rect(-scale * 0.3, -scale * 0.06, scale * 0.6, scale * 0.3);
                path.moveTo(-scale * 0.12, -scale * 0.06);
                path.lineTo(-scale * 0.06, -scale * 0.22);
                path.lineTo(scale * 0.06, -scale * 0.22);
                path.lineTo(scale * 0.12, -scale * 0.06);
                path.moveTo(-scale * 0.3, scale * 0.08);
                path.lineTo(scale * 0.3, scale * 0.08);
                path.moveTo(-scale * 0.12, scale * 0.08);
                path.lineTo(-scale * 0.04, scale * 0.08);
                path.moveTo(scale * 0.04, scale * 0.08);
                path.lineTo(scale * 0.12, scale * 0.08);
            });
            return;
        case "log":
            strokeGlyph(context, centerX, centerY, size, strokeColor, lineWidth, (path, scale) => {
                path.rect(-scale * 0.26, -scale * 0.32, scale * 0.52, scale * 0.64);
                path.moveTo(-scale * 0.14, -scale * 0.14);
                path.lineTo(scale * 0.14, -scale * 0.14);
                path.moveTo(-scale * 0.14, 0);
                path.lineTo(scale * 0.14, 0);
                path.moveTo(-scale * 0.14, scale * 0.14);
                path.lineTo(scale * 0.08, scale * 0.14);
                path.moveTo(scale * 0.08, -scale * 0.32);
                path.lineTo(scale * 0.08, -scale * 0.18);
                path.lineTo(scale * 0.26, -scale * 0.18);
            });
            return;
        default:
            return;
    }
}

function normalizeHostToolbarActions(state) {
    const actions = state.surface?.chrome?.toolbarActions;
    if (!Array.isArray(actions)) {
        return [];
    }

    return actions
        .filter(action => action && action.isVisible !== false && action.id)
        .map(action => ({
            id: action.id,
            label: action.label || "Host action",
            glyph: action.glyph || "",
            visualLabel: action.visualLabel || "",
            iconOnly: action.iconOnly !== false,
            width: clamp(Number.isFinite(action.width) ? action.width : 44, 28, 96),
            tone: action.tone || "neutral",
            active: !!action.isActive,
            toggled: !!action.isToggled
        }));
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

        const visualLabel = options.visualLabel || options.label || "Action";
        const showGlyph = !!options.glyph;
        const iconOnly = !!options.iconOnly;
        const showLabel = !iconOnly || !showGlyph;
        const glyphSize = Math.max(12, Math.min(safeWidth, safeHeight) * (iconOnly ? 0.48 : 0.36) * (options.sizeScale || 1));

        if (options.caption && showLabel) {
            context.font = "600 10px 'Segoe UI Variable Display', 'Segoe UI', sans-serif";
            context.fillStyle = palette.secondaryText;
            context.textAlign = "left";
            context.textBaseline = "top";
            context.fillText(options.caption, 12, 8);
        }

        if (showGlyph) {
            const glyphCenterX = iconOnly
                ? safeWidth / 2
                : 18 + (glyphSize / 2);
            const glyphCenterY = safeHeight / 2;
            drawToolbarGlyph(context, options.glyph, glyphCenterX, glyphCenterY, glyphSize, palette);
        }

        if (!showLabel) {
            return;
        }

        context.font = `${options.emphasis ? "700" : "600"} ${options.compact ? 12 : 13}px 'Segoe UI Variable Display', 'Segoe UI', sans-serif`;
        context.fillStyle = palette.text;
        context.textBaseline = "middle";

        if (showGlyph && !iconOnly) {
            context.textAlign = "left";
            context.fillText(
                visualLabel,
                Math.max(14, 18 + glyphSize + 8),
                options.caption ? (safeHeight / 2) + 6 : safeHeight / 2);
            return;
        }

        context.textAlign = "center";
        context.fillText(visualLabel, safeWidth / 2, options.caption ? (safeHeight / 2) + 6 : safeHeight / 2);
    });
}

function createPanelTexture(width, height, options) {
    return createCanvasTexture(width, height, (context, safeWidth, safeHeight) => {
        if (options.variant === "toolbar") {
            const gradient = context.createLinearGradient(0, 0, 0, safeHeight);
            gradient.addColorStop(0, "rgba(51, 65, 85, 0.7)");
            gradient.addColorStop(1, "rgba(30, 41, 59, 0.78)");
            drawRoundedRect(context, 0.5, 0.5, safeWidth - 1, safeHeight - 1, Math.min(24, safeHeight / 2), gradient, "rgba(148, 163, 184, 0.28)", 1.2);

            context.fillStyle = "rgba(255, 255, 255, 0.06)";
            drawRoundedRect(context, 8, 6, safeWidth - 16, Math.max(10, safeHeight * 0.36), Math.min(18, safeHeight / 2), null, null);
            return;
        }

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
    const isStageMaximized = !!state.surface.uiState?.isStageMaximized;
    const hostToolbarActions = normalizeHostToolbarActions(state);
    return [
        { id: "tool:select", label: "Select", glyph: "cursor", iconOnly: true, width: 44, tone: "accent", active: toolMode === toolModes.select },
        { id: "tool:delete", label: "Delete", glyph: "delete", iconOnly: true, width: 44, tone: "danger", active: toolMode === toolModes.delete },
        { id: "tool:connect", label: "Connect", glyph: "connect", iconOnly: true, width: 44, tone: "positive", active: toolMode === toolModes.connect },
        { id: "tool:reconnect", label: "Reconnect", glyph: "reconnect", iconOnly: true, width: 44, tone: "warning", active: toolMode === toolModes.reconnect },
        { id: "view:fit", label: "Fit view", glyph: "fit", iconOnly: true, width: 44, tone: "neutral" },
        { id: "view:reset", label: "Reset camera", glyph: "reset", iconOnly: true, width: 44, tone: "neutral" },
        { id: "camera:perspective", label: "Perspective", glyph: "perspective", iconOnly: true, width: 44, tone: "neutral", active: cameraViewMode === cameraViewModes.perspective },
        { id: "camera:xy", label: "XY view", visualLabel: "XY", width: 48, tone: "neutral", active: cameraViewMode === cameraViewModes.xy },
        { id: "camera:xz", label: "XZ view", visualLabel: "XZ", width: 48, tone: "neutral", active: cameraViewMode === cameraViewModes.xz },
        { id: "camera:yz", label: "YZ view", visualLabel: "YZ", width: 48, tone: "neutral", active: cameraViewMode === cameraViewModes.yz },
        ...hostToolbarActions,
        { id: "chrome:settings", label: state.chromeState?.settingsOpen ? "Close settings" : "Settings", glyph: "wrench", iconOnly: true, width: 44, tone: "neutral", active: !!state.chromeState?.settingsOpen },
        { id: "chrome:toggle-stage-size", label: isStageMaximized ? "Dock stage" : "Maximize stage", glyph: isStageMaximized ? "dock" : "maximize", iconOnly: true, width: 44, tone: isStageMaximized ? "warning" : "neutral", active: isStageMaximized }
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
        toolbarActions: normalizeHostToolbarActions(state).map(action => ({
            id: action.id,
            label: action.label,
            glyph: action.glyph,
            visualLabel: action.visualLabel,
            iconOnly: action.iconOnly,
            width: action.width,
            tone: action.tone,
            active: action.active,
            toggled: action.toggled
        })),
        isStageMaximized: !!state.surface.uiState?.isStageMaximized,
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
            isStageMaximized: false,
            actions: [],
            contextMenu: null
        };
        this.objects = [];
        this.renderKey = "";
        this.toolbarBounds = null;
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
                label: metadata.actionLabel || options.label || metadata.id || "action",
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
        const buttons = buildToolbarButtons(this.state);
        const baseGap = 8;
        const basePadding = 10;
        const desiredWidth = buttons.reduce((sum, button) => sum + (button.width || 44), 0)
            + (Math.max(0, buttons.length - 1) * baseGap)
            + (basePadding * 2);
        const scale = clamp((this.state.viewport.width - 24) / desiredWidth, 0.56, 1);
        const compact = scale < 0.92 || this.state.viewport.width < 940;
        const gap = Math.max(4, Math.round(baseGap * scale));
        const padding = Math.max(6, Math.round(basePadding * scale));
        const buttonHeight = Math.max(28, Math.round(42 * scale));
        const scaledButtons = buttons.map(button => ({
            ...button,
            computedWidth: Math.max(button.visualLabel ? 28 : 24, Math.round((button.width || 44) * scale))
        }));
        const width = scaledButtons.reduce((sum, button) => sum + button.computedWidth, 0)
            + (Math.max(0, scaledButtons.length - 1) * gap)
            + (padding * 2);
        const height = buttonHeight + (padding * 2);
        const x = Math.max(12, (this.state.viewport.width - width) / 2);
        const y = 12;
        this.toolbarBounds = { x, y, width, height };

        this.addPanel(x, y, width, height, {
            variant: "toolbar"
        });

        let cursorX = x + padding;
        scaledButtons.forEach(button => {
            this.addCard(
                cursorX,
                y + padding,
                button.computedWidth,
                buttonHeight,
                {
                    label: button.label,
                    visualLabel: button.visualLabel,
                    glyph: button.glyph,
                    iconOnly: button.iconOnly,
                    tone: button.tone,
                    active: button.active,
                    toggled: button.toggled,
                    compact,
                    sizeScale: scale
                },
                {
                    id: button.id,
                    actionLabel: button.label,
                    section: "toolbar",
                    z: 0.05
                });
            cursorX += button.computedWidth + gap;
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
        const y = (this.toolbarBounds?.y || 12) + (this.toolbarBounds?.height || 52) + 12;

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
            isStageMaximized: !!this.state.surface.uiState?.isStageMaximized,
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
            isStageMaximized: !!this.state.surface.uiState?.isStageMaximized,
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
