(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 07-runtime-entry.js.'); }
    const workbenchInternals = new Proxy({}, {
        get(_target, property) {
            return shared.workbenchInternals?.[property];
        }
    });
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition, getLinkAnchorPoint, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, createMarkerBadge, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, writeClipboardText, copySelectionToClipboard, requestClipboardCut, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint, hitTestNode, hitTestFrameHandle, hitTestProgressBadge, isOverlayTarget, applyFullTextTooltip, reconcileSelection, applySelection, selectSingleNode, publishSelection, clearViewportStateCommit, createSerializedStateSnapshot, invokeStateChanged, publishState, publishStateNow, scheduleViewportStateCommit, publishNodesMoved, setSelection, toggleSelection, toggleCollapse, clearContextMenu, closeComposer, ensureHostFocus, deferHostFocus, resolveComposerAnchor, layoutComposer, render, getContextActions, isCreateAction, buildCreateRequest, resolveMenuLabel, getMenuScale, isCompactHiveLayout, resolveMenuActionVariant, getActionMetrics, applyProgressPresetTone, fitContextMenuLabel, resolveActionGlyph, createMenuActionIcon, resolveMenuActionAriaLabel, getRadialOffsets, buildCompactHiveCoordinates, getCompactHiveOffsets, resolveContextMenuOffsets, resolveContextMenuSafeTop, getContextMenuLayerBounds, clampLayerBoundsToHost, positionContextMenu, getContextMenuOrbitRadius, getContextMenuLocalPoint, isPointInContextMenuLayer, closeContextMenuLayersFrom, syncContextMenuLayers, resolveSubmenuOrigin, ensureSubmenuLoadingIndicator, clearSubmenuLoadingIndicator, cancelPendingContextSubmenu, scheduleContextSubmenuOpen, clampLayerOriginToHost, getToolboxPanelSize, getToolboxPanelBounds, clampToolboxPanelOriginToHost, resolveToolboxPanelOrigin, createContextMenuLayer, shiftContextMenuLayerOrigin, nudgeContextMenuLayerIntoVisibleHost, resolveQuickCreateSourceNode, submitCreateRequest, submitNodeEdit, readFileAsUpload, commitComposer, decorateComposerShell, createComposerWizard, createComposerSection, updateComposerFileState, openCreateComposer, openInlineNoteComposer, buildChildNotePlacement, buildSiblingNotePlacement, openKeyboardNoteComposer, openExistingNoteEditor, executeContextAction, openContextSubmenu, openContextSubmenuByActionId, renderContextMenuLayer, renderToolboxPreview, renderToolboxPanelLayer, showContextMenu, legacyOpenNodeMetadataMenu, startPan, isMarqueeModifierPressed, startMarquee, ensureSelectedForDrag, startDragForNodeIds, startDrag, startFrameDrag, updateMarquee, legacyApplyMarqueeSelection, updateDrag, updatePan, isNodeVisibleInViewport, centerNodeElementInViewport, ensureNodeVisible, legacyResize, findContainingBlockOverride, suspendContainingBlock, restoreContainingBlock, setMaximized, fitView, focusNode, normalizeWheelDelta, applyWheelZoom, setZoomPercent, setMenuScalePercent, toggleHelp, isManualDoubleActivation, handleNodeDoubleActivation, requestNodeOpen, legacyAttachEvents, hydrateState, refresh, getCanvasRuntimePrimitives, createFallbackHitRegistry, createCanvasHitRegistry, createCanvasSurfaceHost, destroyCanvasSurfaceHost, hexToRgba, resolveNodeAccentColor, resolveAnchorRect, buildRect, boundsToHitRect, projectSceneBounds, getNodeSceneBounds, clearSceneHotZones, registerSceneHotZone, getSceneHitAtPoint, getSceneHitAtEvent, resolveHitNode, clearScenePopoverHover, syncSceneHoverState, resolveCanvasNodeDetailMode, setCanvasFont, drawCanvasTextLines, drawRoundedPanel, requestSceneImage, buildCanvasSnapshotBounds, reconcileRetainedLayer, drawCanvasFrame, renderGroupFrames, drawCanvasLink, renderLinks, drawCanvasBadgePill, drawCanvasProgressBadge, drawCanvasAnnotationBadges, drawNodeMediaPreview, renderCanvasMicroNode, renderCanvasInlineTextNode, renderCanvasStandardNode } = shared;
    const { renderNodes, renderActiveDrag, renderSnapGuides, renderConnectorAnchorOverlay, renderTransformHandlesOverlay, renderDebugDecorations, renderMinimap, measureRenderedNodeSizes, scheduleNodeMeasurement, applySceneTransform, applyMarqueeSelection, buildDiagnosticsSnapshot, showPopover, openNodeMetadataMenu, copyCompactPath, resolveSurfaceMode, isDeleteMode, isDependencyMode, syncWorkbenchMode, setWorkbenchToolMode, updatePointerHostPoint, dispatchContextActionRequest, distancePointToSegment, cubicBezierPoint, isPointNearRenderedLink, hitTestRenderedLink, resolveDeleteModeHitTarget, updateDeleteHoverState, attachEvents } = shared;
    function resize(state) {
        cancelViewportAnimation(state);
        state.frameSurface?.measure?.();
        state.linkSurface?.measure?.();
        state.nodeSurface?.measure?.();
        state.minimapSurface?.measure?.();
        setPan(state, state.ui.panX, state.ui.panY);
        layoutComposer(state);
    }

    function buildWorkbench(state) {
        clear(state.host);
        state.host.classList.add("cw-workbench");
        syncWorkbenchMode(state);
        syncMenuScaleCss(state);

        const backdrop = createElement(state.document, "div", "cw-workbench__backdrop");
        const scene = createElement(state.document, "div", "cw-workbench__scene");
        const canvasStack = createElement(state.document, "div", "cw-workbench__canvas-stack");
        const frameCanvas = createElement(state.document, "canvas", "cw-workbench__canvas cw-workbench__canvas--frames");
        const linkCanvas = createElement(state.document, "canvas", "cw-workbench__canvas cw-workbench__canvas--links");
        const nodeCanvas = createElement(state.document, "canvas", "cw-workbench__canvas cw-workbench__canvas--nodes");
        const debugLayer = createElement(state.document, "div", "cw-workbench__debug-layer");
        const guideLayer = createElement(state.document, "div", "cw-workbench__guide-layer");
        const anchorLayer = createElement(state.document, "div", "cw-workbench__anchor-layer");
        const transformLayer = createElement(state.document, "div", "cw-workbench__transform-layer");
        const marquee = createElement(state.document, "div", "cw-marquee");
        const contextMenu = createElement(state.document, "div", "cw-context-menu");
        const emptyState = createElement(state.document, "div", "cw-empty-state");
        const emptyStateKicker = createElement(state.document, "p", "cw-empty-state__kicker");
        const emptyStateTitle = createElement(state.document, "h3", "cw-empty-state__title");
        const emptyStateBody = createElement(state.document, "p", "cw-empty-state__body");
        const diagnosticsPanel = createElement(state.document, "div", "cw-diagnostics");
        const diagnosticsTitle = createElement(state.document, "p", "cw-diagnostics__title", "Diagnostics");
        const diagnosticsBody = createElement(state.document, "div", "cw-diagnostics__body");
        const minimapShell = createElement(state.document, "div", "cw-minimap");
        const minimapTitle = createElement(state.document, "p", "cw-minimap__title");
        const minimapCanvas = createElement(state.document, "canvas", "cw-minimap__canvas");
        const popover = createElement(state.document, "div", "cw-workbench__popover");
        const popoverTitle = createElement(state.document, "strong", "cw-workbench__popover-title");
        const popoverBody = createElement(state.document, "span", "cw-workbench__popover-body");
        const statusNotice = createElement(state.document, "div", "cw-status-notice");

        frameCanvas.setAttribute("aria-hidden", "true");
        linkCanvas.setAttribute("aria-hidden", "true");
        nodeCanvas.setAttribute("aria-hidden", "true");
        contextMenu.style.display = "none";
        marquee.style.display = "none";
        emptyState.style.display = "none";
        diagnosticsPanel.style.display = "none";
        minimapShell.style.display = "none";
        popover.style.display = "none";
        statusNotice.style.display = "none";

        contextMenu.addEventListener("pointerdown", event => event.stopPropagation());
        contextMenu.addEventListener("contextmenu", event => {
            event.preventDefault();
            event.stopPropagation();
            const depth = (state.contextMenuState?.layers?.length || 1) - 1;
            if (depth > 0) {
                closeContextMenuLayersFrom(state, depth);
            }
        });
        emptyState.appendChild(emptyStateKicker);
        emptyState.appendChild(emptyStateTitle);
        emptyState.appendChild(emptyStateBody);
        diagnosticsPanel.appendChild(diagnosticsTitle);
        diagnosticsPanel.appendChild(diagnosticsBody);
        minimapShell.appendChild(minimapTitle);
        minimapCanvas.addEventListener("pointerdown", event => {
            event.preventDefault();
            event.stopPropagation();
            navigateViaMinimap(state, event);
        });
        minimapShell.appendChild(minimapCanvas);
        popover.appendChild(popoverTitle);
        popover.appendChild(popoverBody);

        canvasStack.appendChild(frameCanvas);
        canvasStack.appendChild(linkCanvas);
        canvasStack.appendChild(nodeCanvas);
        scene.appendChild(canvasStack);
        scene.appendChild(debugLayer);
        scene.appendChild(guideLayer);
        scene.appendChild(anchorLayer);
        scene.appendChild(transformLayer);
        state.host.appendChild(backdrop);
        state.host.appendChild(scene);
        state.host.appendChild(marquee);
        state.host.appendChild(emptyState);
        state.host.appendChild(diagnosticsPanel);
        state.host.appendChild(minimapShell);
        state.host.appendChild(contextMenu);
        state.host.appendChild(popover);
        state.host.appendChild(statusNotice);

        state.scene = scene;
        state.canvasStack = canvasStack;
        state.frameLayer = frameCanvas;
        state.links = linkCanvas;
        state.nodeLayer = nodeCanvas;
        state.debugLayer = debugLayer;
        state.guideLayer = guideLayer;
        state.anchorLayer = anchorLayer;
        state.transformLayer = transformLayer;
        state.marquee = marquee;
        state.emptyState = emptyState;
        state.emptyStateKicker = emptyStateKicker;
        state.emptyStateTitle = emptyStateTitle;
        state.emptyStateBody = emptyStateBody;
        state.diagnosticsPanel = diagnosticsPanel;
        state.diagnosticsBody = diagnosticsBody;
        state.minimapShell = minimapShell;
        state.minimapTitle = minimapTitle;
        state.minimapCanvas = minimapCanvas;
        state.contextMenu = contextMenu;
        state.popover = popover;
        state.popoverTitle = popoverTitle;
        state.popoverBody = popoverBody;
        state.statusNotice = statusNotice;
        state.sceneHitRegistry = createCanvasHitRegistry();
        state.sceneHotZones = [];
        state.sceneGeometry = {
            nodes: new Map(),
            frames: new Map()
        };
        state.frameSurface = createCanvasSurfaceHost(frameCanvas, state.host);
        state.linkSurface = createCanvasSurfaceHost(linkCanvas, state.host);
        state.nodeSurface = createCanvasSurfaceHost(nodeCanvas, state.host);
        // Measure the minimap against the canvas element itself. Measuring against the shell
        // creates a circular layout dependency where the shell grows to the canvas and the
        // canvas then re-measures to the enlarged shell, which can cover the stage on first load.
        state.minimapSurface = createCanvasSurfaceHost(minimapCanvas, minimapCanvas);
        resize(state);
    }

    function drawEmptyStateToExport(context, width, height, state) {
        const bounds = buildRect(22, height - 122, Math.min(420, width - 44), 100);
        drawRoundedPanel(
            context,
            bounds,
            18,
            "rgba(255, 255, 255, 0.94)",
            "rgba(15, 23, 42, 0.08)",
            1,
            "rgba(15, 23, 42, 0.1)");
        context.save();
        setCanvasFont(context, 700, 11);
        context.fillStyle = "rgba(15, 23, 42, 0.58)";
        context.fillText(state.surface.chrome.emptyStateKicker || "Canvas", bounds.left + 16, bounds.top + 22);
        setCanvasFont(context, 700, 16);
        context.fillStyle = "rgba(15, 23, 42, 0.92)";
        context.fillText(state.surface.chrome.emptyStateTitle || "No nodes yet", bounds.left + 16, bounds.top + 48);
        setCanvasFont(context, 500, 12);
        context.fillStyle = "rgba(71, 85, 105, 0.86)";
        context.fillText(state.surface.chrome.emptyStateDescription || "Use quick create to start building the scene.", bounds.left + 16, bounds.top + 72);
        context.restore();
    }

    async function exportImageData(host) {
        const state = host?.__canvasWorkbenchState;
        if (!state || !state.frameSurface || !state.linkSurface || !state.nodeSurface) {
            return null;
        }

        const bounds = host.getBoundingClientRect();
        const width = Math.max(1, Math.ceil(bounds.width));
        const height = Math.max(1, Math.ceil(bounds.height));
        const canvas = state.document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;
        const context = canvas.getContext("2d");
        if (!context) {
            return null;
        }

        context.fillStyle = "rgba(248, 250, 252, 1)";
        context.fillRect(0, 0, width, height);
        context.save();
        context.globalAlpha = 0.45;
        context.fillStyle = "rgba(15, 23, 42, 0.08)";
        for (let x = 0; x <= width; x += 20) {
            for (let y = 0; y <= height; y += 20) {
                context.beginPath();
                context.arc(x, y, 1, 0, Math.PI * 2);
                context.fill();
            }
        }
        context.restore();
        context.drawImage(state.frameSurface.canvas, 0, 0, width, height);
        context.drawImage(state.linkSurface.canvas, 0, 0, width, height);
        context.drawImage(state.nodeSurface.canvas, 0, 0, width, height);
        if ((state.metrics?.lastRenderedNodeCount || 0) === 0) {
            drawEmptyStateToExport(context, width, height, state);
        }

        return canvas.toDataURL("image/png").replace(/^data:image\/png;base64,/, "");
    }

    function collectSceneSnapshot(state) {
        return {
            rendererMode: "canvas",
            mode: resolveSurfaceMode(state),
            dependencySourceId: state.surface?.dependencySourceId || "",
            nodes: [...(state.sceneGeometry?.nodes?.values?.() || [])],
            links: (state.renderedLinks || []).map(link => ({
                key: link.key || "",
                sourceId: link.sourceId || "",
                targetId: link.targetId || "",
                kind: link.kind || "",
                midPoint: link.midPoint || null,
                bounds: link.bounds || null
            })),
            previewLink: state.previewLink
                ? {
                    sourceId: state.previewLink.sourceId || "",
                    targetId: state.previewLink.targetId || "",
                    midPoint: state.previewLink.midPoint || null,
                    bounds: state.previewLink.bounds || null
                }
                : null,
            frames: [...(state.renderedFrames?.entries?.() || [])].map(([frameId, entry]) => ({
                frameId,
                label: entry?.frame?.label || "Group border",
                nodeIds: entry?.nodeIds || [],
                left: round(entry?.hostBounds?.left || 0),
                top: round(entry?.hostBounds?.top || 0),
                width: round(entry?.hostBounds?.width || 0),
                height: round(entry?.hostBounds?.height || 0),
                labelLeft: round(entry?.labelBounds?.left || 0),
                labelTop: round(entry?.labelBounds?.top || 0),
                labelWidth: round(entry?.labelBounds?.width || 0),
                labelHeight: round(entry?.labelBounds?.height || 0)
            })),
            hotZones: (state.sceneHotZones || []).map(entry => ({
                type: entry.metadata?.type || "",
                nodeId: entry.metadata?.nodeId || "",
                frameId: entry.metadata?.frameId || "",
                bounds: entry.bounds
            })),
            hoveredDeleteNodeId: state.hoveredDeleteNodeId || "",
            hoveredDeleteLinkKey: state.hoveredDeleteLinkKey || "",
            pointerHostPoint: state.pointerHostPoint
                ? {
                    x: round(state.pointerHostPoint.x),
                    y: round(state.pointerHostPoint.y)
                }
                : null,
            minimap: state.minimapMetrics
                ? {
                    width: round(state.minimapMetrics.width),
                    height: round(state.minimapMetrics.height),
                    nodeCount: round(state.minimapMetrics.nodeCount || 0)
                }
                : null
        };
    }

    function findSceneHotZoneCenter(state, request) {
        const zoneType = request?.zone || request?.type || "";
        const hotZones = state.sceneHotZones || [];
        let candidate = null;
        for (let index = hotZones.length - 1; index >= 0; index -= 1) {
            const entry = hotZones[index];
            if ((!zoneType || entry.metadata?.type === zoneType) &&
                (!request?.nodeId || entry.metadata?.nodeId === request.nodeId) &&
                (!request?.frameId || entry.metadata?.frameId === request.frameId)) {
                candidate = entry;
                break;
            }
        }
        if (!candidate) {
            return null;
        }

        return {
            x: round(candidate.bounds.x + (candidate.bounds.width / 2)),
            y: round(candidate.bounds.y + (candidate.bounds.height / 2)),
            width: round(candidate.bounds.width),
            height: round(candidate.bounds.height)
        };
    }

    function activateHotZone(state, request) {
        const center = findSceneHotZoneCenter(state, request);
        if (!center) {
            return false;
        }

        const hitTarget = getSceneHitAtPoint(state, center.x, center.y);
        if (!hitTarget) {
            return false;
        }

        if (hitTarget.type === "node-path") {
            state.pathCopyState = {
                nodeId: hitTarget.nodeId,
                timerHandle: state.pathCopyState?.timerHandle || 0
            };
            void copyCompactPath(state, null, hitTarget.compactPath);
            return true;
        }

        if (hitTarget.type === "annotation") {
            const node = resolveHitNode(state, hitTarget);
            if (!node) {
                return false;
            }

            clearScenePopoverHover(state);
            invokeAnnotationAction(state, node, hitTarget.annotation);
            return true;
        }

        if (hitTarget.type === "node-collapse") {
            toggleCollapse(state, hitTarget.nodeId);
            return true;
        }

        return false;
    }

    function createSyntheticPointerEvent(state, clientX, clientY, request) {
        return {
            clientX,
            clientY,
            button: typeof request?.button === "number" ? request.button : 0,
            altKey: !!request?.altKey,
            ctrlKey: !!request?.ctrlKey,
            metaKey: !!request?.metaKey,
            shiftKey: !!request?.shiftKey,
            target: state.host,
            preventDefault() {
            },
            stopPropagation() {
            }
        };
    }

    function resolveSyntheticNodeDragStart(state, request) {
        const nodeId = request?.nodeId || "";
        if (!nodeId) {
            return null;
        }

        const snapshot = state.sceneGeometry?.nodes?.get?.(nodeId);
        if (!snapshot) {
            return null;
        }

        return {
            x: snapshot.left + Math.max(28, Math.min(snapshot.width - 42, snapshot.width * 0.34)),
            y: snapshot.top + Math.max(32, Math.min(snapshot.height - 34, snapshot.height * 0.5))
        };
    }

    function resolveSyntheticDragStart(state, request) {
        if (request?.frameId) {
            return findSceneHotZoneCenter(state, {
                zone: "frame-handle",
                frameId: request.frameId
            });
        }

        return resolveSyntheticNodeDragStart(state, request);
    }

    function simulatePointerDrag(state, request) {
        if (!state?.handlers?.pointerDown || !state?.handlers?.pointerMove || !state?.handlers?.pointerUp) {
            return false;
        }

        const startPoint = resolveSyntheticDragStart(state, request || {});
        if (!startPoint) {
            return false;
        }

        const hostRect = state.host.getBoundingClientRect();
        const startClientX = hostRect.left + startPoint.x;
        const startClientY = hostRect.top + startPoint.y;
        const deltaX = Number(request?.deltaX);
        const deltaY = Number(request?.deltaY);
        const safeDeltaX = Number.isFinite(deltaX) ? deltaX : 0;
        const safeDeltaY = Number.isFinite(deltaY) ? deltaY : 0;
        const stepCount = Math.max(1, Math.min(32, Math.trunc(Number(request?.steps) || 10)));

        if (state.composer) {
            closeComposer(state, { focusHost: false });
        }

        clearContextMenu(state);
        cancelViewportAnimation(state);
        ensureHostFocus(state);
        deferHostFocus(state);

        const startEvent = createSyntheticPointerEvent(state, startClientX, startClientY, request);
        state.handlers.pointerDown(startEvent);

        if (!state.interaction) {
            return true;
        }

        for (let stepIndex = 1; stepIndex <= stepCount; stepIndex += 1) {
            const progress = stepIndex / stepCount;
            const moveEvent = createSyntheticPointerEvent(
                state,
                startClientX + (safeDeltaX * progress),
                startClientY + (safeDeltaY * progress),
                request);
            switch (state.interaction?.kind) {
                case "drag":
                case "frame-drag":
                    updateDrag(state, moveEvent);
                    break;
                case "pan":
                    updatePan(state, moveEvent);
                    break;
                case "marquee":
                    updateMarquee(state, moveEvent);
                    break;
                default:
                    state.handlers.pointerMove(moveEvent);
                    break;
            }
        }

        if (request?.release !== false) {
            state.handlers.pointerUp();
        }

        return true;
    }

    function releaseSyntheticInteraction(state) {
        if (!state?.interaction || !state?.handlers?.pointerUp) {
            return false;
        }

        state.handlers.pointerUp();
        return true;
    }

    function removeWorkbenchEventHandlers(state) {
        if (!state?.handlers) {
            return;
        }

        state.host.removeEventListener("pointerdown", state.handlers.pointerDown);
        window.removeEventListener("pointermove", state.handlers.pointerMove);
        window.removeEventListener("pointerup", state.handlers.pointerUp);
        window.removeEventListener("blur", state.handlers.blur);
        state.host.removeEventListener("dblclick", state.handlers.doubleClick);
        state.host.removeEventListener("wheel", state.handlers.wheel);
        state.host.removeEventListener("contextmenu", state.handlers.contextMenu);
        state.document.removeEventListener("keydown", state.handlers.keyDown);
    }

    function disposeWorkbenchStateCore(state, options) {
        destroyCanvasSurfaceHost(state.frameSurface);
        destroyCanvasSurfaceHost(state.linkSurface);
        destroyCanvasSurfaceHost(state.nodeSurface);
        destroyCanvasSurfaceHost(state.minimapSurface);
        if (state.pathCopyState?.timerHandle) {
            window.clearTimeout(state.pathCopyState.timerHandle);
        }

        if (state.resizeObserver) {
            state.resizeObserver.disconnect();
        }

        if (state.measureLayoutFrame) {
            window.cancelAnimationFrame(state.measureLayoutFrame);
            state.measureLayoutFrame = 0;
        }

        shared.cancelDeferredViewportRender?.(state);

        if (state.statusNoticeTimer) {
            window.clearTimeout(state.statusNoticeTimer);
            state.statusNoticeTimer = 0;
        }

        if (options?.clearFocusRecenterTimer && state.focusRecenterTimer) {
            window.clearTimeout(state.focusRecenterTimer);
            state.focusRecenterTimer = 0;
        }

        workbenchInternals.stateStore.clearViewportStateCommit(state);
        state.publishStateDebounced.cancel?.();

        if (state.animationTimeline) {
            state.animationTimeline.dispose();
        }

        removeWorkbenchEventHandlers(state);
        workbenchInternals.runtime.setMaximized(state, false);
        clear(state.host);
        delete state.host.__canvasWorkbenchState;
    }

    function legacyDisposeWorkbenchState(state) {
        if (!state) {
            return;
        }

        disposeWorkbenchStateCore(state, {
            clearFocusRecenterTimer: true
        });
    }

    function disposeWorkbenchState(state) {
        if (!state) {
            return;
        }

        disposeWorkbenchStateCore(state);
    }

    function createWorkbenchInternals() {
        return Object.freeze({
            instrumentation: Object.freeze({
                createWorkbenchMetrics,
                cloneWorkbenchMetrics,
                buildDiagnosticsSnapshot,
                resetLastDragPatchMetrics,
                recordDragPatchMetrics,
                now
            }),
            stateStore: Object.freeze({
                normalizeSurface,
                buildNodeLookup,
                toSelectionSet,
                toCollapsedSet,
                reconcileSelection,
                serializeState,
                applySelection,
                clearViewportStateCommit
            }),
            sceneLayout: Object.freeze({
                getVisibleNodes,
                getProjectedNodes,
                ensureLayoutPositions,
                getSceneBounds,
                cancelViewportAnimation,
                applySceneTransform,
                setPan,
                resize,
                fitView,
                focusNode,
                ensureNodeVisible,
                isNodeVisibleInViewport,
                setZoomPercent,
                setMenuScalePercent,
                worldToHostPoint,
                getNodePosition
            }),
            scenePatching: Object.freeze({
                renderGroupFrames,
                renderLinks,
                renderNodes,
                renderActiveDrag,
                scheduleNodeMeasurement
            }),
            overlayRenderer: Object.freeze({
                clearContextMenu,
                hidePopover,
                closeComposer,
                layoutComposer,
                clearSnapGuides,
                renderSnapGuides,
                renderConnectorAnchorOverlay,
                renderTransformHandlesOverlay,
                renderEmptyStateOverlay,
                renderDebugDecorations,
                renderDiagnosticsOverlay,
                renderMinimap
            }),
            runtime: Object.freeze({
                hydrateState,
                refresh,
                buildWorkbench,
                attachEvents,
                syncWorkbenchMode,
                setMaximized,
                exportImageData,
                disposeState: disposeWorkbenchState
            })
        });
    }

    function resolveWorkbenchState(host, options) {
        if (!host || typeof host !== "object") {
            return null;
        }

        const state = host.__canvasWorkbenchState || null;
        if (!state) {
            return null;
        }

        if (options?.requireConnected === false) {
            return state;
        }

        return state.host?.isConnected ? state : null;
    }

    function applyRequestedSelection(state, nodeIds, primaryNodeId) {
        const normalizedNodeIds = Array.isArray(nodeIds) ? nodeIds : [];
        const normalized = selectionModel.replace(
            normalizedNodeIds,
            primaryNodeId || (normalizedNodeIds[0] || null));
        workbenchInternals.stateStore.applySelection(state, normalized.selectedNodeIds, normalized.primaryNodeId);
    }

    function applyRenderOptions(state, options) {
        if (!state || !options) {
            return;
        }

        if (typeof options.isMaximized === "boolean") {
            workbenchInternals.runtime.setMaximized(state, options.isMaximized);
        }

        if (options.fitView === true) {
            workbenchInternals.sceneLayout.fitView(state);
        }

        if (Array.isArray(options.selectedNodeIds) && options.selectedNodeIds.length > 0) {
            applyRequestedSelection(state, options.selectedNodeIds, options.primaryNodeId || options.selectedNodeIds[0]);
        }
    }

    root.canvasWorkbench = {
        create(host, dotNetRef, surface, selectionDispatchSeed, stateDispatchSeed, options) {
            if (!host) {
                return false;
            }

            const previousState = resolveWorkbenchState(host, { requireConnected: false });
            if (previousState) {
                disposeWorkbenchState(previousState);
            }

            const state = workbenchInternals.runtime.hydrateState(
                host,
                dotNetRef,
                surface,
                selectionDispatchSeed,
                stateDispatchSeed);
            workbenchInternals.runtime.buildWorkbench(state);
            workbenchInternals.runtime.attachEvents(state);
            workbenchInternals.runtime.setMaximized(state, !!state.ui.isMaximized);
            if (typeof window.ResizeObserver === "function") {
                state.resizeObserver = new window.ResizeObserver(() => {
                    workbenchInternals.sceneLayout.resize(state);
                    render(state);
                });
                const resizeTargets = [host, state.shell, host.closest(".cw-stage-surface")]
                    .filter((target, index, collection) => !!target && collection.indexOf(target) === index);
                for (const target of resizeTargets) {
                    state.resizeObserver.observe(target);
                }
            }

            host.__canvasWorkbenchState = state;
            applyRenderOptions(state, options);
            render(state);
            return true;
        },
        update(host, surface, options) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return false;
            }

            workbenchInternals.runtime.refresh(state, surface);
            applyRenderOptions(state, options);
            return true;
        },
        fitView(host) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.fitView(state);
        },
        focusNode(host, nodeId) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.focusNode(state, nodeId);
        },
        async openNode(host, nodeId) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return false;
            }

            return await requestNodeOpen(state, nodeId);
        },
        openContextSubmenu(host, actionId) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return false;
            }

            return openContextSubmenuByActionId(state, actionId || "");
        },
        setZoomPercent(host, zoomPercent) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.setZoomPercent(state, zoomPercent);
        },
        setMenuScalePercent(host, menuScalePercent) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.setMenuScalePercent(state, menuScalePercent);
        },
        openCreateComposer(host, action, request) {
            const state = resolveWorkbenchState(host);
            if (!state || !action) {
                return;
            }

            openCreateComposer(state, action, request || {});
        },
        openQuickCreateMenu(host, anchorElement) {
            const state = resolveWorkbenchState(host);
            if (!state || !anchorElement) {
                return;
            }

            const sourceNode = resolveQuickCreateSourceNode(state);
            const rect = anchorElement.getBoundingClientRect();
            showContextMenu(state, {
                node: sourceNode,
                actions: state.surface.chrome.quickCreateActions || [],
                clientX: rect.left + (rect.width / 2),
                clientY: rect.top + (rect.height / 2),
                placementKind: sourceNode ? "child" : "canvas",
                label: sourceNode?.title || "Quick create"
            });
        },
        toggleMinimap(host) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            toggleMinimap(state);
        },
        toggleDiagnostics(host) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            toggleDiagnostics(state);
        },
        getState(host) {
            const state = resolveWorkbenchState(host, { requireConnected: false });
            return state ? workbenchInternals.stateStore.serializeState(state) : JSON.stringify({});
        },
        activateHotZone(host, request) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return false;
            }

            return activateHotZone(state, request || {});
        },
        getDiagnostics(host) {
            const state = resolveWorkbenchState(host, { requireConnected: false });
            return state
                ? workbenchInternals.instrumentation.buildDiagnosticsSnapshot(state, workbenchInternals.sceneLayout.getSceneBounds(state))
                : null;
        },
        getViewportSnapshot() {
            return {
                width: window.innerWidth || 0,
                height: window.innerHeight || 0
            };
        },
        selectNodes(host, nodeIds, primaryNodeId) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            applyRequestedSelection(state, nodeIds, primaryNodeId);
        },
        setMaximized(host, isMaximized) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            workbenchInternals.runtime.setMaximized(state, isMaximized);
        },
        resize(host) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.resize(state);
            render(state);
        },
        exportImageData(host) {
            return workbenchInternals.runtime.exportImageData(host);
        },
        simulateDrag(host, request) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return false;
            }

            return simulatePointerDrag(state, request || {});
        },
        finishInteraction(host) {
            const state = resolveWorkbenchState(host);
            if (!state) {
                return false;
            }

            return releaseSyntheticInteraction(state);
        },
        dispose(host) {
            if (!host) {
                return;
            }

            const state = resolveWorkbenchState(host, { requireConnected: false });
            if (!state) {
                return;
            }

            workbenchInternals.runtime.disposeState(state);
        }
    };

    root.canvasWorkbench.getSceneSnapshot = function (host) {
        const state = resolveWorkbenchState(host, { requireConnected: false });
        return state ? collectSceneSnapshot(state) : null;
    };

    root.canvasWorkbench.getHotZoneCenter = function (host, request) {
        const state = resolveWorkbenchState(host, { requireConnected: false });
        return state ? findSceneHotZoneCenter(state, request || {}) : null;
    };
    shared.workbenchInternals = createWorkbenchInternals();
    root.canvasWorkbenchInternals = shared.workbenchInternals;
    Object.assign(shared, { renderNodes, renderActiveDrag, renderSnapGuides, renderConnectorAnchorOverlay, renderTransformHandlesOverlay, renderDebugDecorations, renderMinimap, measureRenderedNodeSizes, scheduleNodeMeasurement, applySceneTransform, applyMarqueeSelection, buildDiagnosticsSnapshot, showPopover, openNodeMetadataMenu, resize, buildWorkbench, attachEvents, drawEmptyStateToExport, collectSceneSnapshot, findSceneHotZoneCenter, activateHotZone, createSyntheticPointerEvent, resolveSyntheticNodeDragStart, resolveSyntheticDragStart, simulatePointerDrag, releaseSyntheticInteraction, legacyDisposeWorkbenchState, disposeWorkbenchState, createWorkbenchInternals });
})();
