(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 07b-runtime-rendering.js.'); }
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition, getLinkAnchorPoint, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, createMarkerBadge, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, writeClipboardText, copySelectionToClipboard, requestClipboardCut, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint, hitTestNode, hitTestFrameHandle, hitTestProgressBadge, isOverlayTarget, applyFullTextTooltip, reconcileSelection, applySelection, selectSingleNode, publishSelection, clearViewportStateCommit, createSerializedStateSnapshot, invokeStateChanged, publishState, publishStateNow, scheduleViewportStateCommit, publishNodesMoved, setSelection, toggleSelection, toggleCollapse, clearContextMenu, closeComposer, ensureHostFocus, deferHostFocus, resolveComposerAnchor, layoutComposer, render, getContextActions, isCreateAction, buildCreateRequest, resolveMenuLabel, getMenuScale, isCompactHiveLayout, resolveMenuActionVariant, getActionMetrics, applyProgressPresetTone, fitContextMenuLabel, resolveActionGlyph, createMenuActionIcon, resolveMenuActionAriaLabel, getRadialOffsets, buildCompactHiveCoordinates, getCompactHiveOffsets, resolveContextMenuOffsets, resolveContextMenuSafeTop, getContextMenuLayerBounds, clampLayerBoundsToHost, positionContextMenu, getContextMenuOrbitRadius, getContextMenuLocalPoint, isPointInContextMenuLayer, closeContextMenuLayersFrom, syncContextMenuLayers, resolveSubmenuOrigin, ensureSubmenuLoadingIndicator, clearSubmenuLoadingIndicator, cancelPendingContextSubmenu, scheduleContextSubmenuOpen, clampLayerOriginToHost, getToolboxPanelSize, getToolboxPanelBounds, clampToolboxPanelOriginToHost, resolveToolboxPanelOrigin, createContextMenuLayer, shiftContextMenuLayerOrigin, nudgeContextMenuLayerIntoVisibleHost, resolveQuickCreateSourceNode, submitCreateRequest, submitNodeEdit, readFileAsUpload, commitComposer, decorateComposerShell, createComposerWizard, createComposerSection, updateComposerFileState, openCreateComposer, openInlineNoteComposer, buildChildNotePlacement, buildSiblingNotePlacement, openKeyboardNoteComposer, openExistingNoteEditor, executeContextAction, openContextSubmenu, openContextSubmenuByActionId, renderContextMenuLayer, renderToolboxPreview, renderToolboxPanelLayer, showContextMenu, legacyOpenNodeMetadataMenu, startPan, isMarqueeModifierPressed, startMarquee, ensureSelectedForDrag, startDragForNodeIds, startDrag, startFrameDrag, updateMarquee, legacyApplyMarqueeSelection, updateDrag, updatePan, isNodeVisibleInViewport, centerNodeElementInViewport, ensureNodeVisible, legacyResize, findContainingBlockOverride, suspendContainingBlock, restoreContainingBlock, setMaximized, fitView, focusNode, normalizeWheelDelta, applyWheelZoom, setZoomPercent, setMenuScalePercent, toggleHelp, isManualDoubleActivation, handleNodeDoubleActivation, requestNodeOpen, legacyAttachEvents, hydrateState, refresh, getCanvasRuntimePrimitives, createFallbackHitRegistry, createCanvasHitRegistry, createCanvasSurfaceHost, destroyCanvasSurfaceHost, hexToRgba, resolveNodeAccentColor, resolveAnchorRect, buildRect, boundsToHitRect, projectSceneBounds, getNodeSceneBounds, clearSceneHotZones, registerSceneHotZone, getSceneHitAtPoint, getSceneHitAtEvent, resolveHitNode, clearScenePopoverHover, syncSceneHoverState, resolveCanvasNodeDetailMode, setCanvasFont, drawCanvasTextLines, drawRoundedPanel, requestSceneImage, buildCanvasSnapshotBounds, reconcileRetainedLayer, drawCanvasFrame, renderGroupFrames, drawCanvasLink, renderLinks, renderCanvasLinkLabels, drawCanvasBadgePill, drawCanvasProgressBadge, drawCanvasAnnotationBadges, drawNodeMediaPreview, renderCanvasMicroNode, renderCanvasInlineTextNode, renderCanvasDecisionNode, renderCanvasStandardNode, renderCanvasAdvancedNode } = shared;
    const { resolveSurfaceMode } = shared;

    function renderNodes(state, visibleNodes) {
        const surface = state.nodeSurface;
        if (!surface) {
            return;
        }

        surface.clear();
        state.sceneGeometry = {
            nodes: new Map(),
            frames: state.sceneGeometry?.frames || new Map()
        };
        const detailMode = resolveCanvasNodeDetailMode(state, (visibleNodes || []).length);
        const nextEntries = new Map();
        const hasHighlightedNodes = state.highlightedIds?.size > 0;
        const surfaceMode = resolveSurfaceMode(state);
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
            const isHighlighted = hasHighlightedNodes && state.highlightedIds.has(node.id);
            const isDimmed = hasHighlightedNodes && !isHighlighted;
            const meta = {
                selected: state.selectedIds.has(node.id),
                highlighted: isHighlighted,
                collapsed: state.collapsedIds.has(node.id),
                markerText: "",
                priorityText: "",
                progressTitle: "",
                hasPathButton: false,
                pathTitle: "",
                pathDisplayText: "",
                pathPromotedText: ""
            };
            if (isDimmed) {
                surface.context.save();
                surface.context.globalAlpha = Math.min(surface.context.globalAlpha, 0.5);
            }

            if (detailMode === "micro") {
                renderCanvasMicroNode(surface.context, state, node, hostBounds, accent, meta);
            }
            else if (node.isInlineTextNode) {
                renderCanvasInlineTextNode(surface.context, state, node, hostBounds, accent, detailMode, meta);
            }
            else if ((node.family || "").toLowerCase() === "workflow-decision" || (node.paletteKey || "").toLowerCase() === "workflow-decision") {
                renderCanvasDecisionNode(surface.context, state, node, hostBounds, accent, detailMode, meta);
            }
            else if ((Array.isArray(node.inputPorts) && node.inputPorts.length > 0) || (Array.isArray(node.outputPorts) && node.outputPorts.length > 0)) {
                renderCanvasAdvancedNode(surface.context, state, node, hostBounds, accent, detailMode, meta);
            }
            else {
                renderCanvasStandardNode(surface.context, state, node, hostBounds, accent, detailMode, meta);
            }

            if (isDimmed) {
                surface.context.restore();
            }

            if (surfaceMode === "delete" && state.hoveredDeleteNodeId === node.id) {
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

        renderCanvasLinkLabels(surface.context, state.renderedLinks);

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

    function clearActiveDragTransientOverlays(state) {
        if (state.anchorLayer) {
            state.anchorLayer.innerHTML = "";
        }

        if (state.transformLayer) {
            state.transformLayer.innerHTML = "";
        }

        if (state.debugLayer) {
            state.debugLayer.innerHTML = "";
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
        const movedNodeIds = new Set(state.interaction?.nodeIds || []);
        applySceneTransform(state);
        clearSceneHotZones(state);
        renderGroupFrames(state, projectedNodes);
        renderLinks(state, projectedNodes, {
            includePreviewLinks: false,
            linkFilter: link => movedNodeIds.has(link.sourceId) || movedNodeIds.has(link.targetId)
        });
        renderSnapGuides(state);
        renderNodes(state, projectedNodes);
        clearActiveDragTransientOverlays(state);

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

        if (state.connectionDraft?.nodeId) {
            activeIds.add(state.connectionDraft.nodeId);
        }

        if (state.connectionTarget?.nodeId) {
            activeIds.add(state.connectionTarget.nodeId);
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
                anchor.dataset.portId = point.portId || "";
                anchor.dataset.direction = point.direction || "";
                anchor.title = point.label
                    ? `${node.title || node.kind || "Node"} ${point.label}`
                    : `${node.title || node.kind || "Node"} ${point.side} anchor`;
                if (isPrimary) {
                    anchor.classList.add("is-primary");
                }

                if (point.accentColor) {
                    anchor.style.setProperty("--cw-connector-anchor-accent", point.accentColor);
                    anchor.style.setProperty("--cw-connector-anchor-ring", hexToRgba(point.accentColor, 0.2));
                    anchor.style.setProperty("--cw-connector-anchor-shadow", hexToRgba(point.accentColor, 0.18));
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
            const detailMode = resolveCanvasNodeDetailMode(state, (visibleNodes || []).length);
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

                const anchorSides = shared.resolveLinkAnchorSides?.(state, source, target, detailMode) || {
                    sourceSide: "right",
                    targetSide: "left"
                };
                for (const point of [
                    getLinkAnchorPoint(state, source, anchorSides.sourceSide, link.sourcePortId, "output", detailMode),
                    getLinkAnchorPoint(state, target, anchorSides.targetSide, link.targetPortId, "input", detailMode)
                ]) {
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

    function buildLocalMarqueeRect(state, interaction) {
        if (interaction &&
            Number.isFinite(interaction.startX) &&
            Number.isFinite(interaction.startY) &&
            Number.isFinite(interaction.currentX) &&
            Number.isFinite(interaction.currentY)) {
            const left = Math.min(interaction.startX, interaction.currentX);
            const top = Math.min(interaction.startY, interaction.currentY);
            return buildRect(
                left,
                top,
                Math.abs(interaction.currentX - interaction.startX),
                Math.abs(interaction.currentY - interaction.startY));
        }

        const hostRect = state.host.getBoundingClientRect();
        const marqueeRect = state.marquee.getBoundingClientRect();
        return buildRect(
            marqueeRect.left - hostRect.left,
            marqueeRect.top - hostRect.top,
            marqueeRect.width,
            marqueeRect.height);
    }

    function applyMarqueeSelection(state, interaction) {
        const marqueeRect = buildLocalMarqueeRect(state, interaction);
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
        if (!state?.host?.isConnected ||
            !state?.popover ||
            !state.popover.isConnected ||
            !state.popoverTitle ||
            !state.popoverBody ||
            !anchor ||
            !annotation) {
            hidePopover(state);
            return false;
        }

        if (state.surface?.chrome?.tooltipPopover?.isEnabled === false) {
            hidePopover(state);
            return false;
        }

        const anchorRect = resolveAnchorRect(anchor);
        if (!anchorRect) {
            hidePopover(state);
            return false;
        }

        state.popover.dataset.kind = annotation.kind || "info";
        state.popover.dataset.tone = annotation.tone || "accent";
        state.popoverTitle.textContent = annotation.label || annotation.kind || "Signal";
        state.popoverBody.textContent = annotation.description || annotation.label || "Shared workbench signal";
        state.popover.style.display = "grid";
        state.popoverAnchor = anchorRect;
        positionFloatingOverlayWithinHost(state, state.popover, anchorRect);
        return true;
    }

    Object.assign(shared, {
        renderNodes,
        renderActiveDrag,
        renderSnapGuides,
        renderConnectorAnchorOverlay,
        renderTransformHandlesOverlay,
        renderDebugDecorations,
        renderMinimap,
        measureRenderedNodeSizes,
        scheduleNodeMeasurement,
        applySceneTransform,
        applyMarqueeSelection,
        buildDiagnosticsSnapshot,
        showPopover
    });
})();
