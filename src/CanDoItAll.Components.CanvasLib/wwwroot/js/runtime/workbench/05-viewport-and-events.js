(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 05-viewport-and-events.js.'); }
    const workbenchInternals = new Proxy({}, {
        get(_target, property) {
            return shared.workbenchInternals?.[property];
        }
    });
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition, getLinkAnchorPoint, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, createMarkerBadge, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, copySelectionToClipboard, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint, hitTestNode, hitTestFrameHandle, hitTestProgressBadge, isOverlayTarget, applyFullTextTooltip, reconcileSelection, applySelection, selectSingleNode, publishSelection, clearViewportStateCommit, createSerializedStateSnapshot, invokeStateChanged, publishState, publishStateNow, scheduleViewportStateCommit, publishNodesMoved, setSelection, toggleSelection, toggleCollapse, clearContextMenu, closeComposer, ensureHostFocus, deferHostFocus, resolveComposerAnchor, layoutComposer, render, getContextActions, isCreateAction, buildCreateRequest, resolveMenuLabel, getMenuScale, isCompactHiveLayout, resolveMenuActionVariant, getActionMetrics, applyProgressPresetTone, fitContextMenuLabel, resolveActionGlyph, createMenuActionIcon, resolveMenuActionAriaLabel, getRadialOffsets, buildCompactHiveCoordinates, getCompactHiveOffsets, resolveContextMenuOffsets, resolveContextMenuSafeTop, getContextMenuLayerBounds, clampLayerBoundsToHost, positionContextMenu, getContextMenuOrbitRadius, getContextMenuLocalPoint, isPointInContextMenuLayer, closeContextMenuLayersFrom, syncContextMenuLayers, resolveSubmenuOrigin, ensureSubmenuLoadingIndicator, clearSubmenuLoadingIndicator, cancelPendingContextSubmenu, scheduleContextSubmenuOpen, clampLayerOriginToHost, getToolboxPanelSize, getToolboxPanelBounds, clampToolboxPanelOriginToHost, resolveToolboxPanelOrigin, createContextMenuLayer, shiftContextMenuLayerOrigin, nudgeContextMenuLayerIntoVisibleHost, resolveQuickCreateSourceNode, submitCreateRequest, submitNodeEdit, readFileAsUpload, commitComposer, decorateComposerShell, createComposerWizard, createComposerSection, updateComposerFileState, openCreateComposer, openInlineNoteComposer, buildChildNotePlacement, buildSiblingNotePlacement, openKeyboardNoteComposer, openExistingNoteEditor, executeContextAction, openContextSubmenu, openContextSubmenuByActionId, renderContextMenuLayer, renderToolboxPreview, renderToolboxPanelLayer, showContextMenu, legacyOpenNodeMetadataMenu } = shared;
    function startPan(state, event) {
        clearSnapGuides(state);
        if (state.anchorLayer) {
            state.anchorLayer.innerHTML = "";
        }

        if (state.transformLayer) {
            state.transformLayer.innerHTML = "";
        }

        if (state.debugLayer) {
            state.debugLayer.innerHTML = "";
        }

        state.interaction = {
            kind: "pan",
            startClientX: event.clientX,
            startClientY: event.clientY,
            panX: state.ui.panX,
            panY: state.ui.panY,
            moved: false
        };
    }

    function isMarqueeModifierPressed(state, event) {
        const modifierKey = state.surface?.chrome?.marqueeSelection?.modifierKey || "alt";
        switch (modifierKey) {
            case "shift":
                return !!event.shiftKey;
            case "control":
            case "ctrl":
                return !!event.ctrlKey;
            case "meta":
            case "cmd":
                return !!event.metaKey;
            default:
                return !!event.altKey;
        }
    }

    function startMarquee(state, event) {
        if (state.surface?.chrome?.marqueeSelection?.isEnabled === false) {
            return;
        }

        if (event.cancelable) {
            event.preventDefault();
        }

        clearSnapGuides(state);
        const point = getHostPoint(state, event.clientX, event.clientY);
        state.interaction = {
            kind: "marquee",
            startX: point.x,
            startY: point.y,
            currentX: point.x,
            currentY: point.y
        };
        Object.assign(state.marquee.style, {
            display: "block",
            left: `${point.x}px`,
            top: `${point.y}px`,
            width: "0px",
            height: "0px"
        });
    }

    function ensureSelectedForDrag(state, nodeId) {
        if (!state.selectedIds.has(nodeId)) {
            selectSingleNode(state, nodeId);
        }
    }

    function startDragForNodeIds(state, event, nodeIds, options) {
        cancelActiveDragRender(state);
        const draggedNodes = [...new Set((nodeIds || []).filter(id => state.lookups.byId.has(id)))];
        if (!draggedNodes.length) {
            return;
        }

        const startPositions = {};
        for (const id of draggedNodes) {
            const node = state.lookups.byId.get(id);
            startPositions[id] = getNodePosition(state, node);
        }

        state.interaction = {
            kind: options?.kind || "drag",
            startClientX: event.clientX,
            startClientY: event.clientY,
            moved: false,
            nodeIds: draggedNodes,
            startPositions,
            frameId: options?.frameId || null,
            sourceNodeId: options?.sourceNodeId || null,
            targetNodeId: options?.targetNodeId || null,
            dragContext: null
        };
        clearSnapGuides(state);
        render(state);
        state.interaction.dragContext = buildActiveDragContext(state);
        captureActiveDragBaseLayout(state, state.interaction);
    }

    function startDrag(state, event, nodeId) {
        ensureSelectedForDrag(state, nodeId);
        startDragForNodeIds(state, event, [...state.selectedIds], { kind: "drag" });
    }

    function startFrameDrag(state, event, frameId) {
        const renderedFrame = state.renderedFrames?.get(frameId || "");
        if (!renderedFrame?.nodeIds?.length) {
            return;
        }

        setSelection(state, renderedFrame.nodeIds, true);
        startDragForNodeIds(state, event, renderedFrame.nodeIds, { kind: "frame-drag", frameId });
    }

    function isNodeDragInteraction(interaction) {
        return interaction?.kind === "drag" ||
            interaction?.kind === "frame-drag" ||
            interaction?.kind === "dependency-drag";
    }

    function cloneLayoutPositionMap(layoutPositions) {
        const clone = new Map();
        for (const [nodeId, position] of layoutPositions || []) {
            if (!position) {
                continue;
            }

            clone.set(nodeId, {
                x: position.x,
                y: position.y
            });
        }

        return clone;
    }

    function captureActiveDragBaseLayout(state, interaction) {
        if (!isNodeDragInteraction(interaction)) {
            return;
        }

        const visibleNodes = getVisibleNodes(state);
        const layoutPositions = ensureLayoutPositions(state, visibleNodes);
        interaction.baseLayoutPositions = cloneLayoutPositionMap(layoutPositions);
    }

    function prepareActiveDragLayoutPositions(state, interaction) {
        if (!isNodeDragInteraction(interaction)) {
            return;
        }

        if (!interaction.baseLayoutPositions) {
            captureActiveDragBaseLayout(state, interaction);
        }

        const visibleNodes = getVisibleNodes(state);
        const layoutPositions = cloneLayoutPositionMap(interaction.baseLayoutPositions);
        for (const nodeId of interaction.nodeIds || []) {
            const position = state.ui.manualPositions?.[nodeId];
            if (!position) {
                continue;
            }

            layoutPositions.set(nodeId, {
                x: position.x,
                y: position.y
            });
        }

        state.layoutPositions = layoutPositions;
        state.layoutKey = buildResolvedLayoutKey(state, visibleNodes);
        state.sceneBounds = null;
        state.sceneBoundsKey = "";
    }

    function cancelActiveDragRender(state) {
        if (!state?.activeDragRenderFrame) {
            return false;
        }

        if (typeof window.cancelAnimationFrame === "function") {
            window.cancelAnimationFrame(state.activeDragRenderFrame);
        }

        state.activeDragRenderFrame = 0;
        return true;
    }

    function scheduleActiveDragRender(state) {
        if (!state || state.activeDragRenderFrame) {
            return;
        }

        if (typeof window.requestAnimationFrame !== "function") {
            workbenchInternals.scenePatching.renderActiveDrag(state);
            return;
        }

        state.activeDragRenderFrame = window.requestAnimationFrame(() => {
            state.activeDragRenderFrame = 0;
            if (!isNodeDragInteraction(state.interaction)) {
                return;
            }

            workbenchInternals.scenePatching.renderActiveDrag(state);
        });
    }

    function captureResolvedDragPositions(state, interaction) {
        const resolvedPositions = {};
        if (!interaction?.nodeIds?.length) {
            return resolvedPositions;
        }

        state.layoutPositions = null;
        state.layoutKey = "";
        const resolvedLayout = ensureLayoutPositions(state, getVisibleNodes(state));
        for (const nodeId of interaction.nodeIds) {
            const position = resolvedLayout.get(nodeId);
            if (!position) {
                continue;
            }

            resolvedPositions[nodeId] = {
                x: round(position.x),
                y: round(position.y)
            };
        }

        return resolvedPositions;
    }

    function applyResolvedDragPositions(state, interaction) {
        const resolvedPositions = interaction?.resolvedPositions || {};
        for (const nodeId of interaction?.nodeIds || []) {
            const resolved = resolvedPositions[nodeId];
            if (!resolved) {
                continue;
            }

            state.ui.manualPositions[nodeId] = {
                x: resolved.x,
                y: resolved.y
            };
        }

        state.layoutPositions = null;
        state.layoutKey = "";
    }

    function updateMarquee(state, event) {
        const point = getHostPoint(state, event.clientX, event.clientY);
        state.interaction.currentX = point.x;
        state.interaction.currentY = point.y;
        const left = Math.min(state.interaction.startX, point.x);
        const top = Math.min(state.interaction.startY, point.y);
        const width = Math.abs(point.x - state.interaction.startX);
        const height = Math.abs(point.y - state.interaction.startY);
        Object.assign(state.marquee.style, {
            left: `${left}px`,
            top: `${top}px`,
            width: `${width}px`,
            height: `${height}px`
        });
    }

    function legacyApplyMarqueeSelection(state) {
        const marqueeRect = state.marquee.getBoundingClientRect();
        const selectionMode = state.surface?.chrome?.marqueeSelection?.selectionMode || "intersect";
        const selected = [];
        for (const element of state.nodeLayer.querySelectorAll(".cw-node")) {
            const rect = element.getBoundingClientRect();
            const intersects = selectionMode === "contain"
                ? rect.left >= marqueeRect.left &&
                rect.right <= marqueeRect.right &&
                rect.top >= marqueeRect.top &&
                rect.bottom <= marqueeRect.bottom
                : rect.left < marqueeRect.right &&
                rect.right > marqueeRect.left &&
                rect.top < marqueeRect.bottom &&
                rect.bottom > marqueeRect.top;
            if (intersects) {
                selected.push(element.dataset.nodeId);
            }
        }

        state.marquee.style.display = "none";
        setSelection(state, selected, true);
    }

    function updateDrag(state, event) {
        const rawDeltaX = (event.clientX - state.interaction.startClientX) / state.ui.zoom;
        const rawDeltaY = (event.clientY - state.interaction.startClientY) / state.ui.zoom;
        const modifierPolicy = state.surface?.chrome?.snapGuides?.modifierPolicy || "shift-bypasses-snap";
        const bypassSnap = event.shiftKey && modifierPolicy === "shift-bypasses-snap";
        const snapResult = bypassSnap
            ? { deltaX: rawDeltaX, deltaY: rawDeltaY, guides: [] }
            : resolveSnapAdjustment(state, state.interaction, rawDeltaX, rawDeltaY);
        const deltaX = snapResult.deltaX;
        const deltaY = snapResult.deltaY;
        state.snapGuides = snapResult.guides;
        if (state.metrics) {
            state.metrics.lastResolvedDragDeltaX = round(deltaX);
            state.metrics.lastResolvedDragDeltaY = round(deltaY);
        }
        state.interaction.moved = state.interaction.moved || Math.abs(deltaX) > 0.5 || Math.abs(deltaY) > 0.5;

        for (const nodeId of state.interaction.nodeIds) {
            const startPosition = state.interaction.startPositions[nodeId];
            state.ui.manualPositions[nodeId] = {
                x: round(startPosition.x + deltaX),
                y: round(startPosition.y + deltaY)
            };
        }

        state.interaction.resolvedPositions = null;
        prepareActiveDragLayoutPositions(state, state.interaction);
        scheduleActiveDragRender(state);
    }

    function updatePan(state, event) {
        const deltaX = event.clientX - state.interaction.startClientX;
        const deltaY = event.clientY - state.interaction.startClientY;
        state.interaction.moved = state.interaction.moved || Math.abs(deltaX) > 1 || Math.abs(deltaY) > 1;
        workbenchInternals.sceneLayout.setPan(state, state.interaction.panX + deltaX, state.interaction.panY + deltaY, state.ui.zoom, { skipClamp: true });
        shared.applyViewportPreviewTransform?.(state);
    }

    async function finishInteraction(state) {
        if (!state.interaction) {
            return;
        }

        const interaction = state.interaction;
        shared.cancelDeferredViewportRender?.(state);
        cancelActiveDragRender(state);
        workbenchInternals.overlayRenderer.clearSnapGuides(state);
        if (state.metrics) {
            state.metrics.lastReleasedInteractionKind = interaction.kind || "";
            state.metrics.lastReleasedInteractionMoved = !!interaction.moved;
        }

        switch (interaction.kind) {
            case "drag":
            case "frame-drag":
            case "dependency-drag":
                if (interaction.moved) {
                    interaction.resolvedPositions ||= captureResolvedDragPositions(state, interaction);
                    applyResolvedDragPositions(state, interaction);
                    state.interaction = null;
                    await publishNodesMoved(state, interaction.nodeIds, interaction.resolvedPositions);
                    publishState(state);
                }
                else if (interaction.kind === "dependency-drag" &&
                    interaction.sourceNodeId &&
                    interaction.targetNodeId &&
                    state.dotNetRef?.invokeMethodAsync) {
                    state.interaction = null;
                    await state.dotNetRef.invokeMethodAsync(
                        "OnContextActionRequest",
                        JSON.stringify({
                            nodeId: interaction.targetNodeId,
                            actionId: "dependency:create",
                            x: 0,
                            y: 0,
                            targetKind: "node",
                            linkSourceId: interaction.sourceNodeId,
                            linkTargetId: interaction.targetNodeId,
                            linkKind: "DependsOn"
                        }));
                }
                else {
                    state.interaction = null;
                }
                break;
            case "pan":
                state.interaction = null;
                if (interaction.moved) {
                    setPan(state, state.ui.panX, state.ui.panY);
                    publishState(state);
                }
                break;
            case "marquee":
                state.interaction = null;
                (shared.applyMarqueeSelection || legacyApplyMarqueeSelection)(state, interaction);
                break;
            default:
                state.interaction = null;
                break;
        }

        render(state);
    }

    function getRenderedNodeBounds(state, nodeId) {
        const bounds = state.sceneGeometry?.nodes?.get?.(nodeId);
        if (!bounds) {
            return null;
        }

        return Number.isFinite(bounds.left) &&
            Number.isFinite(bounds.top) &&
            Number.isFinite(bounds.width) &&
            Number.isFinite(bounds.height)
            ? bounds
            : null;
    }

    function buildViewportRect(left, top, right, bottom) {
        const normalizedRight = Math.max(left, right);
        const normalizedBottom = Math.max(top, bottom);
        return {
            left,
            top,
            right: normalizedRight,
            bottom: normalizedBottom,
            width: Math.max(0, normalizedRight - left),
            height: Math.max(0, normalizedBottom - top)
        };
    }

    function rectContainsPoint(rect, x, y) {
        return rect &&
            x >= rect.left &&
            x <= rect.right &&
            y >= rect.top &&
            y <= rect.bottom;
    }

    function getFloatingWindowOccluders(state, hostRect) {
        const scope = state.host?.closest?.(".cw-stage-surface") || state.shell || state.host?.parentElement || state.document;
        if (!scope?.querySelectorAll) {
            return [];
        }

        return Array.from(scope.querySelectorAll(".cw-floating-window"))
            .filter(candidate => candidate instanceof HTMLElement)
            .map(candidate => {
                const styles = window.getComputedStyle(candidate);
                if (styles.display === "none" ||
                    styles.visibility === "hidden" ||
                    styles.pointerEvents === "none") {
                    return null;
                }

                const rect = candidate.getBoundingClientRect();
                if (rect.width <= 1 || rect.height <= 1) {
                    return null;
                }

                const localRect = buildViewportRect(
                    Math.max(0, rect.left - hostRect.left),
                    Math.max(0, rect.top - hostRect.top),
                    Math.min(hostRect.width, rect.right - hostRect.left),
                    Math.min(hostRect.height, rect.bottom - hostRect.top));

                return localRect.width > 1 && localRect.height > 1
                    ? localRect
                    : null;
            })
            .filter(candidate => candidate !== null);
    }

    function getPreferredViewportRect(state, margin) {
        const hostRect = state.host.getBoundingClientRect();
        const hostViewport = buildViewportRect(0, 0, hostRect.width, hostRect.height);
        const occluders = getFloatingWindowOccluders(state, hostRect);
        if (!occluders.length) {
            return applyViewportMargin(hostViewport, margin);
        }

        const candidates = [hostViewport];
        for (const occluder of occluders) {
            candidates.push(buildViewportRect(hostViewport.left, hostViewport.top, hostViewport.right, occluder.top));
            candidates.push(buildViewportRect(hostViewport.left, occluder.bottom, hostViewport.right, hostViewport.bottom));
            candidates.push(buildViewportRect(hostViewport.left, hostViewport.top, occluder.left, hostViewport.bottom));
            candidates.push(buildViewportRect(occluder.right, hostViewport.top, hostViewport.right, hostViewport.bottom));
        }

        let preferred = hostViewport;
        let preferredScore = -1;
        for (const candidate of candidates) {
            if (candidate.width < 120 || candidate.height < 96) {
                continue;
            }

            const centerX = candidate.left + (candidate.width / 2);
            const centerY = candidate.top + (candidate.height / 2);
            if (occluders.some(occluder => rectContainsPoint(occluder, centerX, centerY))) {
                continue;
            }

            const score = candidate.width * candidate.height;
            if (score > preferredScore) {
                preferred = candidate;
                preferredScore = score;
            }
        }

        return applyViewportMargin(preferred, margin);
    }

    function applyViewportMargin(viewportRect, margin) {
        const desiredMargin = typeof margin === "number" ? margin : 92;
        const insetX = Math.min(desiredMargin, Math.max(0, (viewportRect.width - 180) / 2));
        const insetY = Math.min(desiredMargin, Math.max(0, (viewportRect.height - 132) / 2));
        const insetViewport = buildViewportRect(
            viewportRect.left + insetX,
            viewportRect.top + insetY,
            viewportRect.right - insetX,
            viewportRect.bottom - insetY);
        return insetViewport.width > 0 && insetViewport.height > 0
            ? insetViewport
            : viewportRect;
    }

    function isNodeVisibleInViewport(state, node, margin) {
        const viewportRect = getPreferredViewportRect(state, margin);
        const renderedNode = getRenderedNodeBounds(state, node.id);
        if (renderedNode) {
            return renderedNode.left >= viewportRect.left &&
                renderedNode.right <= viewportRect.right &&
                renderedNode.top >= viewportRect.top &&
                renderedNode.bottom <= viewportRect.bottom;
        }

        const position = worldToHostPoint(state, getNodePosition(state, node));
        const size = getNodeSize(state, node);
        const halfWidth = (size.width * state.ui.zoom) / 2;
        const halfHeight = (size.height * state.ui.zoom) / 2;

        return position.x - halfWidth >= viewportRect.left &&
            position.x + halfWidth <= viewportRect.right &&
            position.y - halfHeight >= viewportRect.top &&
            position.y + halfHeight <= viewportRect.bottom;
    }

    function centerNodeElementInViewport(state, nodeId) {
        const element = state.nodeLayer?.querySelector?.(`.cw-node[data-node-id="${nodeId}"]`);
        if (!element) {
            return false;
        }

        const hostRect = state.host.getBoundingClientRect();
        const viewportRect = getPreferredViewportRect(state, 72);
        const nodeRect = element.getBoundingClientRect();
        const viewportCenterX = hostRect.left + viewportRect.left + (viewportRect.width / 2);
        const viewportCenterY = hostRect.top + viewportRect.top + (viewportRect.height / 2);
        const deltaX = viewportCenterX - (nodeRect.left + (nodeRect.width / 2));
        const deltaY = viewportCenterY - (nodeRect.top + (nodeRect.height / 2));
        state.ui.panX = round(state.ui.panX + deltaX);
        state.ui.panY = round(state.ui.panY + deltaY);
        return true;
    }

    function ensureNodeVisible(state, nodeId, options) {
        const node = state.lookups.byId.get(nodeId);
        if (!node) {
            return false;
        }

        const forceCenter = !!options?.forceCenter;
        if (!forceCenter && isNodeVisibleInViewport(state, node, options?.margin)) {
            return false;
        }

        const viewportRect = getPreferredViewportRect(state, options?.margin);
        const viewportCenterX = viewportRect.left + (viewportRect.width / 2);
        const viewportCenterY = viewportRect.top + (viewportRect.height / 2);
        const renderedNode = getRenderedNodeBounds(state, nodeId);
        if (renderedNode) {
            setPan(
                state,
                state.ui.panX + (viewportCenterX - (renderedNode.left + (renderedNode.width / 2))),
                state.ui.panY + (viewportCenterY - (renderedNode.top + (renderedNode.height / 2))));
            return true;
        }

        const position = getNodePosition(state, node);
        setPan(
            state,
            viewportCenterX - (position.x * state.ui.zoom),
            viewportCenterY - (position.y * state.ui.zoom));
        return true;
    }

    function legacyResize(state) {
        cancelViewportAnimation(state);
        const rect = state.host.getBoundingClientRect();
        state.links.setAttribute("width", `${Math.max(rect.width, 1)}`);
        state.links.setAttribute("height", `${Math.max(rect.height, 1)}`);
        setPan(state, state.ui.panX, state.ui.panY);
        layoutComposer(state);
    }

    function findContainingBlockOverrides(state) {
        const overrides = [];
        let current = state.shell?.parentElement || null;
        while (current) {
            const style = window.getComputedStyle(current);
            if (style.transform !== "none" ||
                style.perspective !== "none" ||
                style.filter !== "none" ||
                style.backdropFilter !== "none" ||
                style.webkitBackdropFilter !== "none") {
                overrides.push({
                    element: current,
                    transform: current.style.transform,
                    perspective: current.style.perspective,
                    filter: current.style.filter,
                    backdropFilter: current.style.backdropFilter,
                    webkitBackdropFilter: current.style.webkitBackdropFilter
                });
            }

            current = current.parentElement;
        }

        return overrides;
    }

    function suspendContainingBlock(state) {
        if (state.containingBlockOverride) {
            return;
        }

        const overrides = findContainingBlockOverrides(state);
        if (!overrides.length) {
            return;
        }

        state.containingBlockOverride = overrides;
        for (const override of overrides) {
            override.element.style.transform = "none";
            override.element.style.perspective = "none";
            override.element.style.filter = "none";
            override.element.style.backdropFilter = "none";
            override.element.style.webkitBackdropFilter = "none";
        }
    }

    function restoreContainingBlock(state) {
        if (!state.containingBlockOverride) {
            return;
        }

        const overrides = Array.isArray(state.containingBlockOverride)
            ? state.containingBlockOverride
            : [state.containingBlockOverride];
        for (const override of overrides) {
            override.element.style.transform = override.transform || "";
            override.element.style.perspective = override.perspective || "";
            override.element.style.filter = override.filter || "";
            override.element.style.backdropFilter = override.backdropFilter || "";
            override.element.style.webkitBackdropFilter = override.webkitBackdropFilter || "";
        }

        state.containingBlockOverride = null;
    }

    function schedulePostLayoutResize(state, resizeSurface) {
        if (typeof window.requestAnimationFrame !== "function") {
            resizeSurface(state);
            render(state);
            return;
        }

        window.requestAnimationFrame(() => {
            resizeSurface(state);
            render(state);

            window.requestAnimationFrame(() => {
                resizeSurface(state);
                render(state);
            });
        });
    }

    function setMaximized(state, isMaximized) {
        cancelViewportAnimation(state);
        state.ui.isMaximized = !!isMaximized;
        if (isMaximized) {
            suspendContainingBlock(state);
        }
        else {
            restoreContainingBlock(state);
        }

        state.document.body.classList.toggle("cw-body-lock", !!isMaximized);
        state.document.documentElement.classList.toggle("cw-body-lock", !!isMaximized);
        if (state.shell) {
            state.shell.classList.toggle("is-maximized", !!isMaximized);
        }

        const resizeSurface = workbenchInternals.sceneLayout?.resize || legacyResize;
        resizeSurface(state);
        render(state);
        schedulePostLayoutResize(state, resizeSurface);
    }

    function fitView(state) {
        const visibleNodes = getVisibleNodes(state);
        if (!visibleNodes.length) {
            return;
        }

        const bounds = getSceneBounds(state, visibleNodes);
        const rect = state.host.getBoundingClientRect();
        const viewportController = getViewportControllerService();
        const target = viewportController?.createFitViewTarget
            ? viewportController.createFitViewTarget({
                bounds,
                hostWidth: rect.width,
                hostHeight: rect.height
            })
            : (() => {
                const padding = 120;
                const width = Math.max(bounds.maxX - bounds.minX, 320);
                const height = Math.max(bounds.maxY - bounds.minY, 240);
                const zoom = clamp(Math.min((rect.width - padding) / width, (rect.height - padding) / height), MIN_ZOOM, MAX_ZOOM);
                return {
                    zoom,
                    panX: (rect.width / 2) - ((bounds.minX + (width / 2)) * zoom),
                    panY: (rect.height / 2) - ((bounds.minY + (height / 2)) * zoom)
                };
            })();

        animateViewportTransition(state, target, {
            key: "viewport",
            durationMs: 320,
            easing: "softInOut"
        });
    }

    function focusNode(state, nodeId) {
        const node = state.lookups.byId.get(nodeId);
        if (!node) {
            return;
        }

        selectSingleNode(state, nodeId, { publish: false });
        const viewportRect = getPreferredViewportRect(state, 72);
        const viewportCenterX = viewportRect.left + (viewportRect.width / 2);
        const viewportCenterY = viewportRect.top + (viewportRect.height / 2);
        const renderedNode = state.sceneGeometry?.nodes?.get?.(nodeId);
        const target = renderedNode
            ? {
                zoom: state.ui.zoom,
                panX: state.ui.panX + (viewportCenterX - (renderedNode.left + (renderedNode.width / 2))),
                panY: state.ui.panY + (viewportCenterY - (renderedNode.top + (renderedNode.height / 2)))
            }
            : (() => {
                const position = getNodePosition(state, node);
                return {
                    zoom: state.ui.zoom,
                    panX: viewportCenterX - (position.x * state.ui.zoom),
                    panY: viewportCenterY - (position.y * state.ui.zoom)
                };
            })();

        cancelViewportAnimation(state);
        updateViewportTransform(state, target, { skipClamp: true });
        render(state);

        for (let attempt = 0; attempt < 2; attempt += 1) {
            const centeredNode = state.sceneGeometry?.nodes?.get?.(nodeId);
            if (!centeredNode) {
                break;
            }

            const deltaX = viewportCenterX - (centeredNode.left + (centeredNode.width / 2));
            const deltaY = viewportCenterY - (centeredNode.top + (centeredNode.height / 2));
            if (Math.abs(deltaX) <= 0.5 && Math.abs(deltaY) <= 0.5) {
                break;
            }

            updateViewportTransform(state, {
                zoom: state.ui.zoom,
                panX: state.ui.panX + deltaX,
                panY: state.ui.panY + deltaY
            }, { skipClamp: true });
            render(state);
        }

        ensureHostFocus(state);
        deferHostFocus(state);
        state.pendingFocusNodeId = nodeId;
        publishSelection(state);
        publishState(state);

        if (state.focusRecenterTimer) {
            window.clearTimeout(state.focusRecenterTimer);
        }

        state.focusRecenterTimer = window.setTimeout(() => {
            state.focusRecenterTimer = 0;
            const currentNode = state.lookups.byId.get(nodeId);
            if (!currentNode) {
                return;
            }

            const latestViewportRect = getPreferredViewportRect(state, 72);
            const latestViewportCenterX = latestViewportRect.left + (latestViewportRect.width / 2);
            const latestViewportCenterY = latestViewportRect.top + (latestViewportRect.height / 2);
            const centeredNode = state.sceneGeometry?.nodes?.get?.(nodeId);
            if (!centeredNode) {
                return;
            }

            const deltaX = latestViewportCenterX - (centeredNode.left + (centeredNode.width / 2));
            const deltaY = latestViewportCenterY - (centeredNode.top + (centeredNode.height / 2));
            if (Math.abs(deltaX) <= 0.5 && Math.abs(deltaY) <= 0.5) {
                return;
            }

            updateViewportTransform(state, {
                zoom: state.ui.zoom,
                panX: state.ui.panX + deltaX,
                panY: state.ui.panY + deltaY
            }, { skipClamp: true });
            render(state);
            publishState(state);
        }, 160);
    }

    function normalizeWheelDelta(event) {
        let delta = event.deltaY;
        switch (event.deltaMode) {
            case 1:
                delta *= 16;
                break;
            case 2:
                delta *= window.innerHeight || 800;
                break;
        }

        return delta;
    }

    function applyWheelZoom(state, event) {
        const hostPoint = getHostPoint(state, event.clientX, event.clientY);
        const normalizedDelta = normalizeWheelDelta(event);
        if (!normalizedDelta) {
            return;
        }

        const wheelZoom = state.wheelZoom || { accumulator: 0, direction: 0, lastTimestamp: 0 };
        const direction = Math.sign(normalizedDelta);
        const magnitude = Math.abs(normalizedDelta);
        const now = typeof event.timeStamp === "number" ? event.timeStamp : Date.now();
        if ((now - wheelZoom.lastTimestamp) > 140) {
            wheelZoom.accumulator = 0;
            wheelZoom.direction = 0;
        }

        if (wheelZoom.direction &&
            direction !== wheelZoom.direction &&
            magnitude < Math.max(8, Math.abs(wheelZoom.accumulator) * 0.6)) {
            wheelZoom.lastTimestamp = now;
            state.wheelZoom = wheelZoom;
            return;
        }

        if (wheelZoom.direction && direction !== wheelZoom.direction) {
            wheelZoom.accumulator = 0;
        }

        wheelZoom.direction = direction;
        wheelZoom.lastTimestamp = now;
        wheelZoom.accumulator += normalizedDelta;

        const threshold = 24;
        if (Math.abs(wheelZoom.accumulator) < threshold) {
            state.wheelZoom = wheelZoom;
            return;
        }

        const stepCount = Math.max(1, Math.floor(Math.abs(wheelZoom.accumulator) / threshold));
        wheelZoom.accumulator -= stepCount * threshold * direction;
        state.wheelZoom = wheelZoom;
        setZoomPercent(
            state,
            (state.ui.zoom * 100) + (-direction * stepCount * 4),
            hostPoint,
            {
                commitMode: "idle",
                delayMs: 280,
                renderMode: "animation-frame"
            });
    }

    function setZoomPercent(state, percent, anchorPoint, options) {
        cancelViewportAnimation(state);
        const rect = state.host.getBoundingClientRect();
        const anchor = anchorPoint || { x: rect.width / 2, y: rect.height / 2 };
        const viewportController = getViewportControllerService();
        const target = viewportController?.zoomAroundPoint
            ? viewportController.zoomAroundPoint({
                bounds: getSceneBounds(state),
                hostWidth: rect.width,
                hostHeight: rect.height,
                anchorX: anchor.x,
                anchorY: anchor.y,
                panX: state.ui.panX,
                panY: state.ui.panY,
                zoom: state.ui.zoom,
                percent
            })
            : (() => {
                const nextZoom = clamp((percent || 100) / 100, MIN_ZOOM, MAX_ZOOM);
                const worldX = (anchor.x - state.ui.panX) / state.ui.zoom;
                const worldY = (anchor.y - state.ui.panY) / state.ui.zoom;
                return {
                    zoom: nextZoom,
                    panX: anchor.x - (worldX * nextZoom),
                    panY: anchor.y - (worldY * nextZoom)
                };
            })();

        state.ui.zoom = target.zoom;
        setPan(
            state,
            target.panX,
            target.panY,
            target.zoom);
        if (options?.renderMode === "animation-frame") {
            shared.applyViewportPreviewTransform?.(state);
            shared.scheduleDeferredViewportRender?.(state, options?.delayMs ?? 180);
        }
        else {
            render(state);
        }

        if (options?.commitMode === "idle") {
            scheduleViewportStateCommit(state, options?.delayMs);
            return;
        }

        publishState(state);
    }

    function setMenuScalePercent(state, menuScalePercent) {
        const nextScale = normalizeMenuActionScale((menuScalePercent || 100) / 100);
        if (Math.abs((state.ui.menuActionScale || 1) - nextScale) <= 0.001) {
            return;
        }

        state.ui.menuActionScale = nextScale;
        syncMenuScaleCss(state);
        clearContextMenu(state);
        render(state);
        publishState(state);
    }

    function toggleHelp(state) {
        state.helpOpen = !state.helpOpen;
        state.dotNetRef.invokeMethodAsync("OnHelpToggled", state.helpOpen);
    }

    function isManualDoubleActivation(state, nodeId) {
        const now = Date.now();
        const isRepeatedTarget = state.lastPointerTarget?.nodeId === nodeId;
        const isRapidRepeat = !!state.lastPointerTarget && (now - state.lastPointerTarget.timestamp) <= 340;
        state.lastPointerTarget = { nodeId, timestamp: now };
        return isRepeatedTarget && isRapidRepeat;
    }

    function handleNodeDoubleActivation(state, node) {
        state.recentDoubleActivationAt = Date.now();
        selectSingleNode(state, node.id, { publish: false });
        publishSelection(state);
        publishState(state);

        if (node.isInlineTextNode) {
            openExistingNoteEditor(state, node);
            return;
        }

        const collapseOnDoubleClick = state?.surface?.chrome?.collapseOnDoubleClick !== false;
        if (collapseOnDoubleClick && node.isCollapsible) {
            toggleCollapse(state, node.id);
            return;
        }

        void requestNodeOpen(state, node.id, { focusNodeIntoView: false });
    }

    async function requestNodeOpen(state, nodeId, options) {
        const node = state?.lookups?.byId?.get?.(nodeId);
        if (!node || !state?.dotNetRef) {
            return false;
        }

        if (options?.focusNodeIntoView !== false) {
            focusNode(state, nodeId);
        }
        else if (!state.selectedIds.has(nodeId)) {
            selectSingleNode(state, nodeId, { publish: false });
            publishSelection(state);
            publishState(state);
        }

        try {
            await state.dotNetRef.invokeMethodAsync("OnNodeOpened", nodeId);
            return true;
        }
        catch (error) {
            console.warn("CanvasLib failed to open node.", { nodeId, error });
            showStatusNotice(state, "Could not open the selected node.");
            return false;
        }
    }

    function legacyAttachEvents(state) {
        state.handlers = {
            pointerDown: event => {
                if (isOverlayTarget(event.target)) {
                    return;
                }

                shared.flushDeferredViewportRender?.(state);

                if (state.composer) {
                    closeComposer(state, { focusHost: false });
                }

                clearContextMenu(state);
                cancelViewportAnimation(state);
                ensureHostFocus(state);
                deferHostFocus(state);

                if (event.button === 2) {
                    return;
                }

                if (event.button === 1) {
                    startPan(state, event);
                    return;
                }

                const targetFrameId = hitTestFrameHandle(event.target);
                if (targetFrameId) {
                    startFrameDrag(state, event, targetFrameId);
                    return;
                }

                const targetNode = hitTestNode(state, event.target);
                if (isMarqueeModifierPressed(state, event)) {
                    startMarquee(state, event);
                    return;
                }

                if (targetNode) {
                    const isMultiToggle = (event.ctrlKey || event.metaKey) && event.shiftKey;
                    const progressBadge = hitTestProgressBadge(event.target);
                    if (event.button === 0 &&
                        !event.altKey &&
                        !event.ctrlKey &&
                        !event.metaKey &&
                        isManualDoubleActivation(state, targetNode.id)) {
                        if (progressBadge) {
                            state.recentDoubleActivationAt = Date.now();
                            openNodeMetadataMenu(state, targetNode, "progress", progressBadge);
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
                if (!state.interaction) {
                    syncContextMenuLayers(state, event);
                    return;
                }

                switch (state.interaction.kind) {
                    case "drag":
                    case "frame-drag":
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
            pointerUp: () => finishInteraction(state),
            blur: () => finishInteraction(state),
            doubleClick: event => {
                if (state.recentDoubleActivationAt && (Date.now() - state.recentDoubleActivationAt) <= 340) {
                    return;
                }

                if (isOverlayTarget(event.target)) {
                    return;
                }

                const targetNode = hitTestNode(state, event.target);
                if (!targetNode) {
                    return;
                }

                const progressBadge = hitTestProgressBadge(event.target);
                if (progressBadge) {
                    openNodeMetadataMenu(state, targetNode, "progress", progressBadge);
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
                const targetNode = hitTestNode(state, event.target);
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
                const isCanvasKeyTarget = !target ||
                    target === state.document.body ||
                    target === state.document.documentElement ||
                    state.host.contains(target);
                if (!isCanvasKeyTarget) {
                    return;
                }

                const isEditable = tagName === "input" || tagName === "textarea" || target?.isContentEditable;
                if (isEditable) {
                    if (event.key === "Escape") {
                        event.preventDefault();
                        closeComposer(state);
                    }

                    return;
                }

                if (shared.routeContextMenuShortcut?.(state, event)) {
                    return;
                }

                const lowerKey = (event.key || "").toLowerCase();
                const usesCommandModifier = event.ctrlKey || event.metaKey;
                if (usesCommandModifier && !event.altKey) {
                    switch (lowerKey) {
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
                            const hadContextMenu = state.contextMenu?.style.display !== "none";
                            const hadComposer = !!state.composer;
                            clearContextMenu(state);
                            closeComposer(state);
                            shared.clearNodeHighlights?.(state, { render: true, publish: true });
                            if (!hadContextMenu && !hadComposer) {
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

    function hydrateState(host, dotNetRef, surface, selectionDispatchSeed, stateDispatchSeed) {
        const normalizedSurface = workbenchInternals.stateStore.normalizeSurface(surface);
        const lookups = workbenchInternals.stateStore.buildNodeLookup(normalizedSurface.nodes);
        const animationTimelineService = getAnimationTimelineService();
        const animationTimeline = animationTimelineService?.createController?.() || null;

        const state = {
            host,
            shell: host.closest(".cw-workbench-shell"),
            document: host.ownerDocument,
            dotNetRef,
            animationTimeline,
            surface: normalizedSurface,
            lookups,
            ui: normalizedSurface.uiState,
            selectedIds: toSelectionSet(normalizedSurface.uiState.selectedNodeIds),
            highlightedIds: new Set((normalizedSurface.uiState.highlightedNodeIds || []).filter(Boolean)),
            collapsedIds: toCollapsedSet(normalizedSurface.uiState.collapsedNodeIds),
            helpOpen: false,
            interaction: null,
            scene: null,
            frameLayer: null,
            links: null,
            debugLayer: null,
            guideLayer: null,
            nodeLayer: null,
            anchorLayer: null,
            transformLayer: null,
            marquee: null,
            emptyState: null,
            emptyStateKicker: null,
            emptyStateTitle: null,
            emptyStateBody: null,
            diagnosticsPanel: null,
            diagnosticsBody: null,
            minimapShell: null,
            minimapTitle: null,
            minimapCanvas: null,
            contextMenu: null,
            contextMenuState: null,
            composer: null,
            pendingCreate: null,
            pendingFocusNodeId: null,
            containingBlockOverride: null,
            resizeObserver: null,
            lastPointerTarget: null,
            lastCreateSignature: "",
            lastCreateRequestedAt: 0,
            recentDoubleActivationAt: 0,
            wheelZoom: null,
            measuredNodeSizes: new Map(),
            renderedFrames: new Map(),
            layoutPositions: null,
            layoutKey: "",
            measureLayoutFrame: 0,
            snapGuides: [],
            hoveredNodeId: null,
            hoveredDeleteNodeId: null,
            hoveredDeleteLinkKey: null,
            hoveredAnnotationKey: "",
            pointerHostPoint: null,
            connectionDraft: null,
            connectionTarget: null,
            renderedLinks: [],
            previewLink: null,
            popover: null,
            popoverTitle: null,
            popoverBody: null,
            popoverAnchor: null,
            statusNotice: null,
            statusNoticeTimer: 0,
            localClipboard: "",
            minimapMetrics: null,
            viewportStateTimer: 0,
            selectionDispatchId: Number.isFinite(selectionDispatchSeed) ? Number(selectionDispatchSeed) : 0,
            stateDispatchId: Number.isFinite(stateDispatchSeed) ? Number(stateDispatchSeed) : 0,
            retainedFrameElements: new Map(),
            retainedLinkElements: new Map(),
            retainedNodeElements: new Map(),
            metrics: workbenchInternals.instrumentation.createWorkbenchMetrics(),
            publishStateDebounced: debounce(stateJson => invokeStateChanged(state, stateJson, "debounced"), 140)
        };

        state.animationTimeline?.setReducedMotionAttribute?.(host);
        workbenchInternals.stateStore.reconcileSelection(state);
        return state;
    }

    function refresh(state, surface) {
        workbenchInternals.sceneLayout.cancelViewportAnimation(state);
        const previousNodeIds = new Set((state.surface?.nodes || []).map(node => node.id));
        const previousSelectedId = state.ui?.selectedNodeIds?.[0] || null;
        const pendingFocusNodeId = state.pendingFocusNodeId;
        const previousViewport = {
            panX: state.ui?.panX ?? 0,
            panY: state.ui?.panY ?? 0,
            zoom: state.ui?.zoom ?? 1
        };
        const pendingCreate = state.pendingCreate;
        if (state.measureLayoutFrame) {
            window.cancelAnimationFrame(state.measureLayoutFrame);
            state.measureLayoutFrame = 0;
        }

        state.surface = workbenchInternals.stateStore.normalizeSurface(surface);
        state.lookups = workbenchInternals.stateStore.buildNodeLookup(state.surface.nodes);
        state.ui = state.surface.uiState;
        workbenchInternals.stateStore.reconcileSelection(state);
        state.highlightedIds = new Set((state.ui.highlightedNodeIds || []).filter(Boolean));
        state.collapsedIds = toCollapsedSet(state.ui.collapsedNodeIds);
        state.pointerHostPoint = null;
        state.hoveredDeleteNodeId = null;
        state.hoveredDeleteLinkKey = null;
        state.hoveredAnnotationKey = "";
        state.connectionDraft = null;
        state.connectionTarget = null;
        state.renderedLinks = [];
        state.previewLink = null;
        syncMenuScaleCss(state);
        workbenchInternals.runtime.syncWorkbenchMode?.(state);
        invalidateMeasuredLayout(state);
        workbenchInternals.overlayRenderer.clearContextMenu(state);
        workbenchInternals.overlayRenderer.hidePopover(state);
        if (state.composer?.nodeId && !state.lookups.byId.has(state.composer.nodeId)) {
            workbenchInternals.overlayRenderer.closeComposer(state, { focusHost: false });
        }

        setMaximized(state, !!state.ui.isMaximized);
        workbenchInternals.sceneLayout.resize(state);
        const incomingViewportIsDefault = Math.abs(state.ui.panX - 90) <= 0.5 &&
            Math.abs(state.ui.panY - 110) <= 0.5 &&
            Math.abs(state.ui.zoom - 1) <= 0.001;
        const shouldPreserveViewport = incomingViewportIsDefault || (Math.abs(state.ui.panX - previousViewport.panX) <= 0.5 &&
            Math.abs(state.ui.panY - previousViewport.panY) <= 0.5 &&
            Math.abs(state.ui.zoom - previousViewport.zoom) <= 0.001);
        if (shouldPreserveViewport) {
            state.ui.zoom = previousViewport.zoom;
            state.ui.panX = previousViewport.panX;
            state.ui.panY = previousViewport.panY;
            workbenchInternals.sceneLayout.applySceneTransform(state);
        }

        const selectedNodeId = state.ui.selectedNodeIds[0] || null;
        const selectedNode = selectedNodeId ? state.lookups.byId.get(selectedNodeId) : null;
        const selectionChanged = !!selectedNodeId && selectedNodeId !== previousSelectedId;
        const shouldRevealSelection = !!selectedNodeId &&
            (!!pendingCreate || selectionChanged);
        const shouldRestoreVisibleSelection = !!selectedNodeId &&
            !!selectedNode &&
            (!!pendingCreate || selectionChanged) &&
            !workbenchInternals.sceneLayout.isNodeVisibleInViewport(state, selectedNode, 72);
        if (shouldRevealSelection || shouldRestoreVisibleSelection) {
            const isNewNode = !previousNodeIds.has(selectedNodeId);
            workbenchInternals.sceneLayout.ensureNodeVisible(state, selectedNodeId, { forceCenter: isNewNode || shouldRestoreVisibleSelection });
        }

        render(state);
        if (selectedNodeId &&
            state.lookups.byId.has(selectedNodeId) &&
            (!!pendingCreate || selectionChanged) &&
            !workbenchInternals.sceneLayout.isNodeVisibleInViewport(state, state.lookups.byId.get(selectedNodeId), 72)) {
            workbenchInternals.sceneLayout.ensureNodeVisible(state, selectedNodeId, { forceCenter: true });
            render(state);
        }

        if (pendingFocusNodeId && state.lookups.byId.has(pendingFocusNodeId)) {
            const rect = state.host.getBoundingClientRect();
            const centeredNode = state.sceneGeometry?.nodes?.get?.(pendingFocusNodeId);
            if (centeredNode) {
                const deltaX = (rect.width / 2) - (centeredNode.left + (centeredNode.width / 2));
                const deltaY = (rect.height / 2) - (centeredNode.top + (centeredNode.height / 2));
                if (Math.abs(deltaX) > 0.5 || Math.abs(deltaY) > 0.5) {
                    updateViewportTransform(state, {
                        zoom: state.ui.zoom,
                        panX: state.ui.panX + deltaX,
                        panY: state.ui.panY + deltaY
                    }, { skipClamp: true });
                    render(state);
                }
            }

            state.pendingFocusNodeId = null;
        }

        if (pendingCreate) {
            if (pendingCreate.focusHost) {
                deferHostFocus(state);
            }

            state.pendingCreate = null;
        }
    }

    Object.assign(shared, { startPan, isMarqueeModifierPressed, startMarquee, ensureSelectedForDrag, startDragForNodeIds, startDrag, startFrameDrag, updateMarquee, legacyApplyMarqueeSelection, updateDrag, updatePan, finishInteraction, isNodeVisibleInViewport, centerNodeElementInViewport, ensureNodeVisible, legacyResize, findContainingBlockOverride: findContainingBlockOverrides, findContainingBlockOverrides, suspendContainingBlock, restoreContainingBlock, setMaximized, fitView, focusNode, normalizeWheelDelta, applyWheelZoom, setZoomPercent, setMenuScalePercent, toggleHelp, isManualDoubleActivation, handleNodeDoubleActivation, requestNodeOpen, legacyAttachEvents, hydrateState, refresh });
})();
