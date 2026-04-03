(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 06-canvas-renderers.js.'); }
    const workbenchInternals = new Proxy({}, {
        get(_target, property) {
            return shared.workbenchInternals?.[property];
        }
    });
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition, getLinkAnchorPoint, resolveCollapseAnchorInfo, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, createMarkerBadge, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, copySelectionToClipboard, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint, hitTestNode, hitTestFrameHandle, hitTestProgressBadge, isOverlayTarget, applyFullTextTooltip, reconcileSelection, applySelection, selectSingleNode, publishSelection, clearViewportStateCommit, createSerializedStateSnapshot, invokeStateChanged, publishState, publishStateNow, scheduleViewportStateCommit, publishNodesMoved, setSelection, toggleSelection, toggleCollapse, clearContextMenu, closeComposer, ensureHostFocus, deferHostFocus, resolveComposerAnchor, layoutComposer, render, getContextActions, isCreateAction, buildCreateRequest, resolveMenuLabel, getMenuScale, isCompactHiveLayout, resolveMenuActionVariant, getActionMetrics, applyProgressPresetTone, fitContextMenuLabel, resolveActionGlyph, createMenuActionIcon, resolveMenuActionAriaLabel, getRadialOffsets, buildCompactHiveCoordinates, getCompactHiveOffsets, resolveContextMenuOffsets, resolveContextMenuSafeTop, getContextMenuLayerBounds, clampLayerBoundsToHost, positionContextMenu, getContextMenuOrbitRadius, getContextMenuLocalPoint, isPointInContextMenuLayer, closeContextMenuLayersFrom, syncContextMenuLayers, resolveSubmenuOrigin, ensureSubmenuLoadingIndicator, clearSubmenuLoadingIndicator, cancelPendingContextSubmenu, scheduleContextSubmenuOpen, clampLayerOriginToHost, getToolboxPanelSize, getToolboxPanelBounds, clampToolboxPanelOriginToHost, resolveToolboxPanelOrigin, createContextMenuLayer, shiftContextMenuLayerOrigin, nudgeContextMenuLayerIntoVisibleHost, resolveQuickCreateSourceNode, submitCreateRequest, submitNodeEdit, readFileAsUpload, commitComposer, decorateComposerShell, createComposerWizard, createComposerSection, updateComposerFileState, openCreateComposer, openInlineNoteComposer, buildChildNotePlacement, buildSiblingNotePlacement, openKeyboardNoteComposer, openExistingNoteEditor, executeContextAction, openContextSubmenu, openContextSubmenuByActionId, renderContextMenuLayer, renderToolboxPreview, renderToolboxPanelLayer, showContextMenu, legacyOpenNodeMetadataMenu, startPan, isMarqueeModifierPressed, startMarquee, ensureSelectedForDrag, startDragForNodeIds, startDrag, startFrameDrag, updateMarquee, legacyApplyMarqueeSelection, updateDrag, updatePan, isNodeVisibleInViewport, centerNodeElementInViewport, ensureNodeVisible, legacyResize, findContainingBlockOverride, suspendContainingBlock, restoreContainingBlock, setMaximized, fitView, focusNode, normalizeWheelDelta, applyWheelZoom, setZoomPercent, setMenuScalePercent, toggleHelp, isManualDoubleActivation, handleNodeDoubleActivation, legacyAttachEvents, hydrateState, refresh } = shared;
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
            return anchor.getBoundingClientRect();
        }

        const left = typeof anchor.left === "number" ? anchor.left : anchor.x;
        const top = typeof anchor.top === "number" ? anchor.top : anchor.y;
        if (typeof left === "number" &&
            typeof top === "number" &&
            typeof anchor.width === "number" &&
            typeof anchor.height === "number") {
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
        if (state.hoveredAnnotationKey) {
            state.hoveredAnnotationKey = "";
            hidePopover(state);
        }
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
        const hitTarget = getSceneHitAtEvent(state, event);
        const nextNodeId = hitTarget?.nodeId || null;
        if ((state.hoveredNodeId || null) !== nextNodeId) {
            state.hoveredNodeId = nextNodeId;
            renderConnectorAnchors(state, getProjectedNodes(state, getVisibleNodes(state)));
        }

        if (hitTarget?.type === "annotation" && hitTarget.annotation) {
            const annotationKey = `${hitTarget.nodeId}:${hitTarget.annotation.id || hitTarget.annotation.kind || hitTarget.annotation.label || hitTarget.annotationIndex || 0}`;
            if (state.hoveredAnnotationKey !== annotationKey) {
                state.hoveredAnnotationKey = annotationKey;
                showPopover(state, resolveAnchorRect(hitTarget.bounds), hitTarget.annotation);
            }
            return;
        }

        clearScenePopoverHover(state);
    }

    function resolveCanvasNodeDetailMode(state, projectedNodeCount) {
        if ((state.ui.zoom || 1) <= 0.3 || projectedNodeCount >= 120) {
            return "micro";
        }

        if ((state.ui.zoom || 1) <= 0.55 || projectedNodeCount >= 70) {
            return "compact";
        }

        return "full";
    }

    function setCanvasFont(context, weight, sizePx) {
        context.font = `${weight} ${Math.max(8, round(sizePx))}px "DM Sans", "Segoe UI", sans-serif`;
    }

    function drawCanvasTextLines(context, lines, x, startY, lineHeight, fillStyle) {
        if (!Array.isArray(lines) || lines.length === 0) {
            return;
        }

        context.fillStyle = fillStyle;
        for (let index = 0; index < lines.length; index += 1) {
            context.fillText(lines[index], x, startY + (index * lineHeight));
        }
    }

    function drawRoundedPanel(context, bounds, radius, fill, stroke, lineWidth, shadowColor) {
        const primitives = getCanvasRuntimePrimitives();
        if (primitives?.fillRoundedPanel) {
            primitives.fillRoundedPanel(context, {
                x: bounds.left,
                y: bounds.top,
                width: bounds.width,
                height: bounds.height,
                radius,
                fill,
                stroke,
                lineWidth,
                shadowColor,
                shadowBlur: shadowColor ? 20 : 0,
                shadowOffsetY: shadowColor ? 8 : 0
            });
            return;
        }

        context.save();
        context.beginPath();
        context.roundRect(bounds.left, bounds.top, bounds.width, bounds.height, radius);
        context.fillStyle = fill;
        context.fill();
        if (stroke) {
            context.lineWidth = lineWidth;
            context.strokeStyle = stroke;
            context.stroke();
        }
        context.restore();
    }

    function requestSceneImage(state, sourceUrl) {
        if (!sourceUrl) {
            return null;
        }

        state.mediaImageCache = state.mediaImageCache || new Map();
        let entry = state.mediaImageCache.get(sourceUrl) || null;
        if (entry) {
            return entry;
        }

        entry = {
            image: null,
            isLoaded: false,
            isLoading: true,
            hasError: false
        };
        state.mediaImageCache.set(sourceUrl, entry);
        const image = new Image();
        image.decoding = "async";
        image.onload = () => {
            entry.image = image;
            entry.isLoaded = true;
            entry.isLoading = false;
            render(state);
        };
        image.onerror = () => {
            entry.isLoading = false;
            entry.hasError = true;
        };
        image.src = sourceUrl;
        return entry;
    }

    function buildCanvasSnapshotBounds(bounds, node, extra) {
        return {
            id: node?.id || extra?.id || "",
            left: round(bounds.left),
            top: round(bounds.top),
            width: round(bounds.width),
            height: round(bounds.height),
            right: round(bounds.right),
            bottom: round(bounds.bottom),
            title: node?.title || "",
            subtitle: node?.subtitle || "",
            inlineText: node?.inlineText || "",
            selected: !!extra?.selected,
            collapsed: !!extra?.collapsed,
            isInlineTextNode: !!node?.isInlineTextNode,
            markerText: extra?.markerText || "",
            priorityText: extra?.priorityText || "",
            progressTitle: extra?.progressTitle || "",
            hasPathButton: !!extra?.hasPathButton,
            pathTitle: extra?.pathTitle || "",
            pathDisplayText: extra?.pathDisplayText || "",
            pathPromotedText: extra?.pathPromotedText || "",
            mediaKind: node?.mediaKind || "",
            mediaPreviewUrl: node?.mediaPreviewUrl || ""
        };
    }

    function reconcileRetainedLayer(retained, nextEntries, metrics, metricKey, signatureSelector) {
        let changed = false;
        for (const key of [...retained.keys()]) {
            if (!nextEntries.has(key)) {
                retained.delete(key);
                changed = true;
            }
        }

        for (const [key, entry] of nextEntries) {
            const previous = retained.get(key) || null;
            const previousSignature = previous ? signatureSelector(previous) : null;
            const nextSignature = signatureSelector(entry);
            if (previousSignature !== nextSignature) {
                changed = true;
            }

            retained.set(key, entry);
        }

        if (changed) {
            incrementMetric(metrics, metricKey);
        }
    }

    function drawCanvasFrame(context, state, frame, hostBounds, memberCount, frameId) {
        const tone = (frame?.tone || "accent").toLowerCase();
        let stroke = "rgba(124, 58, 237, 0.72)";
        let fill = "rgba(124, 58, 237, 0.08)";
        if (tone === "success") {
            stroke = "rgba(5, 150, 105, 0.72)";
            fill = "rgba(5, 150, 105, 0.08)";
        }
        else if (tone === "warning" || tone === "warn") {
            stroke = "rgba(217, 119, 6, 0.72)";
            fill = "rgba(217, 119, 6, 0.08)";
        }
        else if (tone === "danger") {
            stroke = "rgba(220, 38, 38, 0.72)";
            fill = "rgba(220, 38, 38, 0.08)";
        }

        drawRoundedPanel(
            context,
            hostBounds,
            Math.max(16, 18 * state.ui.zoom),
            fill,
            stroke,
            Math.max(1.2, 2 * state.ui.zoom),
            "");

        const labelHeight = Math.max(20, 28 * state.ui.zoom);
        const labelWidth = Math.min(hostBounds.width - 24, Math.max(92, (frame?.label || "Group border").length * 8 * state.ui.zoom + 52));
        const labelBounds = buildRect(hostBounds.left + 18, hostBounds.top - (labelHeight / 2), labelWidth, labelHeight);
        drawRoundedPanel(
            context,
            labelBounds,
            Math.max(10, 14 * state.ui.zoom),
            "rgba(255, 255, 255, 0.96)",
            hexToRgba(stroke, 0.18),
            1,
            "");
        context.save();
        setCanvasFont(context, 700, Math.max(9, 11.5 * state.ui.zoom));
        context.fillStyle = "rgba(15, 23, 42, 0.74)";
        context.fillText(frame?.label || "Group border", labelBounds.left + 12, labelBounds.top + Math.max(14, 18 * state.ui.zoom));
        setCanvasFont(context, 700, Math.max(9, 10.5 * state.ui.zoom));
        context.fillStyle = "rgba(71, 85, 105, 0.84)";
        context.textAlign = "right";
        context.fillText(`${memberCount}`, labelBounds.right - 12, labelBounds.top + Math.max(14, 18 * state.ui.zoom));
        context.restore();

        const handleSize = Math.max(10, 14 * state.ui.zoom);
        const handleInsetX = hostBounds.width / 2;
        const handleInsetY = hostBounds.height / 2;
        const handles = [
            buildRect(hostBounds.left + handleInsetX - (handleSize / 2), hostBounds.top - (handleSize / 2), handleSize, handleSize),
            buildRect(hostBounds.right - (handleSize / 2), hostBounds.top + handleInsetY - (handleSize / 2), handleSize, handleSize),
            buildRect(hostBounds.left + handleInsetX - (handleSize / 2), hostBounds.bottom - (handleSize / 2), handleSize, handleSize),
            buildRect(hostBounds.left - (handleSize / 2), hostBounds.top + handleInsetY - (handleSize / 2), handleSize, handleSize)
        ];
        for (const bounds of handles) {
            drawRoundedPanel(
                context,
                bounds,
                handleSize / 2,
                "rgba(255, 255, 255, 0.96)",
                hexToRgba(stroke, 0.8),
                1,
                "");
            registerSceneHotZone(state, bounds, {
                type: "frame-handle",
                frameId
            });
        }

        registerSceneHotZone(state, labelBounds, {
            type: "frame-handle",
            frameId
        });
        return labelBounds;
    }

    function renderGroupFrames(state, visibleNodes) {
        const surface = state.frameSurface;
        if (!surface) {
            return;
        }

        surface.clear();
        state.renderedFrames = new Map();
        const visibleLookup = new Map((visibleNodes || []).map(node => [node.id, node]));
        const nextEntries = new Map();
        let renderedFrameCount = 0;

        for (const [index, frame] of (state.ui.groupFrames || []).entries()) {
            const memberNodes = getExpandedFrameNodeIds(state, frame)
                .map(nodeId => visibleLookup.get(nodeId))
                .filter(Boolean);
            if (!memberNodes.length) {
                continue;
            }

            const sceneBounds = getFrameBounds(state, memberNodes);
            if (!sceneBounds) {
                continue;
            }

            const frameId = getFrameRetainedKey(frame, index);
            const hostBounds = projectSceneBounds(state, {
                left: sceneBounds.minX,
                top: sceneBounds.minY,
                width: sceneBounds.width,
                height: sceneBounds.height
            });
            const labelBounds = drawCanvasFrame(surface.context, state, frame, hostBounds, memberNodes.length, frameId);
            state.renderedFrames.set(frameId, {
                frame,
                nodeIds: memberNodes.map(node => node.id),
                sceneBounds,
                hostBounds,
                labelBounds
            });
            nextEntries.set(frameId, {
                signature: JSON.stringify({
                    frame,
                    nodeIds: memberNodes.map(node => node.id)
                })
            });
            renderedFrameCount += 1;
        }

        reconcileRetainedLayer(
            state.retainedFrameElements,
            nextEntries,
            state.metrics,
            "frameLayerRebuildCount",
            entry => entry.signature);

        if (state.metrics) {
            state.metrics.lastRenderedFrameCount = renderedFrameCount;
        }
    }

    function sampleCubicBezierPoint(startPoint, controlPoint1, controlPoint2, endPoint, t) {
        const inverse = 1 - t;
        const inverseSquared = inverse * inverse;
        const inverseCubed = inverseSquared * inverse;
        const tSquared = t * t;
        const tCubed = tSquared * t;
        return {
            x: (inverseCubed * startPoint.x) +
                (3 * inverseSquared * t * controlPoint1.x) +
                (3 * inverse * tSquared * controlPoint2.x) +
                (tCubed * endPoint.x),
            y: (inverseCubed * startPoint.y) +
                (3 * inverseSquared * t * controlPoint1.y) +
                (3 * inverse * tSquared * controlPoint2.y) +
                (tCubed * endPoint.y)
        };
    }

    function buildCanvasLinkGeometry(startPoint, endPoint) {
        const controlOffset = Math.max(56, Math.abs(endPoint.x - startPoint.x) * 0.38);
        const sourceSide = endPoint.x >= startPoint.x ? 1 : -1;
        const targetSide = sourceSide === 1 ? -1 : 1;
        const controlPoint1 = {
            x: startPoint.x + (controlOffset * sourceSide),
            y: startPoint.y
        };
        const controlPoint2 = {
            x: endPoint.x + (controlOffset * targetSide),
            y: endPoint.y
        };
        const midPoint = sampleCubicBezierPoint(startPoint, controlPoint1, controlPoint2, endPoint, 0.5);
        const padding = 12;
        const minX = Math.min(startPoint.x, controlPoint1.x, controlPoint2.x, endPoint.x) - padding;
        const minY = Math.min(startPoint.y, controlPoint1.y, controlPoint2.y, endPoint.y) - padding;
        const maxX = Math.max(startPoint.x, controlPoint1.x, controlPoint2.x, endPoint.x) + padding;
        const maxY = Math.max(startPoint.y, controlPoint1.y, controlPoint2.y, endPoint.y) + padding;
        return {
            startPoint: {
                x: round(startPoint.x),
                y: round(startPoint.y)
            },
            endPoint: {
                x: round(endPoint.x),
                y: round(endPoint.y)
            },
            controlPoint1: {
                x: round(controlPoint1.x),
                y: round(controlPoint1.y)
            },
            controlPoint2: {
                x: round(controlPoint2.x),
                y: round(controlPoint2.y)
            },
            midPoint: {
                x: round(midPoint.x),
                y: round(midPoint.y)
            },
            bounds: {
                left: round(minX),
                top: round(minY),
                width: round(maxX - minX),
                height: round(maxY - minY),
                right: round(maxX),
                bottom: round(maxY)
            }
        };
    }

    function resolveCanvasLinkStyle(link, options) {
        if (options?.isHovered) {
            return {
                stroke: "rgba(239, 68, 68, 0.94)",
                arrowFill: "rgba(220, 38, 38, 0.96)",
                lineWidth: 4,
                lineDash: []
            };
        }

        if (options?.isPreview) {
            return {
                stroke: "rgba(124, 58, 237, 0.92)",
                arrowFill: "rgba(109, 40, 217, 0.96)",
                lineWidth: 3,
                lineDash: [10, 6]
            };
        }

        if (isDependencyLink(link)) {
            return {
                stroke: "rgba(37, 99, 235, 0.94)",
                arrowFill: "rgba(29, 78, 216, 0.98)",
                lineWidth: 3.35,
                lineDash: []
            };
        }

        if (link?.isUserAuthored) {
            return {
                stroke: "rgba(14, 165, 233, 0.82)",
                arrowFill: "rgba(14, 165, 233, 0.88)",
                lineWidth: 3,
                lineDash: [12, 8]
            };
        }

        return {
            stroke: "rgba(100, 116, 139, 0.44)",
            arrowFill: "rgba(100, 116, 139, 0.58)",
            lineWidth: 2,
            lineDash: []
        };
    }

    function isDependencyLink(link) {
        const kind = (link?.kind || "").toLowerCase();
        return kind === "dependson" || kind === "dependency";
    }

    function sampleBezierPoint(startPoint, controlPoint1, controlPoint2, endPoint, t) {
        const inverse = 1 - t;
        const inverseSquared = inverse * inverse;
        const inverseCubed = inverseSquared * inverse;
        const tSquared = t * t;
        const tCubed = tSquared * t;

        return {
            x: (inverseCubed * startPoint.x) +
                (3 * inverseSquared * t * controlPoint1.x) +
                (3 * inverse * tSquared * controlPoint2.x) +
                (tCubed * endPoint.x),
            y: (inverseCubed * startPoint.y) +
                (3 * inverseSquared * t * controlPoint1.y) +
                (3 * inverse * tSquared * controlPoint2.y) +
                (tCubed * endPoint.y)
        };
    }

    function sampleBezierTangent(startPoint, controlPoint1, controlPoint2, endPoint, t) {
        const inverse = 1 - t;
        const inverseSquared = inverse * inverse;
        const tSquared = t * t;

        return {
            x: (3 * inverseSquared * (controlPoint1.x - startPoint.x)) +
                (6 * inverse * t * (controlPoint2.x - controlPoint1.x)) +
                (3 * tSquared * (endPoint.x - controlPoint2.x)),
            y: (3 * inverseSquared * (controlPoint1.y - startPoint.y)) +
                (6 * inverse * t * (controlPoint2.y - controlPoint1.y)) +
                (3 * tSquared * (endPoint.y - controlPoint2.y))
        };
    }

    function drawCanvasArrowHead(context, point, angle, fillStyle, length, halfWidth) {
        context.save();
        context.translate(point.x, point.y);
        context.rotate(angle);
        context.beginPath();
        context.moveTo(0, 0);
        context.lineTo(-length, halfWidth);
        context.lineTo(-length, -halfWidth);
        context.closePath();
        context.fillStyle = fillStyle;
        context.fill();
        context.restore();
    }

    function drawCanvasLink(context, link, startPoint, endPoint, options) {
        const geometry = buildCanvasLinkGeometry(startPoint, endPoint);
        const style = resolveCanvasLinkStyle(link, options);
        context.save();
        context.beginPath();
        context.moveTo(geometry.startPoint.x, geometry.startPoint.y);
        context.bezierCurveTo(
            geometry.controlPoint1.x,
            geometry.controlPoint1.y,
            geometry.controlPoint2.x,
            geometry.controlPoint2.y,
            geometry.endPoint.x,
            geometry.endPoint.y);
        context.lineWidth = style.lineWidth;
        context.lineCap = "round";
        context.strokeStyle = style.stroke;
        if (style.lineDash.length) {
            context.setLineDash(style.lineDash);
        }
        context.stroke();
        context.restore();

        if (!shouldRenderArrow(link) && !options?.isPreview && !options?.forceArrow) {
            return geometry;
        }

        const angle = Math.atan2(
            geometry.endPoint.y - geometry.controlPoint2.y,
            geometry.endPoint.x - geometry.controlPoint2.x);
        const dependencyLink = isDependencyLink(link);
        const arrowLength = options?.isPreview ? 14 : dependencyLink ? 12 : 10;
        const arrowHalfWidth = options?.isPreview ? 5 : dependencyLink ? 4.75 : 4;
        drawCanvasArrowHead(context, geometry.endPoint, angle, style.arrowFill, arrowLength, arrowHalfWidth);

        if (dependencyLink && !options?.isPreview) {
            const midT = 0.58;
            const midPoint = sampleBezierPoint(
                geometry.startPoint,
                geometry.controlPoint1,
                geometry.controlPoint2,
                geometry.endPoint,
                midT);
            const tangent = sampleBezierTangent(
                geometry.startPoint,
                geometry.controlPoint1,
                geometry.controlPoint2,
                geometry.endPoint,
                midT);
            const midAngle = Math.atan2(tangent.y, tangent.x);
            drawCanvasArrowHead(context, midPoint, midAngle, style.arrowFill, 10, 4);
        }

        return geometry;
    }

    function renderLinks(state, visibleNodes) {
        const surface = state.linkSurface;
        if (!surface) {
            return;
        }

        surface.clear();
        state.renderedLinks = [];
        state.previewLink = null;
        const visible = new Set((visibleNodes || []).map(node => node.id));
        const nextEntries = new Map();
        let renderedLinkCount = 0;

        for (const [index, link] of state.surface.links.entries()) {
            if (!visible.has(link.sourceId) || !visible.has(link.targetId)) {
                continue;
            }

            const source = state.lookups.byId.get(link.sourceId);
            const target = state.lookups.byId.get(link.targetId);
            if (!source || !target) {
                continue;
            }

            const sourcePosition = getNodePosition(state, source);
            const targetPosition = getNodePosition(state, target);
            const sourceSide = targetPosition.x >= sourcePosition.x ? "right" : "left";
            const targetSide = sourceSide === "right" ? "left" : "right";
            const sourceAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, source, sourceSide));
            const targetAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, target, targetSide));
            const retainedKey = getLinkRetainedKey(link, index);
            const geometry = drawCanvasLink(surface.context, link, sourceAnchor, targetAnchor, {
                key: retainedKey,
                isHovered: state.surface?.mode === "delete" && state.hoveredDeleteLinkKey === retainedKey
            });
            nextEntries.set(retainedKey, {
                signature: JSON.stringify({
                    sourceId: link.sourceId,
                    targetId: link.targetId,
                    kind: link.kind || "",
                    flow: !!link.isUserAuthored,
                    hovered: state.hoveredDeleteLinkKey === retainedKey
                })
            });
            state.renderedLinks.push({
                key: retainedKey,
                sourceId: link.sourceId,
                targetId: link.targetId,
                kind: link.kind || "",
                isUserAuthored: !!link.isUserAuthored,
                startPoint: geometry.startPoint,
                endPoint: geometry.endPoint,
                controlPoint1: geometry.controlPoint1,
                controlPoint2: geometry.controlPoint2,
                midPoint: geometry.midPoint,
                bounds: geometry.bounds
            });
            renderedLinkCount += 1;
        }

        const dependencySourceId = state.surface?.dependencySourceId || "";
        if (state.surface?.mode === "dependency" &&
            dependencySourceId &&
            visible.has(dependencySourceId) &&
            state.lookups.byId.has(dependencySourceId)) {
            const previewSource = state.lookups.byId.get(dependencySourceId);
            const hoveredTargetId = state.hoveredNodeId &&
                state.hoveredNodeId !== dependencySourceId &&
                visible.has(state.hoveredNodeId)
                ? state.hoveredNodeId
                : null;
            const previewLink = {
                sourceId: dependencySourceId,
                targetId: hoveredTargetId || "",
                kind: "DependsOn",
                isUserAuthored: true
            };

            if (hoveredTargetId && state.lookups.byId.has(hoveredTargetId)) {
                const previewTarget = state.lookups.byId.get(hoveredTargetId);
                const sourcePosition = getNodePosition(state, previewSource);
                const targetPosition = getNodePosition(state, previewTarget);
                const sourceSide = targetPosition.x >= sourcePosition.x ? "right" : "left";
                const targetSide = sourceSide === "right" ? "left" : "right";
                const sourceAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, previewSource, sourceSide));
                const targetAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, previewTarget, targetSide));
                const previewGeometry = drawCanvasLink(surface.context, previewLink, sourceAnchor, targetAnchor, {
                    isPreview: true,
                    forceArrow: true
                });
                state.previewLink = {
                    sourceId: dependencySourceId,
                    targetId: hoveredTargetId,
                    startPoint: previewGeometry.startPoint,
                    endPoint: previewGeometry.endPoint,
                    controlPoint1: previewGeometry.controlPoint1,
                    controlPoint2: previewGeometry.controlPoint2,
                    midPoint: previewGeometry.midPoint,
                    bounds: previewGeometry.bounds
                };
            }
            else if (state.pointerHostPoint) {
                const previewSourceBounds = projectSceneBounds(state, getNodeSceneBounds(state, previewSource));
                const sourceSide = state.pointerHostPoint.x >= (previewSourceBounds.left + (previewSourceBounds.width / 2))
                    ? "right"
                    : "left";
                const sourceAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, previewSource, sourceSide));
                const previewGeometry = drawCanvasLink(surface.context, previewLink, sourceAnchor, state.pointerHostPoint, {
                    isPreview: true,
                    forceArrow: true
                });
                state.previewLink = {
                    sourceId: dependencySourceId,
                    targetId: null,
                    startPoint: previewGeometry.startPoint,
                    endPoint: previewGeometry.endPoint,
                    controlPoint1: previewGeometry.controlPoint1,
                    controlPoint2: previewGeometry.controlPoint2,
                    midPoint: previewGeometry.midPoint,
                    bounds: previewGeometry.bounds
                };
            }
        }

        reconcileRetainedLayer(
            state.retainedLinkElements,
            nextEntries,
            state.metrics,
            "linkLayerRebuildCount",
            entry => entry.signature);

        if (state.metrics) {
            state.metrics.lastRenderedLinkCount = renderedLinkCount;
        }
    }

    function drawCanvasBadgePill(context, bounds, text, fill, stroke, textColor, fontSize) {
        drawRoundedPanel(context, bounds, Math.max(8, bounds.height / 2), fill, stroke, 1, "");
        context.save();
        setCanvasFont(context, 700, fontSize);
        context.fillStyle = textColor;
        context.textAlign = "center";
        context.fillText(text, bounds.left + (bounds.width / 2), bounds.top + (bounds.height / 2) + (fontSize * 0.34));
        context.restore();
    }

    function drawCanvasProgressBadge(context, state, bounds, node, paletteStyle) {
        const display = resolveProgressDisplay(node?.progressMode, node?.progressPercent);
        const centerX = bounds.left + (bounds.width / 2);
        const centerY = bounds.top + (bounds.height / 2);
        const radius = Math.max(6, bounds.width / 2);
        context.save();
        context.lineWidth = Math.max(2, 2.2 * state.ui.zoom);
        context.strokeStyle = paletteStyle?.progressTrack || "rgba(148, 163, 184, 0.28)";
        context.beginPath();
        context.arc(centerX, centerY, radius - context.lineWidth, 0, Math.PI * 2);
        context.stroke();
        if (display.mode !== "na") {
            context.strokeStyle = display.mode === "complete"
                ? "rgba(5, 150, 105, 0.92)"
                : "rgba(124, 58, 237, 0.92)";
            context.beginPath();
            context.arc(centerX, centerY, radius - context.lineWidth, -Math.PI / 2, -Math.PI / 2 + ((display.angle / 360) * Math.PI * 2));
            context.stroke();
        }

        setCanvasFont(context, 700, Math.max(7, 10 * state.ui.zoom));
        context.fillStyle = paletteStyle?.progressText || "rgba(15, 23, 42, 0.78)";
        context.textAlign = "center";
        context.fillText(display.centerText || "", centerX, centerY + Math.max(2, 3 * state.ui.zoom));
        context.restore();
        return display.title;
    }

    function resolveCanvasNodeMarkers(node) {
        const markers = [];
        const seen = new Set();
        const pushMarker = marker => {
            const icon = (marker?.icon || "").trim().toLowerCase();
            if (!icon || seen.has(icon)) {
                return;
            }

            seen.add(icon);
            markers.push({
                icon,
                tone: (marker?.tone || "accent").trim().toLowerCase(),
                label: (marker?.label || icon).trim()
            });
        };

        if (Array.isArray(node?.markers)) {
            for (const marker of node.markers) {
                pushMarker(marker);
            }
        }

        if (markers.length === 0 && node?.markerIcon) {
            pushMarker({
                icon: node.markerIcon,
                tone: node.markerTone,
                label: node.markerLabel
            });
        }

        return markers;
    }

    function resolveMarkerToneAccentColor(tone, fallbackAccent) {
        switch ((tone || "").toLowerCase()) {
            case "sky":
                return "#38bdf8";
            case "mint":
                return "#10b981";
            case "warn":
                return "#f97316";
            case "danger":
                return "#e11d48";
            case "primary":
                return "#0f172a";
            case "ghost":
                return "#94a3b8";
            case "accent":
            default:
                return fallbackAccent || "#8b5cf6";
        }
    }

    function drawCanvasMarkerBadges(context, state, node, accent, paletteStyle, startLeft, top, badgeSize, badgeGap, maxVisible, direction, meta) {
        const markers = resolveCanvasNodeMarkers(node);
        meta.markerText = markers.map(marker => marker.label).join(", ");
        if (markers.length === 0) {
            return startLeft;
        }

        let cursor = startLeft;
        const limit = Math.max(1, maxVisible || 3);
        for (const marker of markers.slice(0, limit)) {
            const bounds = direction === "left"
                ? buildRect(cursor - badgeSize, top, badgeSize, badgeSize)
                : buildRect(cursor, top, badgeSize, badgeSize);
            drawCanvasBadgePill(
                context,
                bounds,
                resolveMarkerGlyph(marker.icon),
                hexToRgba(resolveMarkerToneAccentColor(marker.tone, accent), 0.12),
                paletteStyle.subtleStroke,
                paletteStyle.subtleText,
                Math.max(8, 9.5 * state.ui.zoom));
            cursor = direction === "left"
                ? bounds.left - badgeGap
                : bounds.right + badgeGap;
        }

        const overflowCount = markers.length - limit;
        if (overflowCount > 0) {
            const bounds = direction === "left"
                ? buildRect(cursor - badgeSize, top, badgeSize, badgeSize)
                : buildRect(cursor, top, badgeSize, badgeSize);
            drawCanvasBadgePill(
                context,
                bounds,
                `+${overflowCount}`,
                paletteStyle.subtleFill,
                paletteStyle.subtleStroke,
                paletteStyle.subtleText,
                Math.max(7.5, 8.8 * state.ui.zoom));
            cursor = direction === "left"
                ? bounds.left - badgeGap
                : bounds.right + badgeGap;
        }

        return cursor;
    }

    function drawCanvasAnnotationBadges(context, state, node, startX, y, maxWidth, baseFontSize) {
        const annotations = Array.isArray(node?.annotations) ? node.annotations : [];
        const results = [];
        if (annotations.length === 0) {
            return results;
        }

        let cursorX = startX;
        let cursorY = y;
        const rowHeight = Math.max(16, 18 * state.ui.zoom);
        const gap = Math.max(4, 6 * state.ui.zoom);
        context.save();
        setCanvasFont(context, 700, baseFontSize);
        for (let index = 0; index < annotations.length; index += 1) {
            const annotation = annotations[index];
            const label = annotation?.icon
                ? `${annotation.icon} ${annotation.label || annotation.kind || "Signal"}`
                : (annotation?.label || annotation?.kind || "Signal");
            const width = Math.min(maxWidth, Math.max(36, context.measureText(label).width + (16 * state.ui.zoom)));
            if ((cursorX - startX) + width > maxWidth) {
                cursorX = startX;
                cursorY += rowHeight + gap;
            }

            const bounds = buildRect(cursorX, cursorY, width, rowHeight);
            drawCanvasBadgePill(
                context,
                bounds,
                label,
                "rgba(255, 255, 255, 0.94)",
                "rgba(148, 163, 184, 0.24)",
                "rgba(15, 23, 42, 0.78)",
                baseFontSize);
            results.push({
                bounds,
                annotation,
                annotationIndex: index
            });
            cursorX += width + gap;
        }

        context.restore();
        return results;
    }

    function drawNodeMediaPreview(context, state, node, bounds, radius) {
        if (!node?.mediaKind || !node?.mediaPreviewUrl) {
            return false;
        }

        drawRoundedPanel(
            context,
            bounds,
            radius,
            "rgba(241, 245, 249, 0.95)",
            "rgba(148, 163, 184, 0.22)",
            1,
            "");
        if (node.mediaKind === "image") {
            const entry = requestSceneImage(state, node.mediaPreviewUrl);
            if (entry?.isLoaded && entry.image) {
                context.save();
                context.beginPath();
                context.roundRect(bounds.left, bounds.top, bounds.width, bounds.height, radius);
                context.clip();
                context.drawImage(entry.image, bounds.left, bounds.top, bounds.width, bounds.height);
                context.restore();
            }
        }

        context.save();
        setCanvasFont(context, 700, Math.max(10, 12 * state.ui.zoom));
        context.fillStyle = "rgba(15, 23, 42, 0.76)";
        context.fillText(node.mediaKind === "image" ? "Image" : "Preview", bounds.left + (12 * state.ui.zoom), bounds.bottom - (12 * state.ui.zoom));
        context.restore();
        return true;
    }

    function renderCanvasMicroNode(context, state, node, hostBounds, accent, meta) {
        const isSelected = state.selectedIds.has(node.id);
        const paletteStyle = resolveCanvasNodePaletteStyle(node, accent, isSelected);
        drawRoundedPanel(
            context,
            hostBounds,
            Math.max(10, 16 * state.ui.zoom),
            paletteStyle.surfaceFill,
            paletteStyle.surfaceStroke,
            isSelected ? Math.max(1.4, 2 * state.ui.zoom) : 1,
            paletteStyle.surfaceShadow);
        context.save();
        setCanvasFont(context, 700, Math.max(7, 10 * state.ui.zoom));
        context.fillStyle = paletteStyle.titleText;
        context.textAlign = "center";
        const label = (node.title || node.kind || "Node").slice(0, 12);
        context.fillText(label, hostBounds.left + (hostBounds.width / 2), hostBounds.top + (hostBounds.height / 2) + 3);
        context.restore();
        meta.progressTitle = resolveProgressDisplay(node?.progressMode, node?.progressPercent).title;
    }

    function renderCanvasInlineTextNode(context, state, node, hostBounds, accent, detailMode, meta) {
        const isSelected = state.selectedIds.has(node.id);
        const paletteStyle = resolveCanvasNodePaletteStyle(node, accent, isSelected);
        drawRoundedPanel(
            context,
            hostBounds,
            Math.max(16, 20 * state.ui.zoom),
            paletteStyle.surfaceFill,
            paletteStyle.surfaceStroke,
            isSelected ? Math.max(1.5, 2.2 * state.ui.zoom) : 1,
            paletteStyle.surfaceShadow);
        context.save();
        const padding = Math.max(12, 18 * state.ui.zoom);
        const contentWidth = Math.max(24, hostBounds.width - (padding * 2));
        const noteText = node.inlineText || node.title || node.leadText || "Write note";
        setCanvasFont(context, 600, Math.max(10, 13 * state.ui.zoom));
        const primitives = getCanvasRuntimePrimitives();
        const lines = primitives?.wrapText
            ? primitives.wrapText(context, noteText, contentWidth, detailMode === "compact" ? 2 : 5)
            : [noteText];
        drawCanvasTextLines(
            context,
            lines,
            hostBounds.left + padding,
            hostBounds.top + padding + Math.max(10, 14 * state.ui.zoom),
            Math.max(12, 16 * state.ui.zoom),
            paletteStyle.titleText);
        context.restore();

        const badgeSize = Math.max(18, 22 * state.ui.zoom);
        const badgeGap = Math.max(4, 6 * state.ui.zoom);
        const badgeTop = hostBounds.bottom - Math.max(24, 28 * state.ui.zoom);
        let badgeLeft = hostBounds.left + Math.max(10, 14 * state.ui.zoom);
        const indicatorBounds = buildRect(badgeLeft, badgeTop, badgeSize, badgeSize);
        meta.progressTitle = drawCanvasProgressBadge(context, state, indicatorBounds, node, paletteStyle);
        registerSceneHotZone(state, indicatorBounds, {
            type: "node-progress",
            nodeId: node.id
        });
        badgeLeft = indicatorBounds.right + badgeGap;

        badgeLeft = drawCanvasMarkerBadges(context, state, node, accent, paletteStyle, badgeLeft, badgeTop, badgeSize, badgeGap, 3, "right", meta);

        if (node.priority > 0) {
            meta.priorityText = `${node.priority}`;
            const priorityBounds = buildRect(badgeLeft, badgeTop, badgeSize, badgeSize);
            drawCanvasBadgePill(
                context,
                priorityBounds,
                `${node.priority}`,
                paletteStyle.subtleFill,
                paletteStyle.subtleStroke,
                paletteStyle.subtleText,
                Math.max(8, 9.5 * state.ui.zoom));
            badgeLeft = priorityBounds.right + badgeGap;
        }

        const annotations = drawCanvasAnnotationBadges(
            context,
            state,
            node,
            badgeLeft,
            hostBounds.bottom - Math.max(25, 30 * state.ui.zoom),
            Math.max(24, hostBounds.right - badgeLeft - Math.max(12, 16 * state.ui.zoom)),
            Math.max(8, 9.5 * state.ui.zoom));
        for (const entry of annotations) {
            registerSceneHotZone(state, entry.bounds, {
                type: "annotation",
                nodeId: node.id,
                annotation: entry.annotation,
                annotationIndex: entry.annotationIndex
            });
        }

        drawCanvasCollapseControl(context, state, node, paletteStyle);
    }

    function drawCanvasCollapseControl(context, state, node, paletteStyle) {
        if (!node?.isCollapsible) {
            return;
        }

        const collapseSize = Math.max(16, 18 * state.ui.zoom);
        const collapseAnchor = resolveCollapseAnchorInfo(state, node);
        const hostAnchor = worldToHostPoint(state, collapseAnchor.world);
        const collapseBounds = buildRect(
            hostAnchor.x - (collapseSize / 2),
            hostAnchor.y - (collapseSize / 2),
            collapseSize,
            collapseSize);
        drawCanvasBadgePill(
            context,
            collapseBounds,
            state.collapsedIds.has(node.id) ? "+" : "-",
            paletteStyle.iconFill,
            paletteStyle.iconStroke,
            paletteStyle.iconText,
            Math.max(9, 11 * state.ui.zoom));
        registerSceneHotZone(state, collapseBounds, {
            type: "node-collapse",
            nodeId: node.id
        });
    }

    function renderCanvasStandardNode(context, state, node, hostBounds, accent, detailMode, meta) {
        const isSelected = state.selectedIds.has(node.id);
        const paletteStyle = resolveCanvasNodePaletteStyle(node, accent, isSelected);
        const padding = Math.max(12, 18 * state.ui.zoom);
        drawRoundedPanel(
            context,
            hostBounds,
            Math.max(18, 22 * state.ui.zoom),
            paletteStyle.surfaceFill,
            paletteStyle.surfaceStroke,
            isSelected ? Math.max(1.6, 2.4 * state.ui.zoom) : 1,
            paletteStyle.surfaceShadow);
        if (node.isPreviewOnly) {
            context.save();
            context.strokeStyle = "rgba(14, 165, 233, 0.38)";
            context.setLineDash([8, 6]);
            context.lineWidth = Math.max(1, 1.4 * state.ui.zoom);
            context.beginPath();
            context.roundRect(hostBounds.left + 6, hostBounds.top + 6, Math.max(0, hostBounds.width - 12), Math.max(0, hostBounds.height - 12), Math.max(12, 16 * state.ui.zoom));
            context.stroke();
            context.restore();
        }

        let cursorY = hostBounds.top + padding;
        const contentLeft = hostBounds.left + padding;
        const contentWidth = Math.max(30, hostBounds.width - (padding * 2));
        const rightCursorStart = hostBounds.right - padding;

        context.save();
        setCanvasFont(context, 700, Math.max(8, 10.5 * state.ui.zoom));
        context.fillStyle = paletteStyle.labelText;
        context.fillText(node.kind || node.family || "item", contentLeft + Math.max(20, 26 * state.ui.zoom), cursorY + Math.max(7, 10 * state.ui.zoom));
        drawCanvasBadgePill(
            context,
            buildRect(contentLeft, cursorY, Math.max(18, 22 * state.ui.zoom), Math.max(18, 22 * state.ui.zoom)),
            (node.icon || node.kind || "n").slice(0, 1).toUpperCase(),
            paletteStyle.iconFill,
            paletteStyle.iconStroke,
            paletteStyle.iconText,
            Math.max(8, 9 * state.ui.zoom));
        context.restore();

        let rightCursor = rightCursorStart;
        const badgeSize = Math.max(18, 22 * state.ui.zoom);
        const badgeGap = Math.max(4, 6 * state.ui.zoom);
        const progressBounds = buildRect(rightCursor - badgeSize, cursorY, badgeSize, badgeSize);
        meta.progressTitle = drawCanvasProgressBadge(context, state, progressBounds, node, paletteStyle);
        registerSceneHotZone(state, progressBounds, {
            type: "node-progress",
            nodeId: node.id
        });
        rightCursor = progressBounds.left - badgeGap;

        rightCursor = drawCanvasMarkerBadges(context, state, node, accent, paletteStyle, rightCursor, cursorY, badgeSize, badgeGap, 3, "left", meta);

        if (node.priority > 0) {
            meta.priorityText = `${node.priority}`;
            const priorityBounds = buildRect(rightCursor - badgeSize, cursorY, badgeSize, badgeSize);
            drawCanvasBadgePill(
                context,
                priorityBounds,
                `${node.priority}`,
                paletteStyle.subtleFill,
                paletteStyle.subtleStroke,
                paletteStyle.subtleText,
                Math.max(8, 9.5 * state.ui.zoom));
            rightCursor = priorityBounds.left - badgeGap;
        }

        cursorY += Math.max(28, 34 * state.ui.zoom);
        if (node.mediaPreviewUrl && detailMode === "full") {
            const mediaHeight = Math.min(Math.max(42, 62 * state.ui.zoom), hostBounds.height * 0.28);
            drawNodeMediaPreview(
                context,
                state,
                node,
                buildRect(contentLeft, cursorY, contentWidth, mediaHeight),
                Math.max(10, 12 * state.ui.zoom));
            cursorY += mediaHeight + Math.max(10, 12 * state.ui.zoom);
        }

        const primitives = getCanvasRuntimePrimitives();
        context.save();
        setCanvasFont(context, 700, Math.max(10, 15 * state.ui.zoom));
        const titleLines = primitives?.wrapText
            ? primitives.wrapText(context, node.title || "Untitled", contentWidth, detailMode === "compact" ? 1 : 2)
            : [node.title || "Untitled"];
        drawCanvasTextLines(
            context,
            titleLines,
            contentLeft,
            cursorY + Math.max(8, 12 * state.ui.zoom),
            Math.max(12, 17 * state.ui.zoom),
            paletteStyle.titleText);
        cursorY += (titleLines.length * Math.max(12, 17 * state.ui.zoom)) + Math.max(4, 6 * state.ui.zoom);
        context.restore();

        const secondaryLines = [];
        if (node.subtitle) {
            secondaryLines.push(node.subtitle);
        }
        if (detailMode === "full" && node.compactPath?.promotedText &&
            node.compactPath.promotedText !== node.title &&
            node.compactPath.promotedText !== node.subtitle) {
            secondaryLines.push(node.compactPath.promotedText);
        }
        if (detailMode === "full" && node.leadText) {
            secondaryLines.push(node.leadText);
        }

        context.save();
        setCanvasFont(context, 500, Math.max(8, 11.5 * state.ui.zoom));
        context.fillStyle = paletteStyle.secondaryText;
        for (const line of secondaryLines.slice(0, detailMode === "compact" ? 1 : 3)) {
            const wrapped = primitives?.wrapText
                ? primitives.wrapText(context, line, contentWidth, detailMode === "compact" ? 1 : 2)
                : [line];
            drawCanvasTextLines(
                context,
                wrapped,
                contentLeft,
                cursorY + Math.max(7, 10 * state.ui.zoom),
                Math.max(10, 14 * state.ui.zoom),
                paletteStyle.secondaryText);
            cursorY += (wrapped.length * Math.max(10, 14 * state.ui.zoom)) + Math.max(4, 6 * state.ui.zoom);
        }
        context.restore();

        if (node.compactPath?.fullPath && detailMode !== "micro") {
            const pathHeight = Math.max(20, 24 * state.ui.zoom);
            const pathBounds = buildRect(contentLeft, cursorY, contentWidth, pathHeight);
            drawRoundedPanel(
                context,
                pathBounds,
                Math.max(8, 10 * state.ui.zoom),
                paletteStyle.subtleFill,
                paletteStyle.subtleStroke,
                1,
                "");
            context.save();
            setCanvasFont(context, 600, Math.max(8, 10 * state.ui.zoom));
            const textWidth = Math.max(12, pathBounds.width - Math.max(28, 34 * state.ui.zoom));
            const pathLabel = primitives?.fitText
                ? primitives.fitText(context, node.compactPath.displayText || node.compactPath.fullPath, textWidth, "...")
                : (node.compactPath.displayText || node.compactPath.fullPath);
            context.fillStyle = paletteStyle.subtleText;
            context.fillText(pathLabel, pathBounds.left + Math.max(8, 10 * state.ui.zoom), pathBounds.top + Math.max(13, 15 * state.ui.zoom));
            context.textAlign = "right";
            context.fillText(
                state.pathCopyState?.nodeId === node.id ? resolveActionGlyph("qa") : resolveActionGlyph("copy"),
                pathBounds.right - Math.max(8, 10 * state.ui.zoom),
                pathBounds.top + Math.max(13, 15 * state.ui.zoom));
            context.restore();
            meta.hasPathButton = true;
            meta.pathTitle = node.compactPath.fullPath;
            meta.pathDisplayText = node.compactPath.displayText || node.compactPath.fullPath;
            meta.pathPromotedText = node.compactPath.promotedText || "";
            registerSceneHotZone(state, pathBounds, {
                type: "node-path",
                nodeId: node.id,
                compactPath: node.compactPath
            });
            cursorY += pathHeight + Math.max(6, 8 * state.ui.zoom);
        }

        const annotationEntries = detailMode === "full"
            ? drawCanvasAnnotationBadges(
                context,
                state,
                node,
                contentLeft,
                cursorY,
                contentWidth,
                Math.max(8, 9 * state.ui.zoom))
            : [];
        if (annotationEntries.length > 0) {
            const lastEntry = annotationEntries[annotationEntries.length - 1];
            cursorY = lastEntry.bounds.bottom + Math.max(8, 10 * state.ui.zoom);
            for (const entry of annotationEntries) {
                registerSceneHotZone(state, entry.bounds, {
                    type: "annotation",
                    nodeId: node.id,
                    annotation: entry.annotation,
                    annotationIndex: entry.annotationIndex
                });
            }
        }

        const footerHeight = Math.max(18, 22 * state.ui.zoom);
        const footerTop = hostBounds.bottom - padding - footerHeight;
        const footerPillBounds = buildRect(contentLeft, footerTop, Math.max(52, 70 * state.ui.zoom), footerHeight);
        drawCanvasBadgePill(
            context,
            footerPillBounds,
            node.isRequired ? "required" : "optional",
            paletteStyle.subtleFill,
            paletteStyle.subtleStroke,
            paletteStyle.subtleText,
            Math.max(8, 9.5 * state.ui.zoom));

        drawCanvasCollapseControl(context, state, node, paletteStyle);
    }

    Object.assign(shared, { getCanvasRuntimePrimitives, createFallbackHitRegistry, createCanvasHitRegistry, createCanvasSurfaceHost, destroyCanvasSurfaceHost, hexToRgba, resolveNodeAccentColor, resolveAnchorRect, buildRect, boundsToHitRect, projectSceneBounds, getNodeSceneBounds, clearSceneHotZones, registerSceneHotZone, getSceneHitAtPoint, getSceneHitAtEvent, resolveHitNode, clearScenePopoverHover, syncSceneHoverState, resolveCanvasNodeDetailMode, setCanvasFont, drawCanvasTextLines, drawRoundedPanel, requestSceneImage, buildCanvasSnapshotBounds, reconcileRetainedLayer, drawCanvasFrame, renderGroupFrames, drawCanvasLink, renderLinks, drawCanvasBadgePill, drawCanvasProgressBadge, drawCanvasAnnotationBadges, drawNodeMediaPreview, renderCanvasMicroNode, renderCanvasInlineTextNode, renderCanvasStandardNode });
})();
