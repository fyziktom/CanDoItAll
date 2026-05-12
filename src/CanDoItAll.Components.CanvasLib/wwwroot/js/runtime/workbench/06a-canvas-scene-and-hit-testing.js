(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 06a-canvas-scene-and-hit-testing.js.'); }
    const workbenchInternals = new Proxy({}, {
        get(_target, property) {
            return shared.workbenchInternals?.[property];
        }
    });
    const { clamp, round, hidePopover, getNodeSize, getVisibleNodes, getProjectedNodes, getNodePosition, getHostPoint, worldToHostPoint } = shared;

    function getCanvasRuntimePrimitives() {
        return window.ZyCanvasPrimitives || null;
    }

    function createFallbackHitRegistry() {
        return {
            items: [],
            clear() {
                this.items = [];
            },
            add(bounds, metadata) {
                this.items.push({
                    bounds: {
                        x: bounds?.x || 0,
                        y: bounds?.y || 0,
                        width: bounds?.width || 0,
                        height: bounds?.height || 0
                    },
                    metadata: metadata || {}
                });
            },
            find(pointX, pointY) {
                for (let index = this.items.length - 1; index >= 0; index -= 1) {
                    const item = this.items[index];
                    const bounds = item.bounds;
                    if (pointX >= bounds.x &&
                        pointX <= bounds.x + bounds.width &&
                        pointY >= bounds.y &&
                        pointY <= bounds.y + bounds.height) {
                        return item.metadata;
                    }
                }

                return null;
            }
        };
    }

    function createCanvasHitRegistry() {
        const primitives = getCanvasRuntimePrimitives();
        return primitives?.HitRegistry
            ? new primitives.HitRegistry()
            : createFallbackHitRegistry();
    }

    function createCanvasSurfaceHost(canvas, resizeTarget) {
        const primitives = getCanvasRuntimePrimitives();
        if (primitives?.CanvasSurface) {
            return new primitives.CanvasSurface({
                canvas,
                resizeTarget
            });
        }

        const context = canvas.getContext("2d");
        return {
            canvas,
            context,
            size: {
                width: Math.max(1, Math.round(canvas.getBoundingClientRect().width || 1)),
                height: Math.max(1, Math.round(canvas.getBoundingClientRect().height || 1))
            },
            measure() {
                const rect = (resizeTarget || canvas.parentElement || canvas).getBoundingClientRect();
                const width = Math.max(1, Math.round(rect.width || 1));
                const height = Math.max(1, Math.round(rect.height || 1));
                const ratio = Math.max(1, Math.min(3, window.devicePixelRatio || 1));
                this.size.width = width;
                this.size.height = height;
                canvas.width = Math.round(width * ratio);
                canvas.height = Math.round(height * ratio);
                canvas.style.width = `${width}px`;
                canvas.style.height = `${height}px`;
                context.setTransform(ratio, 0, 0, ratio, 0, 0);
            },
            clear(fillStyle) {
                context.save();
                context.setTransform(1, 0, 0, 1, 0, 0);
                context.clearRect(0, 0, canvas.width, canvas.height);
                context.restore();
                if (fillStyle) {
                    context.save();
                    context.fillStyle = fillStyle;
                    context.fillRect(0, 0, this.size.width, this.size.height);
                    context.restore();
                }
            },
            destroy() {
            }
        };
    }

    function destroyCanvasSurfaceHost(surface) {
        surface?.destroy?.();
    }

    function hexToRgba(color, alpha) {
        if (typeof color !== "string" || color.trim().length === 0) {
            return `rgba(124, 58, 237, ${alpha})`;
        }

        const normalized = color.trim();
        if (normalized.startsWith("rgba(")) {
            return normalized.replace(/rgba\(([^,]+),([^,]+),([^,]+),([^)]+)\)/i, `rgba($1,$2,$3,${alpha})`);
        }

        if (normalized.startsWith("rgb(")) {
            return normalized.replace(/rgb\(([^,]+),([^,]+),([^)]+)\)/i, `rgba($1,$2,$3,${alpha})`);
        }

        const hex = normalized.replace("#", "");
        if (hex.length !== 3 && hex.length !== 6) {
            return normalized;
        }

        const expanded = hex.length === 3
            ? hex.split("").map(token => `${token}${token}`).join("")
            : hex;
        const red = Number.parseInt(expanded.substring(0, 2), 16);
        const green = Number.parseInt(expanded.substring(2, 4), 16);
        const blue = Number.parseInt(expanded.substring(4, 6), 16);
        if (!Number.isFinite(red) || !Number.isFinite(green) || !Number.isFinite(blue)) {
            return normalized;
        }

        return `rgba(${red}, ${green}, ${blue}, ${alpha})`;
    }

    function parseColorChannels(color) {
        if (typeof color !== "string" || color.trim().length === 0) {
            return { red: 124, green: 58, blue: 237 };
        }

        const normalized = color.trim();
        if (normalized.startsWith("rgba(") || normalized.startsWith("rgb(")) {
            const values = normalized
                .replace(/rgba?\(/i, "")
                .replace(")", "")
                .split(",")
                .map(token => Number.parseFloat(token.trim()));
            if (values.length >= 3 &&
                Number.isFinite(values[0]) &&
                Number.isFinite(values[1]) &&
                Number.isFinite(values[2])) {
                return {
                    red: clamp(Math.round(values[0]), 0, 255),
                    green: clamp(Math.round(values[1]), 0, 255),
                    blue: clamp(Math.round(values[2]), 0, 255)
                };
            }
        }

        const hex = normalized.replace("#", "");
        if (hex.length === 3 || hex.length === 6) {
            const expanded = hex.length === 3
                ? hex.split("").map(token => `${token}${token}`).join("")
                : hex;
            const red = Number.parseInt(expanded.substring(0, 2), 16);
            const green = Number.parseInt(expanded.substring(2, 4), 16);
            const blue = Number.parseInt(expanded.substring(4, 6), 16);
            if (Number.isFinite(red) && Number.isFinite(green) && Number.isFinite(blue)) {
                return { red, green, blue };
            }
        }

        return { red: 124, green: 58, blue: 237 };
    }

    function mixColorChannels(base, target, ratio) {
        const normalizedRatio = clamp(ratio, 0, 1);
        return {
            red: Math.round(base.red + ((target.red - base.red) * normalizedRatio)),
            green: Math.round(base.green + ((target.green - base.green) * normalizedRatio)),
            blue: Math.round(base.blue + ((target.blue - base.blue) * normalizedRatio))
        };
    }

    function rgbaFromChannels(channels, alpha) {
        return `rgba(${channels.red}, ${channels.green}, ${channels.blue}, ${alpha})`;
    }

    function buildAccentPalette(accent, isSelected) {
        const accentChannels = parseColorChannels(accent);
        const whiteMix = ratio => mixColorChannels(accentChannels, { red: 255, green: 255, blue: 255 }, ratio);
        const darkMix = ratio => mixColorChannels(accentChannels, { red: 15, green: 23, blue: 42 }, ratio);

        return {
            surfaceFill: rgbaFromChannels(whiteMix(0.84), 0.98),
            surfaceStroke: rgbaFromChannels(whiteMix(isSelected ? 0.08 : 0.36), 0.92),
            surfaceShadow: rgbaFromChannels(accentChannels, 0.16),
            labelText: rgbaFromChannels(darkMix(0.12), 0.72),
            titleText: rgbaFromChannels(darkMix(0.22), 0.96),
            secondaryText: rgbaFromChannels(darkMix(0.16), 0.84),
            iconFill: rgbaFromChannels(whiteMix(0.94), 0.84),
            iconStroke: rgbaFromChannels(whiteMix(0.6), 0.42),
            iconText: rgbaFromChannels(darkMix(0.12), 0.95),
            subtleFill: rgbaFromChannels(whiteMix(0.97), 0.86),
            subtleStroke: rgbaFromChannels(whiteMix(0.68), 0.44),
            subtleText: rgbaFromChannels(darkMix(0.18), 0.88),
            progressTrack: rgbaFromChannels(accentChannels, 0.22),
            progressText: rgbaFromChannels(darkMix(0.18), 0.88)
        };
    }

    function resolveNodeAccentColor(node) {
        if (typeof node?.accentColor === "string" && node.accentColor.trim().length > 0) {
            return node.accentColor.trim();
        }

        switch ((node?.paletteKey || "").toLowerCase()) {
            case "violet":
                return "#7c3aed";
            case "mint":
                return "#10b981";
            case "sky":
                return "#0ea5e9";
            case "amber":
                return "#f59e0b";
            case "rose":
                return "#e11d48";
            case "success":
                return "#059669";
            case "warning":
            case "warn":
                return "#d97706";
            case "danger":
                return "#dc2626";
            case "info":
                return "#0284c7";
            case "neutral":
                return "#475569";
            default:
                return "#7c3aed";
        }
    }

    function resolveCanvasNodePaletteStyle(node, accent, isSelected) {
        if (node?.isReadOnly) {
            return {
                surfaceFill: "rgba(248, 250, 252, 0.98)",
                surfaceStroke: isSelected ? "rgba(71, 85, 105, 0.9)" : "rgba(148, 163, 184, 0.34)",
                surfaceShadow: "rgba(100, 116, 139, 0.12)",
                labelText: "rgba(71, 85, 105, 0.76)",
                titleText: "rgba(51, 65, 85, 0.94)",
                secondaryText: "rgba(100, 116, 139, 0.92)",
                iconFill: "rgba(241, 245, 249, 0.94)",
                iconStroke: "rgba(148, 163, 184, 0.28)",
                iconText: "rgba(71, 85, 105, 0.96)",
                subtleFill: "rgba(255, 255, 255, 0.86)",
                subtleStroke: "rgba(148, 163, 184, 0.3)",
                subtleText: "rgba(51, 65, 85, 0.9)",
                progressTrack: "rgba(148, 163, 184, 0.28)",
                progressText: "rgba(71, 85, 105, 0.9)"
            };
        }

        const paletteKey = (node?.paletteKey || (node?.family === "root" ? "primary" : "neutral")).toLowerCase();
        const palettes = {
            primary: {
                surfaceFill: "rgba(15, 23, 42, 0.98)",
                surfaceStroke: isSelected ? "rgba(248, 250, 252, 0.94)" : "rgba(51, 65, 85, 0.96)",
                surfaceShadow: "rgba(15, 23, 42, 0.28)",
                labelText: "rgba(191, 219, 254, 0.78)",
                titleText: "rgba(248, 250, 252, 0.98)",
                secondaryText: "rgba(226, 232, 240, 0.84)",
                iconFill: "rgba(255, 255, 255, 0.14)",
                iconStroke: "rgba(255, 255, 255, 0.18)",
                iconText: "rgba(248, 250, 252, 0.96)",
                subtleFill: "rgba(255, 255, 255, 0.14)",
                subtleStroke: "rgba(255, 255, 255, 0.18)",
                subtleText: "rgba(248, 250, 252, 0.94)",
                progressTrack: "rgba(248, 250, 252, 0.24)",
                progressText: "rgba(248, 250, 252, 0.92)"
            },
            secondary: {
                surfaceFill: "rgba(237, 233, 254, 0.98)",
                surfaceStroke: isSelected ? "rgba(124, 58, 237, 0.92)" : "rgba(167, 139, 250, 0.62)",
                surfaceShadow: "rgba(109, 40, 217, 0.16)",
                labelText: "rgba(109, 40, 217, 0.72)",
                titleText: "rgba(88, 28, 135, 0.94)",
                secondaryText: "rgba(107, 33, 168, 0.82)",
                iconFill: "rgba(255, 255, 255, 0.64)",
                iconStroke: "rgba(167, 139, 250, 0.4)",
                iconText: "rgba(109, 40, 217, 0.94)",
                subtleFill: "rgba(255, 255, 255, 0.74)",
                subtleStroke: "rgba(196, 181, 253, 0.44)",
                subtleText: "rgba(88, 28, 135, 0.88)",
                progressTrack: "rgba(139, 92, 246, 0.26)",
                progressText: "rgba(88, 28, 135, 0.88)"
            },
            success: {
                surfaceFill: "rgba(220, 252, 231, 0.98)",
                surfaceStroke: isSelected ? "rgba(22, 163, 74, 0.92)" : "rgba(74, 222, 128, 0.62)",
                surfaceShadow: "rgba(22, 163, 74, 0.14)",
                labelText: "rgba(21, 128, 61, 0.72)",
                titleText: "rgba(20, 83, 45, 0.95)",
                secondaryText: "rgba(21, 128, 61, 0.82)",
                iconFill: "rgba(255, 255, 255, 0.62)",
                iconStroke: "rgba(74, 222, 128, 0.42)",
                iconText: "rgba(21, 128, 61, 0.94)",
                subtleFill: "rgba(255, 255, 255, 0.76)",
                subtleStroke: "rgba(134, 239, 172, 0.46)",
                subtleText: "rgba(20, 83, 45, 0.88)",
                progressTrack: "rgba(22, 163, 74, 0.22)",
                progressText: "rgba(20, 83, 45, 0.88)"
            },
            info: {
                surfaceFill: "rgba(224, 242, 254, 0.98)",
                surfaceStroke: isSelected ? "rgba(2, 132, 199, 0.92)" : "rgba(125, 211, 252, 0.64)",
                surfaceShadow: "rgba(2, 132, 199, 0.15)",
                labelText: "rgba(14, 116, 144, 0.72)",
                titleText: "rgba(12, 74, 110, 0.95)",
                secondaryText: "rgba(14, 116, 144, 0.82)",
                iconFill: "rgba(255, 255, 255, 0.62)",
                iconStroke: "rgba(125, 211, 252, 0.44)",
                iconText: "rgba(2, 132, 199, 0.94)",
                subtleFill: "rgba(255, 255, 255, 0.76)",
                subtleStroke: "rgba(125, 211, 252, 0.46)",
                subtleText: "rgba(12, 74, 110, 0.88)",
                progressTrack: "rgba(2, 132, 199, 0.22)",
                progressText: "rgba(12, 74, 110, 0.88)"
            },
            warning: {
                surfaceFill: "rgba(254, 243, 199, 0.98)",
                surfaceStroke: isSelected ? "rgba(217, 119, 6, 0.92)" : "rgba(251, 191, 36, 0.64)",
                surfaceShadow: "rgba(217, 119, 6, 0.15)",
                labelText: "rgba(180, 83, 9, 0.72)",
                titleText: "rgba(120, 53, 15, 0.95)",
                secondaryText: "rgba(146, 64, 14, 0.82)",
                iconFill: "rgba(255, 255, 255, 0.58)",
                iconStroke: "rgba(251, 191, 36, 0.42)",
                iconText: "rgba(180, 83, 9, 0.94)",
                subtleFill: "rgba(255, 255, 255, 0.76)",
                subtleStroke: "rgba(252, 211, 77, 0.48)",
                subtleText: "rgba(120, 53, 15, 0.88)",
                progressTrack: "rgba(217, 119, 6, 0.22)",
                progressText: "rgba(120, 53, 15, 0.88)"
            },
            danger: {
                surfaceFill: "rgba(254, 226, 226, 0.98)",
                surfaceStroke: isSelected ? "rgba(220, 38, 38, 0.94)" : "rgba(252, 165, 165, 0.7)",
                surfaceShadow: "rgba(220, 38, 38, 0.16)",
                labelText: "rgba(185, 28, 28, 0.72)",
                titleText: "rgba(127, 29, 29, 0.95)",
                secondaryText: "rgba(153, 27, 27, 0.82)",
                iconFill: "rgba(255, 255, 255, 0.6)",
                iconStroke: "rgba(252, 165, 165, 0.46)",
                iconText: "rgba(185, 28, 28, 0.95)",
                subtleFill: "rgba(255, 255, 255, 0.8)",
                subtleStroke: "rgba(252, 165, 165, 0.48)",
                subtleText: "rgba(127, 29, 29, 0.88)",
                progressTrack: "rgba(220, 38, 38, 0.22)",
                progressText: "rgba(127, 29, 29, 0.88)"
            },
            "workflow-decision": {
                surfaceFill: "rgba(240, 253, 250, 0.98)",
                surfaceStroke: isSelected ? "rgba(15, 118, 110, 0.96)" : "rgba(20, 184, 166, 0.64)",
                surfaceShadow: "rgba(15, 118, 110, 0.16)",
                labelText: "rgba(15, 118, 110, 0.78)",
                titleText: "rgba(19, 78, 74, 0.96)",
                secondaryText: "rgba(15, 118, 110, 0.84)",
                iconFill: "rgba(255, 251, 235, 0.86)",
                iconStroke: "rgba(245, 158, 11, 0.46)",
                iconText: "rgba(15, 118, 110, 0.96)",
                subtleFill: "rgba(255, 255, 255, 0.84)",
                subtleStroke: "rgba(20, 184, 166, 0.4)",
                subtleText: "rgba(15, 118, 110, 0.9)",
                progressTrack: "rgba(20, 184, 166, 0.24)",
                progressText: "rgba(19, 78, 74, 0.9)"
            },
            neutral: {
                surfaceFill: "rgba(241, 245, 249, 0.98)",
                surfaceStroke: isSelected ? "rgba(71, 85, 105, 0.92)" : "rgba(148, 163, 184, 0.44)",
                surfaceShadow: "rgba(71, 85, 105, 0.1)",
                labelText: "rgba(71, 85, 105, 0.7)",
                titleText: "rgba(15, 23, 42, 0.94)",
                secondaryText: "rgba(71, 85, 105, 0.84)",
                iconFill: "rgba(255, 255, 255, 0.72)",
                iconStroke: "rgba(148, 163, 184, 0.34)",
                iconText: "rgba(71, 85, 105, 0.94)",
                subtleFill: "rgba(255, 255, 255, 0.82)",
                subtleStroke: "rgba(148, 163, 184, 0.38)",
                subtleText: "rgba(30, 41, 59, 0.88)",
                progressTrack: "rgba(148, 163, 184, 0.28)",
                progressText: "rgba(51, 65, 85, 0.88)"
            }
        };

        return palettes[paletteKey] || buildAccentPalette(accent, isSelected);
    }

    function resolveAnchorRect(anchor) {
        if (!anchor) {
            return null;
        }

        if (typeof anchor.getBoundingClientRect === "function") {
            const rect = anchor.getBoundingClientRect();
            if (Number.isFinite(rect?.left) &&
                Number.isFinite(rect?.top) &&
                Number.isFinite(rect?.width) &&
                Number.isFinite(rect?.height) &&
                Number.isFinite(rect?.right) &&
                Number.isFinite(rect?.bottom)) {
                return rect;
            }

            return null;
        }

        const left = typeof anchor.left === "number" ? anchor.left : anchor.x;
        const top = typeof anchor.top === "number" ? anchor.top : anchor.y;
        if (Number.isFinite(left) &&
            Number.isFinite(top) &&
            Number.isFinite(anchor.width) &&
            Number.isFinite(anchor.height)) {
            return {
                left,
                top,
                width: anchor.width,
                height: anchor.height,
                right: left + anchor.width,
                bottom: top + anchor.height
            };
        }

        return null;
    }

    function buildRect(left, top, width, height) {
        return {
            left,
            top,
            width,
            height,
            right: left + width,
            bottom: top + height
        };
    }

    function boundsToHitRect(bounds) {
        return {
            x: round(bounds.left),
            y: round(bounds.top),
            width: round(bounds.width),
            height: round(bounds.height)
        };
    }

    function projectSceneBounds(state, bounds) {
        const topLeft = worldToHostPoint(state, { x: bounds.left, y: bounds.top });
        const width = bounds.width * state.ui.zoom;
        const height = bounds.height * state.ui.zoom;
        return buildRect(topLeft.x, topLeft.y, width, height);
    }

    function getNodeSceneBounds(state, node) {
        const position = getNodePosition(state, node);
        const size = getNodeSize(state, node);
        return {
            left: position.x - (size.width / 2),
            top: position.y - (size.height / 2),
            width: size.width,
            height: size.height,
            centerX: position.x,
            centerY: position.y
        };
    }

    function clearSceneHotZones(state) {
        state.sceneHitRegistry?.clear?.();
        state.sceneHotZones = [];
    }

    function registerSceneHotZone(state, bounds, metadata) {
        if (!bounds || bounds.width <= 0 || bounds.height <= 0) {
            return;
        }

        const hitRect = boundsToHitRect(bounds);
        const entry = {
            bounds: hitRect,
            metadata: {
                ...metadata,
                bounds: hitRect
            }
        };
        state.sceneHotZones.push(entry);
        state.sceneHitRegistry?.add?.(hitRect, entry.metadata);
    }

    function getSceneHitAtPoint(state, pointX, pointY) {
        return state.sceneHitRegistry?.find?.(pointX, pointY) || null;
    }

    function getSceneHitAtEvent(state, event) {
        const point = getHostPoint(state, event.clientX, event.clientY);
        return getSceneHitAtPoint(state, point.x, point.y);
    }

    function resolveHitNode(state, hitTarget) {
        return hitTarget?.nodeId
            ? state.lookups.byId.get(hitTarget.nodeId) || null
            : null;
    }

    function clearScenePopoverHover(state) {
        state.hoveredAnnotationKey = "";
        hidePopover(state);
    }

    function areAnchorRectsEquivalent(left, right) {
        if (!left || !right) {
            return false;
        }

        return round(left.left) === round(right.left) &&
            round(left.top) === round(right.top) &&
            round(left.width) === round(right.width) &&
            round(left.height) === round(right.height);
    }

    function shouldRefreshScenePopover(state, annotationKey, anchorRect) {
        if (state.hoveredAnnotationKey !== annotationKey) {
            return true;
        }

        if (!state?.popover || !state.popover.isConnected || state.popover.style.display === "none") {
            return true;
        }

        return !areAnchorRectsEquivalent(resolveAnchorRect(state.popoverAnchor), anchorRect);
    }

    function showScenePopover(state, anchorRect, annotation) {
        const showPopoverFn = shared.showPopover;
        if (typeof showPopoverFn !== "function") {
            return false;
        }

        return showPopoverFn(state, anchorRect, annotation) !== false;
    }

    function renderConnectorAnchors(state, visibleNodes) {
        const renderConnectorAnchorOverlayFn =
            workbenchInternals.overlayRenderer?.renderConnectorAnchorOverlay ||
            shared.renderConnectorAnchorOverlay ||
            shared.legacyRenderConnectorAnchorOverlay;
        if (typeof renderConnectorAnchorOverlayFn === "function") {
            renderConnectorAnchorOverlayFn(state, visibleNodes);
        }
    }

    function syncSceneHoverState(state, event) {
        if (!state?.host?.isConnected || !state.lookups?.byId || !state.sceneHitRegistry) {
            clearScenePopoverHover(state);
            return;
        }

        const hitTarget = getSceneHitAtEvent(state, event);
        const nextNodeId = hitTarget?.nodeId || null;
        if ((state.hoveredNodeId || null) !== nextNodeId) {
            state.hoveredNodeId = nextNodeId;
            const visibleNodes = getVisibleNodes(state);
            renderConnectorAnchors(state, Array.isArray(visibleNodes) ? getProjectedNodes(state, visibleNodes) : []);
        }

        if (hitTarget?.type === "annotation" && hitTarget.annotation) {
            const annotationKey = `${hitTarget.nodeId}:${hitTarget.annotation.id || hitTarget.annotation.kind || hitTarget.annotation.label || hitTarget.annotationIndex || 0}`;
            const anchorRect = resolveAnchorRect(hitTarget.bounds);
            if (!anchorRect) {
                clearScenePopoverHover(state);
                return;
            }

            if (shouldRefreshScenePopover(state, annotationKey, anchorRect)) {
                if (!showScenePopover(state, anchorRect, hitTarget.annotation)) {
                    clearScenePopoverHover(state);
                    return;
                }

                state.hoveredAnnotationKey = annotationKey;
            }
            return;
        }

        clearScenePopoverHover(state);
    }

    Object.assign(shared, {
        getCanvasRuntimePrimitives,
        createFallbackHitRegistry,
        createCanvasHitRegistry,
        createCanvasSurfaceHost,
        destroyCanvasSurfaceHost,
        hexToRgba,
        resolveNodeAccentColor,
        resolveCanvasNodePaletteStyle,
        resolveAnchorRect,
        buildRect,
        boundsToHitRect,
        projectSceneBounds,
        getNodeSceneBounds,
        clearSceneHotZones,
        registerSceneHotZone,
        getSceneHitAtPoint,
        getSceneHitAtEvent,
        resolveHitNode,
        clearScenePopoverHover,
        syncSceneHoverState
    });
})();
