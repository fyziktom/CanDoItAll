(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 07-runtime-entry.js.'); }
    const workbenchInternals = new Proxy({}, {
        get(_target, property) {
            return shared.workbenchInternals?.[property];
        }
    });
    function finishCanvasInteraction(state) {
        const finishInteractionFn = shared.finishInteraction;
        if (typeof finishInteractionFn === "function") {
            return finishInteractionFn(state);
        }

        return Promise.resolve();
    }
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition, getLinkAnchorPoint, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, createMarkerBadge, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, writeClipboardText, copySelectionToClipboard, requestClipboardCut, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint, hitTestNode, hitTestFrameHandle, hitTestProgressBadge, isOverlayTarget, applyFullTextTooltip, reconcileSelection, applySelection, selectSingleNode, publishSelection, clearViewportStateCommit, createSerializedStateSnapshot, invokeStateChanged, publishState, publishStateNow, scheduleViewportStateCommit, publishNodesMoved, setSelection, toggleSelection, toggleCollapse, clearContextMenu, closeComposer, ensureHostFocus, deferHostFocus, resolveComposerAnchor, layoutComposer, render, getContextActions, isCreateAction, buildCreateRequest, resolveMenuLabel, getMenuScale, isCompactHiveLayout, resolveMenuActionVariant, getActionMetrics, applyProgressPresetTone, fitContextMenuLabel, resolveActionGlyph, createMenuActionIcon, resolveMenuActionAriaLabel, getRadialOffsets, buildCompactHiveCoordinates, getCompactHiveOffsets, resolveContextMenuOffsets, resolveContextMenuSafeTop, getContextMenuLayerBounds, clampLayerBoundsToHost, positionContextMenu, getContextMenuOrbitRadius, getContextMenuLocalPoint, isPointInContextMenuLayer, closeContextMenuLayersFrom, syncContextMenuLayers, resolveSubmenuOrigin, ensureSubmenuLoadingIndicator, clearSubmenuLoadingIndicator, cancelPendingContextSubmenu, scheduleContextSubmenuOpen, clampLayerOriginToHost, getToolboxPanelSize, getToolboxPanelBounds, clampToolboxPanelOriginToHost, resolveToolboxPanelOrigin, createContextMenuLayer, shiftContextMenuLayerOrigin, nudgeContextMenuLayerIntoVisibleHost, resolveQuickCreateSourceNode, submitCreateRequest, submitNodeEdit, readFileAsUpload, commitComposer, decorateComposerShell, createComposerWizard, createComposerSection, updateComposerFileState, openCreateComposer, openInlineNoteComposer, buildChildNotePlacement, buildSiblingNotePlacement, openKeyboardNoteComposer, openExistingNoteEditor, executeContextAction, openContextSubmenu, openContextSubmenuByActionId, renderContextMenuLayer, renderToolboxPreview, renderToolboxPanelLayer, showContextMenu, legacyOpenNodeMetadataMenu, startPan, isMarqueeModifierPressed, startMarquee, ensureSelectedForDrag, startDragForNodeIds, startDrag, startFrameDrag, updateMarquee, legacyApplyMarqueeSelection, updateDrag, updatePan, isNodeVisibleInViewport, centerNodeElementInViewport, ensureNodeVisible, legacyResize, findContainingBlockOverride, suspendContainingBlock, restoreContainingBlock, setMaximized, fitView, focusNode, normalizeWheelDelta, applyWheelZoom, setZoomPercent, setMenuScalePercent, toggleHelp, isManualDoubleActivation, handleNodeDoubleActivation, requestNodeOpen, legacyAttachEvents, hydrateState, refresh, getCanvasRuntimePrimitives, createFallbackHitRegistry, createCanvasHitRegistry, createCanvasSurfaceHost, destroyCanvasSurfaceHost, hexToRgba, resolveNodeAccentColor, resolveAnchorRect, buildRect, boundsToHitRect, projectSceneBounds, getNodeSceneBounds, clearSceneHotZones, registerSceneHotZone, getSceneHitAtPoint, getSceneHitAtEvent, resolveHitNode, clearScenePopoverHover, syncSceneHoverState, resolveCanvasNodeDetailMode, setCanvasFont, drawCanvasTextLines, drawRoundedPanel, requestSceneImage, buildCanvasSnapshotBounds, reconcileRetainedLayer, drawCanvasFrame, renderGroupFrames, drawCanvasLink, renderLinks, drawCanvasBadgePill, drawCanvasProgressBadge, drawCanvasAnnotationBadges, drawNodeMediaPreview, renderCanvasMicroNode, renderCanvasInlineTextNode, renderCanvasStandardNode } = shared;
    function renderNodes(state, visibleNodes) {
        const surface = state.nodeSurface;
        if (!surface) {
            return;
        }

        surface.clear();
        clearSceneHotZones(state);
        state.sceneGeometry = {
            nodes: new Map(),
            frames: state.sceneGeometry?.frames || new Map()
        };
        const detailMode = resolveCanvasNodeDetailMode(state, (visibleNodes || []).length);
        const nextEntries = new Map();
        let renderedNodeCount = 0;

        for (const node of visibleNodes || []) {
            const sceneBounds = getNodeSceneBounds(state, node);
            const hostBounds = projectSceneBounds(state, sceneBounds);
            if (hostBounds.width <= 0 || hostBounds.height <= 0) {
                continue;
            }

            registerSceneHotZone(state, hostBounds, {
                type: "node-body",
                nodeId: node.id
            });

            const accent = resolveNodeAccentColor(node);
            const meta = {
                selected: state.selectedIds.has(node.id),
                collapsed: state.collapsedIds.has(node.id),
                markerText: "",
                priorityText: "",
            progressTitle: "",
            hasPathButton: false,
            pathTitle: "",
            pathDisplayText: "",
            pathPromotedText: ""
        };
            if (detailMode === "micro") {
                renderCanvasMicroNode(surface.context, state, node, hostBounds, accent, meta);
            }
            else if (node.isInlineTextNode) {
                renderCanvasInlineTextNode(surface.context, state, node, hostBounds, accent, detailMode, meta);
            }
            else {
                renderCanvasStandardNode(surface.context, state, node, hostBounds, accent, detailMode, meta);
            }

            if (resolveSurfaceMode(state) === "delete" && state.hoveredDeleteNodeId === node.id) {
                drawRoundedPanel(
                    surface.context,
                    buildRect(hostBounds.left - 4, hostBounds.top - 4, hostBounds.width + 8, hostBounds.height + 8),
                    Math.max(16, 22 * state.ui.zoom),
                    "rgba(254, 226, 226, 0.12)",
                    "rgba(220, 38, 38, 0.92)",
                    Math.max(2, 3 * state.ui.zoom),
                    "");
            }

            state.sceneGeometry.nodes.set(node.id, buildCanvasSnapshotBounds(hostBounds, node, meta));
            nextEntries.set(node.id, {
                contentKey: getNodeRetainedContentKey(node, state.collapsedIds.has(node.id))
            });
            renderedNodeCount += 1;
        }

        reconcileRetainedLayer(
            state.retainedNodeElements,
            nextEntries,
            state.metrics,
            "nodeLayerRebuildCount",
            entry => entry.contentKey);

        if (state.metrics) {
            state.metrics.lastRenderedNodeCount = renderedNodeCount;
        }
    }

    function renderActiveDrag(state) {
        const dragContext = state?.interaction?.dragContext || buildActiveDragContext(state);
        if (!dragContext) {
            render(state);
            return;
        }

        state.interaction.dragContext = dragContext;
        const startedAt = now();
        if (state.metrics) {
            state.metrics.renderCount += 1;
            state.metrics.lastVisibleNodeCount = dragContext.projectedNodeCount;
            resetLastDragPatchMetrics(state.metrics);
        }

        const visibleNodes = getVisibleNodes(state);
        const projectedNodes = getProjectedNodes(state, visibleNodes);
        applySceneTransform(state);
        renderGroupFrames(state, projectedNodes);
        renderLinks(state, projectedNodes);
        renderSnapGuides(state);
        renderNodes(state, projectedNodes);
        renderConnectorAnchorOverlay(state, projectedNodes);
        renderTransformHandlesOverlay(state, projectedNodes);
        renderDebugDecorations(state, dragContext.dirtyDebugNodes);
        renderDiagnosticsOverlay(state, projectedNodes);
        renderMinimap(state, visibleNodes);
        layoutComposer(state);

        if (state.metrics) {
            recordDragPatchMetrics(
                state.metrics,
                dragContext.movedNodes.length,
                dragContext.dirtyLinks.length,
                dragContext.dirtyFrames.length);
            const elapsedMs = Math.max(0, now() - startedAt);
            state.metrics.totalRenderDurationMs += elapsedMs;
            state.metrics.lastRenderDurationMs = elapsedMs;
            state.metrics.maxRenderDurationMs = Math.max(state.metrics.maxRenderDurationMs, elapsedMs);
        }
    }

    function renderSnapGuides(state) {
        if (!state?.guideLayer) {
            return;
        }

        state.guideLayer.innerHTML = "";
        state.guideLayer.style.opacity = "1";
        if (state.surface?.chrome?.snapGuides?.isEnabled === false) {
            return;
        }

        if (!Array.isArray(state.snapGuides) || state.snapGuides.length === 0) {
            return;
        }

        const hostRect = state.host.getBoundingClientRect();
        for (const guide of state.snapGuides) {
            const element = createElement(state.document, "div", `cw-snap-guide is-${guide.orientation || "vertical"}`);
            if (guide.orientation === "horizontal") {
                const point = worldToHostPoint(state, { x: 0, y: guide.value });
                element.style.left = "0px";
                element.style.top = `${round(point.y)}px`;
                element.style.width = `${round(hostRect.width)}px`;
            }
            else {
                const point = worldToHostPoint(state, { x: guide.value, y: 0 });
                element.style.left = `${round(point.x)}px`;
                element.style.top = "0px";
                element.style.height = `${round(hostRect.height)}px`;
            }

            state.guideLayer.appendChild(element);
        }

        state.animationTimeline?.fadeElement?.("snap-guides", state.guideLayer, {
            from: 0.2,
            to: 1,
            durationMs: 160,
            easing: "cubicOut"
        });
    }

    function renderConnectorAnchorOverlay(state, visibleNodes) {
        if (!state?.anchorLayer) {
            return;
        }

        state.anchorLayer.innerHTML = "";
        state.anchorLayer.style.opacity = "1";
        if (resolveSurfaceMode(state) === "delete") {
            return;
        }
        const anchors = state.surface.chrome.connectorAnchors || {};
        if (!anchors.isEnabled) {
            return;
        }

        const visibleLookup = new Set((visibleNodes || []).map(node => node.id));
        const activeIds = new Set();
        if (anchors.showOnSelection) {
            for (const nodeId of state.selectedIds) {
                activeIds.add(nodeId);
            }
        }

        if (anchors.showOnHover && state.hoveredNodeId) {
            activeIds.add(state.hoveredNodeId);
        }

        if (activeIds.size === 0) {
            return;
        }

        for (const nodeId of activeIds) {
            if (!visibleLookup.has(nodeId)) {
                continue;
            }

            const node = state.lookups.byId.get(nodeId);
            if (!node) {
                continue;
            }

            const isPrimary = (state.ui.selectedNodeIds?.[0] || null) === nodeId;
            for (const point of getConnectorAnchorPoints(state, node, anchors.placementMode)) {
                const hostPoint = worldToHostPoint(state, point);
                const anchor = createElement(state.document, "div", `cw-connector-anchor is-${point.side}`);
                anchor.dataset.nodeId = nodeId;
                anchor.dataset.side = point.side;
                anchor.title = `${node.title || node.kind || "Node"} ${point.side} anchor`;
                if (isPrimary) {
                    anchor.classList.add("is-primary");
                }

                anchor.style.left = `${round(hostPoint.x)}px`;
                anchor.style.top = `${round(hostPoint.y)}px`;
                state.anchorLayer.appendChild(anchor);
            }
        }

        state.animationTimeline?.fadeElement?.("connector-anchors", state.anchorLayer, {
            from: 0.24,
            to: 1,
            durationMs: 160,
            easing: "cubicOut"
        });
    }

    function renderTransformHandlesOverlay(state, visibleNodes) {
        if (!state?.transformLayer) {
            return;
        }

        state.transformLayer.innerHTML = "";
        const handles = state.surface?.chrome?.transformHandles || {};
        if (!handles.isEnabled) {
            return;
        }

        const selectedNodes = (visibleNodes || []).filter(node => state.selectedIds.has(node.id));
        if (selectedNodes.length === 0) {
            return;
        }

        let minX = Number.POSITIVE_INFINITY;
        let minY = Number.POSITIVE_INFINITY;
        let maxX = Number.NEGATIVE_INFINITY;
        let maxY = Number.NEGATIVE_INFINITY;
        let isReadOnly = true;
        for (const node of selectedNodes) {
            const bounds = projectSceneBounds(state, getNodeSceneBounds(state, node));
            minX = Math.min(minX, bounds.left);
            minY = Math.min(minY, bounds.top);
            maxX = Math.max(maxX, bounds.right);
            maxY = Math.max(maxY, bounds.bottom);
            isReadOnly = isReadOnly && !!node.isReadOnly;
        }

        const frame = createElement(state.document, "div", "cw-transform-frame");
        frame.style.left = `${round(minX)}px`;
        frame.style.top = `${round(minY)}px`;
        frame.style.width = `${round(maxX - minX)}px`;
        frame.style.height = `${round(maxY - minY)}px`;
        frame.dataset.selectedCount = `${selectedNodes.length}`;
        if (isReadOnly) {
            frame.classList.add("is-read-only");
        }

        state.transformLayer.appendChild(frame);
        if (handles.showResizeHandles) {
            for (const position of ["nw", "n", "ne", "e", "se", "s", "sw", "w"]) {
                const handle = createElement(state.document, "div", `cw-transform-handle is-${position}`);
                if (isReadOnly) {
                    handle.classList.add("is-read-only");
                }

                handle.setAttribute("aria-hidden", "true");
                frame.appendChild(handle);
            }
        }

        if (handles.showRotateHandle) {
            const stem = createElement(state.document, "div", "cw-transform-rotate-stem");
            const rotate = createElement(state.document, "div", "cw-transform-rotate-handle");
            if (isReadOnly) {
                stem.classList.add("is-read-only");
                rotate.classList.add("is-read-only");
            }

            frame.appendChild(stem);
            frame.appendChild(rotate);
        }
    }

    function renderDebugDecorations(state, visibleNodes) {
        if (!state?.debugLayer) {
            return;
        }

        state.debugLayer.innerHTML = "";
        const diagnostics = state.surface.chrome.diagnostics || {};
        const enabled = diagnostics.isEnabled && state.ui.showDiagnostics;
        if (!enabled) {
            return;
        }

        if (diagnostics.showNodeBounds) {
            for (const node of visibleNodes || []) {
                const bounds = projectSceneBounds(state, getNodeSceneBounds(state, node));
                const element = createElement(state.document, "div", "cw-debug-bounds");
                element.style.left = `${round(bounds.left)}px`;
                element.style.top = `${round(bounds.top)}px`;
                element.style.width = `${round(bounds.width)}px`;
                element.style.height = `${round(bounds.height)}px`;
                state.debugLayer.appendChild(element);
            }
        }

        if (diagnostics.showConnectorAnchors) {
            const visibleLookup = new Set((visibleNodes || []).map(node => node.id));
            for (const link of state.surface.links) {
                if (!visibleLookup.has(link.sourceId) || !visibleLookup.has(link.targetId)) {
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
                for (const point of [getLinkAnchorPoint(state, source, sourceSide), getLinkAnchorPoint(state, target, targetSide)]) {
                    const hostPoint = worldToHostPoint(state, point);
                    const dot = createElement(state.document, "div", "cw-debug-anchor");
                    dot.style.left = `${round(hostPoint.x)}px`;
                    dot.style.top = `${round(hostPoint.y)}px`;
                    state.debugLayer.appendChild(dot);
                }
            }
        }
    }

    function renderMinimap(state, visibleNodes) {
        if (!state?.minimapShell || !state?.minimapCanvas || !state?.minimapSurface) {
            return;
        }

        const minimap = state.surface.chrome.minimap || {};
        const enabled = minimap.isEnabled && state.ui.showMinimap !== false && visibleNodes.length > 0;
        state.minimapShell.style.display = enabled ? "grid" : "none";
        if (!enabled) {
            state.minimapMetrics = null;
            return;
        }

        const surface = state.minimapSurface;
        surface.measure?.();
        surface.clear("rgba(248, 250, 252, 0.98)");
        state.minimapTitle.textContent = minimap.title || "Scene overview";

        const bounds = getSceneBounds(state) || { minX: 0, maxX: 320, minY: 0, maxY: 240 };
        const width = surface.size.width;
        const height = surface.size.height;
        const padding = 12;
        const sceneWidth = Math.max(bounds.maxX - bounds.minX, 240);
        const sceneHeight = Math.max(bounds.maxY - bounds.minY, 180);
        const scale = Math.min((width - (padding * 2)) / sceneWidth, (height - (padding * 2)) / sceneHeight);
        const offsetX = (width - (sceneWidth * scale)) / 2;
        const offsetY = (height - (sceneHeight * scale)) / 2;

        state.minimapMetrics = {
            width,
            height,
            padding,
            scale,
            offsetX,
            offsetY,
            bounds,
            nodeCount: visibleNodes.length
        };

        const context = surface.context;
        drawRoundedPanel(
            context,
            buildRect(0, 0, width, height),
            16,
            "rgba(226, 232, 240, 0.68)",
            "rgba(148, 163, 184, 0.18)",
            1,
            "");

        for (const node of visibleNodes) {
            const position = getNodePosition(state, node);
            const size = getNodeSize(state, node);
            const rect = buildRect(
                offsetX + ((position.x - (size.width / 2) - bounds.minX) * scale),
                offsetY + ((position.y - (size.height / 2) - bounds.minY) * scale),
                Math.max(4, size.width * scale),
                Math.max(4, size.height * scale));
            drawRoundedPanel(
                context,
                rect,
                node.family === "root" ? 5 : 3,
                state.selectedIds.has(node.id) ? "rgba(124, 58, 237, 0.72)" : "rgba(148, 163, 184, 0.68)",
                state.selectedIds.has(node.id) ? "rgba(91, 33, 182, 0.92)" : "rgba(71, 85, 105, 0.42)",
                1,
                "");
        }

        const hostRect = state.host.getBoundingClientRect();
        const viewportBounds = buildRect(
            offsetX + ((((0 - state.ui.panX) / state.ui.zoom) - bounds.minX) * scale),
            offsetY + ((((0 - state.ui.panY) / state.ui.zoom) - bounds.minY) * scale),
            Math.max(12, (hostRect.width / state.ui.zoom) * scale),
            Math.max(12, (hostRect.height / state.ui.zoom) * scale));
        context.save();
        context.fillStyle = "rgba(14, 165, 233, 0.16)";
        context.strokeStyle = "rgba(2, 132, 199, 0.92)";
        context.lineWidth = 1.5;
        context.beginPath();
        context.roundRect(viewportBounds.left, viewportBounds.top, viewportBounds.width, viewportBounds.height, 8);
        context.fill();
        context.stroke();
        context.restore();
    }

    function measureRenderedNodeSizes() {
        return false;
    }

    function scheduleNodeMeasurement() {
    }

    function applySceneTransform(state) {
        if (state?.scene) {
            state.scene.style.transform = "none";
        }
    }

    function applyMarqueeSelection(state) {
        const marqueeRect = state.marquee.getBoundingClientRect();
        const selectionMode = state.surface?.chrome?.marqueeSelection?.selectionMode || "intersect";
        const selected = [];
        for (const node of state.sceneGeometry?.nodes?.values?.() || []) {
            const intersects = selectionMode === "contain"
                ? node.left >= marqueeRect.left &&
                node.right <= marqueeRect.right &&
                node.top >= marqueeRect.top &&
                node.bottom <= marqueeRect.bottom
                : node.left < marqueeRect.right &&
                node.right > marqueeRect.left &&
                node.top < marqueeRect.bottom &&
                node.bottom > marqueeRect.top;
            if (intersects) {
                selected.push(node.id);
            }
        }

        state.marquee.style.display = "none";
        setSelection(state, selected, true);
    }

    function buildDiagnosticsSnapshot(state, bounds) {
        return {
            isVisible: !!(state?.surface?.chrome?.diagnostics?.isEnabled && state?.ui?.showDiagnostics),
            rendererMode: "canvas",
            visibleNodeCount: state?.metrics?.lastVisibleNodeCount || 0,
            totalNodeCount: state?.surface?.nodes?.length || 0,
            totalLinkCount: state?.surface?.links?.length || 0,
            selectedCount: state?.selectedIds?.size || 0,
            interaction: state?.interaction?.kind || "idle",
            zoomPercent: Math.round((state?.ui?.zoom || 1) * 100),
            panX: round(state?.ui?.panX || 0),
            panY: round(state?.ui?.panY || 0),
            bounds: bounds
                ? {
                    minX: round(bounds.minX),
                    minY: round(bounds.minY),
                    maxX: round(bounds.maxX),
                    maxY: round(bounds.maxY)
                }
                : null,
            metrics: cloneWorkbenchMetrics(state?.metrics),
            canvasLayers: {
                frames: state?.frameSurface?.size || null,
                links: state?.linkSurface?.size || null,
                nodes: state?.nodeSurface?.size || null,
                minimap: state?.minimapSurface?.size || null
            }
        };
    }

    function showPopover(state, anchor, annotation) {
        if (!state?.popover || !anchor || !annotation) {
            return;
        }

        if (state.surface?.chrome?.tooltipPopover?.isEnabled === false) {
            return;
        }

        const anchorRect = resolveAnchorRect(anchor);
        if (!anchorRect) {
            return;
        }

        state.popover.dataset.kind = annotation.kind || "info";
        state.popover.dataset.tone = annotation.tone || "accent";
        state.popoverTitle.textContent = annotation.label || annotation.kind || "Signal";
        state.popoverBody.textContent = annotation.description || annotation.label || "Shared workbench signal";
        state.popover.style.display = "grid";
        state.popoverAnchor = anchorRect;
        positionFloatingOverlayWithinHost(state, state.popover, anchorRect);
    }

    function openNodeMetadataMenu(state, node, actionId, anchor) {
        if (!node || !anchor) {
            return;
        }

        const rect = resolveAnchorRect(anchor);
        if (!rect) {
            return;
        }

        if (state.selectedIds.size !== 1 || !state.selectedIds.has(node.id)) {
            setSelection(state, [node.id], true);
        }

        showContextMenu(state, {
            node,
            clientX: rect.left + (rect.width / 2),
            clientY: rect.top + (rect.height / 2),
            placementKind: "child",
            label: node.title || "Canvas"
        });
        openContextSubmenuByActionId(state, actionId);
    }

    async function copyCompactPath(state, button, compactPath) {
        if (!compactPath?.fullPath) {
            return;
        }

        const didCopy = await writeClipboardText(compactPath.fullPath);
        if (!didCopy) {
            showStatusNotice(state, "Clipboard access is unavailable for this path", "warn");
            return;
        }

        if (state.pathCopyState?.timerHandle) {
            window.clearTimeout(state.pathCopyState.timerHandle);
        }

        state.pathCopyState = {
            nodeId: state.pathCopyState?.nodeId || "",
            timerHandle: 0
        };
        if (button?.closest) {
            const nodeElement = button.closest(".cw-node");
            state.pathCopyState.nodeId = nodeElement?.dataset?.nodeId || state.pathCopyState.nodeId || "";
        }

        render(state);
        showStatusNotice(state, `${compactPath.label || "Path"} copied`, "success");
        state.pathCopyState.timerHandle = window.setTimeout(() => {
            state.pathCopyState = null;
            render(state);
        }, 2000);
    }

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

    function resolveSurfaceMode(state) {
        return state?.surface?.mode || "authoring";
    }

    function isDeleteMode(state) {
        return resolveSurfaceMode(state) === "delete";
    }

    function isDependencyMode(state) {
        return resolveSurfaceMode(state) === "dependency";
    }

    function syncWorkbenchMode(state) {
        if (!state?.host?.dataset) {
            return;
        }

        state.host.dataset.workbenchMode = resolveSurfaceMode(state);
    }

    function setWorkbenchToolMode(state, mode) {
        if (!state?.surface) {
            return;
        }

        const normalizedMode = mode === "delete" || mode === "dependency"
            ? mode
            : "authoring";
        state.surface.mode = normalizedMode;
        if (normalizedMode !== "dependency") {
            state.surface.dependencySourceId = "";
        }

        if (normalizedMode !== "delete") {
            state.hoveredDeleteNodeId = null;
            state.hoveredDeleteLinkKey = null;
        }

        syncWorkbenchMode(state);
    }

    function updatePointerHostPoint(state, event) {
        const hostRect = state.host?.getBoundingClientRect?.();
        if (!hostRect) {
            state.pointerHostPoint = null;
            return null;
        }

        const isWithinHost = event.clientX >= hostRect.left &&
            event.clientX <= hostRect.right &&
            event.clientY >= hostRect.top &&
            event.clientY <= hostRect.bottom;
        state.pointerHostPoint = isWithinHost
            ? getHostPoint(state, event.clientX, event.clientY)
            : null;
        return state.pointerHostPoint;
    }

    function dispatchContextActionRequest(state, request) {
        if (!state?.dotNetRef?.invokeMethodAsync) {
            return;
        }

        void state.dotNetRef.invokeMethodAsync("OnContextActionRequest", JSON.stringify(request));
    }

    function distancePointToSegment(pointX, pointY, startX, startY, endX, endY) {
        const deltaX = endX - startX;
        const deltaY = endY - startY;
        if (Math.abs(deltaX) <= 0.001 && Math.abs(deltaY) <= 0.001) {
            return Math.hypot(pointX - startX, pointY - startY);
        }

        const segmentLengthSquared = (deltaX * deltaX) + (deltaY * deltaY);
        const projected = clamp(
            (((pointX - startX) * deltaX) + ((pointY - startY) * deltaY)) / segmentLengthSquared,
            0,
            1);
        const projectionX = startX + (projected * deltaX);
        const projectionY = startY + (projected * deltaY);
        return Math.hypot(pointX - projectionX, pointY - projectionY);
    }

    function cubicBezierPoint(link, t) {
        const inverse = 1 - t;
        const inverseSquared = inverse * inverse;
        const inverseCubed = inverseSquared * inverse;
        const tSquared = t * t;
        const tCubed = tSquared * t;
        return {
            x: (inverseCubed * link.startPoint.x) +
                (3 * inverseSquared * t * link.controlPoint1.x) +
                (3 * inverse * tSquared * link.controlPoint2.x) +
                (tCubed * link.endPoint.x),
            y: (inverseCubed * link.startPoint.y) +
                (3 * inverseSquared * t * link.controlPoint1.y) +
                (3 * inverse * tSquared * link.controlPoint2.y) +
                (tCubed * link.endPoint.y)
        };
    }

    function isPointNearRenderedLink(link, pointX, pointY) {
        if (!link?.bounds) {
            return false;
        }

        const margin = 12;
        if (pointX < (link.bounds.left - margin) ||
            pointX > (link.bounds.right + margin) ||
            pointY < (link.bounds.top - margin) ||
            pointY > (link.bounds.bottom + margin)) {
            return false;
        }

        let previous = link.startPoint;
        const segments = 18;
        for (let index = 1; index <= segments; index += 1) {
            const current = cubicBezierPoint(link, index / segments);
            if (distancePointToSegment(pointX, pointY, previous.x, previous.y, current.x, current.y) <= 10) {
                return true;
            }

            previous = current;
        }

        return false;
    }

    function hitTestRenderedLink(state, pointX, pointY) {
        const renderedLinks = state.renderedLinks || [];
        for (let index = renderedLinks.length - 1; index >= 0; index -= 1) {
            const link = renderedLinks[index];
            if (isPointNearRenderedLink(link, pointX, pointY)) {
                return link;
            }
        }

        return null;
    }

    function resolveDeleteModeHitTarget(state, event) {
        const point = updatePointerHostPoint(state, event);
        if (!point) {
            return null;
        }

        const sceneHit = getSceneHitAtPoint(state, point.x, point.y);
        const targetNode = resolveHitNode(state, sceneHit);
        if (targetNode) {
            return {
                targetKind: "node",
                nodeId: targetNode.id
            };
        }

        const targetLink = hitTestRenderedLink(state, point.x, point.y);
        if (targetLink) {
            return {
                targetKind: "link",
                link: targetLink
            };
        }

        return null;
    }

    function updateDeleteHoverState(state, event) {
        const hitTarget = resolveDeleteModeHitTarget(state, event);
        const nextNodeId = hitTarget?.targetKind === "node" ? hitTarget.nodeId : null;
        const nextLinkKey = hitTarget?.targetKind === "link" ? hitTarget.link.key : null;
        if ((state.hoveredDeleteNodeId || null) === nextNodeId &&
            (state.hoveredDeleteLinkKey || null) === nextLinkKey) {
            return hitTarget;
        }

        state.hoveredDeleteNodeId = nextNodeId;
        state.hoveredDeleteLinkKey = nextLinkKey;
        clearScenePopoverHover(state);
        render(state);
        return hitTarget;
    }

    function attachEvents(state) {
        state.handlers = {
            pointerDown: event => {
                if (isOverlayTarget(event.target)) {
                    return;
                }

                if (state.composer) {
                    closeComposer(state, { focusHost: false });
                }

                clearContextMenu(state);
                cancelViewportAnimation(state);
                ensureHostFocus(state);
                deferHostFocus(state);
                updatePointerHostPoint(state, event);

                if (event.button === 2) {
                    return;
                }

                if (event.button === 1) {
                    startPan(state, event);
                    return;
                }

                const hitTarget = getSceneHitAtEvent(state, event);
                if (isDeleteMode(state) && event.button === 0) {
                    const deleteTarget = updateDeleteHoverState(state, event);
                    if (deleteTarget?.targetKind === "node") {
                        dispatchContextActionRequest(state, {
                            nodeId: deleteTarget.nodeId,
                            actionId: "delete",
                            x: 0,
                            y: 0,
                            targetKind: "node"
                        });
                        return;
                    }

                    if (deleteTarget?.targetKind === "link") {
                        dispatchContextActionRequest(state, {
                            nodeId: deleteTarget.link.targetId,
                            actionId: "delete-link",
                            x: 0,
                            y: 0,
                            targetKind: "link",
                            linkSourceId: deleteTarget.link.sourceId,
                            linkTargetId: deleteTarget.link.targetId,
                            linkKind: deleteTarget.link.kind
                        });
                        return;
                    }
                }

                if (hitTarget?.type === "node-path" && event.button === 0) {
                    state.pathCopyState = {
                        nodeId: hitTarget.nodeId,
                        timerHandle: state.pathCopyState?.timerHandle || 0
                    };
                    void copyCompactPath(state, null, hitTarget.compactPath);
                    return;
                }

                if (hitTarget?.type === "annotation" && event.button === 0) {
                    const node = resolveHitNode(state, hitTarget);
                    if (node) {
                        hidePopover(state);
                        invokeAnnotationAction(state, node, hitTarget.annotation);
                    }
                    return;
                }

                if (hitTarget?.type === "node-collapse" && event.button === 0) {
                    toggleCollapse(state, hitTarget.nodeId);
                    return;
                }

                if (hitTarget?.type === "frame-handle") {
                    startFrameDrag(state, event, hitTarget.frameId);
                    return;
                }

                const targetNode = resolveHitNode(state, hitTarget);
                if (isMarqueeModifierPressed(state, event)) {
                    startMarquee(state, event);
                    return;
                }

                if (targetNode) {
                    const dependencySourceId = state.surface?.dependencySourceId || "";
                    if (isDependencyMode(state) &&
                        event.button === 0 &&
                        dependencySourceId &&
                        dependencySourceId !== targetNode.id) {
                        startDragForNodeIds(state, event, [targetNode.id], {
                            kind: "dependency-drag",
                            sourceNodeId: dependencySourceId,
                            targetNodeId: targetNode.id
                        });
                        return;
                    }

                    const isMultiToggle = (event.ctrlKey || event.metaKey) && event.shiftKey;
                    if (event.button === 0 &&
                        !event.altKey &&
                        !event.ctrlKey &&
                        !event.metaKey &&
                        isManualDoubleActivation(state, targetNode.id)) {
                        if (hitTarget?.type === "node-progress") {
                            state.recentDoubleActivationAt = Date.now();
                            openNodeMetadataMenu(state, targetNode, "progress", resolveAnchorRect(hitTarget.bounds));
                            return;
                        }

                        handleNodeDoubleActivation(state, targetNode);
                        return;
                    }

                    if (isMultiToggle) {
                        toggleSelection(state, targetNode.id);
                        return;
                    }

                    const isGroupDrag = (event.ctrlKey || event.metaKey) &&
                        state.selectedIds.size > 1 &&
                        state.selectedIds.has(targetNode.id);
                    if (!state.selectedIds.has(targetNode.id) || (state.selectedIds.size > 1 && !isGroupDrag)) {
                        selectSingleNode(state, targetNode.id);
                    }

                    startDrag(state, event, targetNode.id);
                    return;
                }

                startPan(state, event);
            },
            pointerMove: event => {
                updatePointerHostPoint(state, event);
                if (!state.interaction) {
                    syncContextMenuLayers(state, event);
                    if (isDeleteMode(state)) {
                        updateDeleteHoverState(state, event);
                        return;
                    }

                    if (!isOverlayTarget(event.target)) {
                        syncSceneHoverState(state, event);
                        if (isDependencyMode(state)) {
                            render(state);
                        }
                    }
                    return;
                }

                switch (state.interaction.kind) {
                    case "drag":
                    case "frame-drag":
                    case "dependency-drag":
                        updateDrag(state, event);
                        break;
                    case "pan":
                        updatePan(state, event);
                        break;
                    case "marquee":
                        updateMarquee(state, event);
                        break;
                }
            },
            pointerUp: () => {
                clearScenePopoverHover(state);
                void finishCanvasInteraction(state);
            },
            blur: () => {
                state.pointerHostPoint = null;
                state.hoveredDeleteNodeId = null;
                state.hoveredDeleteLinkKey = null;
                state.hoveredNodeId = null;
                clearScenePopoverHover(state);
                void finishCanvasInteraction(state);
                render(state);
            },
            doubleClick: event => {
                if (state.recentDoubleActivationAt && (Date.now() - state.recentDoubleActivationAt) <= 340) {
                    return;
                }

                if (isOverlayTarget(event.target)) {
                    return;
                }

                const hitTarget = getSceneHitAtEvent(state, event);
                const targetNode = resolveHitNode(state, hitTarget);
                if (!targetNode) {
                    return;
                }

                if (hitTarget?.type === "node-progress") {
                    openNodeMetadataMenu(state, targetNode, "progress", resolveAnchorRect(hitTarget.bounds));
                    return;
                }

                handleNodeDoubleActivation(state, targetNode);
            },
            wheel: event => {
                event.preventDefault();
                applyWheelZoom(state, event);
            },
            contextMenu: event => {
                if (isOverlayTarget(event.target)) {
                    return;
                }

                event.preventDefault();
                const hitTarget = getSceneHitAtEvent(state, event);
                const targetNode = resolveHitNode(state, hitTarget);
                const isGroupSelection = !!targetNode &&
                    state.selectedIds.size > 1 &&
                    state.selectedIds.has(targetNode.id);
                if (targetNode && !isGroupSelection) {
                    setSelection(state, [targetNode.id], true);
                }

                showContextMenu(state, {
                    node: targetNode,
                    clientX: event.clientX,
                    clientY: event.clientY,
                    placementKind: targetNode ? "child" : "canvas",
                    label: isGroupSelection ? `${state.selectedIds.size} selected` : undefined
                });
            },
            keyDown: event => {
                const target = event.target;
                const tagName = target?.tagName?.toLowerCase?.() || "";
                const isWorkbenchKeyTarget = !target ||
                    target === state.document.body ||
                    target === state.document.documentElement ||
                    state.host.contains(target) ||
                    !!target?.closest?.(".cw-workbench-shell");

                const isEditable = tagName === "input" || tagName === "textarea" || target?.isContentEditable;
                if (isEditable) {
                    if (event.key === "Escape") {
                        event.preventDefault();
                        closeComposer(state);
                        if (resolveSurfaceMode(state) !== "authoring") {
                            setWorkbenchToolMode(state, "authoring");
                            dispatchContextActionRequest(state, {
                                nodeId: null,
                                actionId: "tool-mode:select",
                                x: 0,
                                y: 0,
                                targetKind: "canvas"
                            });
                        }

                        render(state);
                        ensureHostFocus(state);
                    }

                    return;
                }

                if (!isWorkbenchKeyTarget) {
                    return;
                }

                if (shared.routeContextMenuShortcut?.(state, event)) {
                    return;
                }

                const lowerKey = (event.key || "").toLowerCase();
                const usesCommandModifier = event.ctrlKey || event.metaKey;
                if (usesCommandModifier && !event.altKey) {
                    switch (lowerKey) {
                        case "x":
                            event.preventDefault();
                            requestClipboardCut(state);
                            return;
                        case "c":
                            event.preventDefault();
                            copySelectionToClipboard(state);
                            return;
                        case "v":
                            event.preventDefault();
                            void requestClipboardPaste(state);
                            return;
                        case "d":
                            event.preventDefault();
                            requestClipboardDuplicate(state);
                            return;
                    }
                }

                if (event.key === "Tab" && !event.shiftKey && !event.ctrlKey && !event.metaKey && !event.altKey) {
                    event.preventDefault();
                    openKeyboardNoteComposer(state, "child");
                    return;
                }

                if (event.key === "Enter" && !event.shiftKey && !event.ctrlKey && !event.metaKey && !event.altKey) {
                    event.preventDefault();
                    openKeyboardNoteComposer(state, "sibling");
                    return;
                }

                switch (event.key) {
                    case "+":
                    case "=":
                        event.preventDefault();
                        setZoomPercent(state, (state.ui.zoom * 100) + 10);
                        break;
                    case "-":
                        event.preventDefault();
                        setZoomPercent(state, (state.ui.zoom * 100) - 10);
                        break;
                    case "0":
                        event.preventDefault();
                        fitView(state);
                        break;
                    case "?":
                    case "h":
                    case "H":
                        event.preventDefault();
                        toggleHelp(state);
                        break;
                    case "d":
                    case "D":
                        event.preventDefault();
                        toggleDiagnostics(state);
                        break;
                    case "m":
                    case "M":
                        event.preventDefault();
                        toggleMinimap(state);
                        break;
                    case "Escape":
                        event.preventDefault();
                        {
                            const activeMode = resolveSurfaceMode(state);
                            const hadContextMenu = state.contextMenu?.style.display !== "none";
                            const hadComposer = !!state.composer;
                            clearContextMenu(state);
                            closeComposer(state);
                            if (activeMode !== "authoring") {
                                setWorkbenchToolMode(state, "authoring");
                                render(state);
                                ensureHostFocus(state);
                                dispatchContextActionRequest(state, {
                                    nodeId: null,
                                    actionId: "tool-mode:select",
                                    x: 0,
                                    y: 0,
                                    targetKind: "canvas"
                                });
                            }
                            else if (!hadContextMenu && !hadComposer) {
                                setSelection(state, [], true);
                            }
                            else {
                                render(state);
                                ensureHostFocus(state);
                            }
                        }
                        break;
                }
            }
        };

        state.host.addEventListener("pointerdown", state.handlers.pointerDown);
        window.addEventListener("pointermove", state.handlers.pointerMove);
        window.addEventListener("pointerup", state.handlers.pointerUp);
        window.addEventListener("blur", state.handlers.blur);
        state.host.addEventListener("dblclick", state.handlers.doubleClick);
        state.host.addEventListener("wheel", state.handlers.wheel, { passive: false });
        state.host.addEventListener("contextmenu", state.handlers.contextMenu);
        state.document.addEventListener("keydown", state.handlers.keyDown);
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

            hidePopover(state);
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

    function legacyDisposeWorkbenchState(state) {
        if (!state) {
            return;
        }

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

        if (state.statusNoticeTimer) {
            window.clearTimeout(state.statusNoticeTimer);
            state.statusNoticeTimer = 0;
        }

        if (state.focusRecenterTimer) {
            window.clearTimeout(state.focusRecenterTimer);
            state.focusRecenterTimer = 0;
        }

        workbenchInternals.stateStore.clearViewportStateCommit(state);
        state.publishStateDebounced.cancel?.();
        if (state.animationTimeline) {
            state.animationTimeline.dispose();
        }

        if (state.handlers) {
            state.host.removeEventListener("pointerdown", state.handlers.pointerDown);
            window.removeEventListener("pointermove", state.handlers.pointerMove);
            window.removeEventListener("pointerup", state.handlers.pointerUp);
            window.removeEventListener("blur", state.handlers.blur);
            state.host.removeEventListener("dblclick", state.handlers.doubleClick);
            state.host.removeEventListener("wheel", state.handlers.wheel);
            state.host.removeEventListener("contextmenu", state.handlers.contextMenu);
            state.document.removeEventListener("keydown", state.handlers.keyDown);
        }

        workbenchInternals.runtime.setMaximized(state, false);
        clear(state.host);
        delete state.host.__canvasWorkbenchState;
    }

    function disposeWorkbenchState(state) {
        if (!state) {
            return;
        }

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

        if (state.statusNoticeTimer) {
            window.clearTimeout(state.statusNoticeTimer);
            state.statusNoticeTimer = 0;
        }

        workbenchInternals.stateStore.clearViewportStateCommit(state);
        state.publishStateDebounced.cancel?.();

        if (state.animationTimeline) {
            state.animationTimeline.dispose();
        }

        if (state.handlers) {
            state.host.removeEventListener("pointerdown", state.handlers.pointerDown);
            window.removeEventListener("pointermove", state.handlers.pointerMove);
            window.removeEventListener("pointerup", state.handlers.pointerUp);
            window.removeEventListener("blur", state.handlers.blur);
            state.host.removeEventListener("dblclick", state.handlers.doubleClick);
            state.host.removeEventListener("wheel", state.handlers.wheel);
            state.host.removeEventListener("contextmenu", state.handlers.contextMenu);
            state.document.removeEventListener("keydown", state.handlers.keyDown);
        }

        workbenchInternals.runtime.setMaximized(state, false);
        clear(state.host);
        delete state.host.__canvasWorkbenchState;
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

    root.canvasWorkbench = {
        create(host, dotNetRef, surface, selectionDispatchSeed, stateDispatchSeed) {
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
            render(state);
        },
        update(host, surface) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            workbenchInternals.runtime.refresh(state, surface);
        },
        fitView(host) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.fitView(state);
        },
        focusNode(host, nodeId) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.focusNode(state, nodeId);
        },
        async openNode(host, nodeId) {
            const state = host?.__canvasWorkbenchState;
            if (!state) {
                return false;
            }

            return await requestNodeOpen(state, nodeId);
        },
        openContextSubmenu(host, actionId) {
            const state = host?.__canvasWorkbenchState;
            if (!state) {
                return false;
            }

            return openContextSubmenuByActionId(state, actionId || "");
        },
        setZoomPercent(host, zoomPercent) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.setZoomPercent(state, zoomPercent);
        },
        setMenuScalePercent(host, menuScalePercent) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            workbenchInternals.sceneLayout.setMenuScalePercent(state, menuScalePercent);
        },
        openCreateComposer(host, action, request) {
            const state = host.__canvasWorkbenchState;
            if (!state || !action) {
                return;
            }

            openCreateComposer(state, action, request || {});
        },
        openQuickCreateMenu(host, anchorElement) {
            const state = host.__canvasWorkbenchState;
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
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            toggleMinimap(state);
        },
        toggleDiagnostics(host) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            toggleDiagnostics(state);
        },
        getState(host) {
            const state = host.__canvasWorkbenchState;
            return state ? workbenchInternals.stateStore.serializeState(state) : JSON.stringify({});
        },
        activateHotZone(host, request) {
            const state = host?.__canvasWorkbenchState;
            if (!state) {
                return false;
            }

            return activateHotZone(state, request || {});
        },
        getDiagnostics(host) {
            const state = host.__canvasWorkbenchState;
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
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            const normalized = selectionModel.replace(nodeIds, primaryNodeId || (Array.isArray(nodeIds) ? nodeIds[0] : null));
            workbenchInternals.stateStore.applySelection(state, normalized.selectedNodeIds, normalized.primaryNodeId);
        },
        setMaximized(host, isMaximized) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            workbenchInternals.runtime.setMaximized(state, isMaximized);
        },
        resize(host) {
            const state = host.__canvasWorkbenchState;
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
            const state = host?.__canvasWorkbenchState;
            if (!state) {
                return false;
            }

            return simulatePointerDrag(state, request || {});
        },
        finishInteraction(host) {
            const state = host?.__canvasWorkbenchState;
            if (!state) {
                return false;
            }

            return releaseSyntheticInteraction(state);
        },
        dispose(host) {
            if (!host) {
                return;
            }

            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            workbenchInternals.runtime.disposeState(state);
        }
    };

    root.canvasWorkbench.getSceneSnapshot = function (host) {
        const state = host?.__canvasWorkbenchState;
        return state ? collectSceneSnapshot(state) : null;
    };

    root.canvasWorkbench.getHotZoneCenter = function (host, request) {
        const state = host?.__canvasWorkbenchState;
        return state ? findSceneHotZoneCenter(state, request || {}) : null;
    };
    shared.workbenchInternals = createWorkbenchInternals();
    root.canvasWorkbenchInternals = shared.workbenchInternals;
    Object.assign(shared, { renderNodes, renderActiveDrag, renderSnapGuides, renderConnectorAnchorOverlay, renderTransformHandlesOverlay, renderDebugDecorations, renderMinimap, measureRenderedNodeSizes, scheduleNodeMeasurement, applySceneTransform, applyMarqueeSelection, buildDiagnosticsSnapshot, showPopover, openNodeMetadataMenu, resize, buildWorkbench, attachEvents, drawEmptyStateToExport, collectSceneSnapshot, findSceneHotZoneCenter, activateHotZone, createSyntheticPointerEvent, resolveSyntheticNodeDragStart, resolveSyntheticDragStart, simulatePointerDrag, releaseSyntheticInteraction, legacyDisposeWorkbenchState, disposeWorkbenchState, createWorkbenchInternals });
})();
