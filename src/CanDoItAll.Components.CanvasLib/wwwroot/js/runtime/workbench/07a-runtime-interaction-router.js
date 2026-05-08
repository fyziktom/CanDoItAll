(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 07a-runtime-interaction-router.js.'); }
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition, getLinkAnchorPoint, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, createMarkerBadge, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, writeClipboardText, copySelectionToClipboard, requestClipboardCut, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint, hitTestNode, hitTestFrameHandle, hitTestProgressBadge, isOverlayTarget, applyFullTextTooltip, reconcileSelection, applySelection, selectSingleNode, publishSelection, clearViewportStateCommit, createSerializedStateSnapshot, invokeStateChanged, publishState, publishStateNow, scheduleViewportStateCommit, publishNodesMoved, setSelection, toggleSelection, toggleCollapse, clearContextMenu, closeComposer, ensureHostFocus, deferHostFocus, resolveComposerAnchor, layoutComposer, render, getContextActions, isCreateAction, buildCreateRequest, resolveMenuLabel, getMenuScale, isCompactHiveLayout, resolveMenuActionVariant, getActionMetrics, applyProgressPresetTone, fitContextMenuLabel, resolveActionGlyph, createMenuActionIcon, resolveMenuActionAriaLabel, getRadialOffsets, buildCompactHiveCoordinates, getCompactHiveOffsets, resolveContextMenuOffsets, resolveContextMenuSafeTop, getContextMenuLayerBounds, clampLayerBoundsToHost, positionContextMenu, getContextMenuOrbitRadius, getContextMenuLocalPoint, isPointInContextMenuLayer, closeContextMenuLayersFrom, syncContextMenuLayers, resolveSubmenuOrigin, ensureSubmenuLoadingIndicator, clearSubmenuLoadingIndicator, cancelPendingContextSubmenu, scheduleContextSubmenuOpen, clampLayerOriginToHost, getToolboxPanelSize, getToolboxPanelBounds, clampToolboxPanelOriginToHost, resolveToolboxPanelOrigin, createContextMenuLayer, shiftContextMenuLayerOrigin, nudgeContextMenuLayerIntoVisibleHost, resolveQuickCreateSourceNode, submitCreateRequest, submitNodeEdit, readFileAsUpload, commitComposer, decorateComposerShell, createComposerWizard, createComposerSection, updateComposerFileState, openCreateComposer, openInlineNoteComposer, buildChildNotePlacement, buildSiblingNotePlacement, openKeyboardNoteComposer, openExistingNoteEditor, executeContextAction, openContextSubmenu, openContextSubmenuByActionId, renderContextMenuLayer, renderToolboxPreview, renderToolboxPanelLayer, showContextMenu, legacyOpenNodeMetadataMenu, startPan, isMarqueeModifierPressed, startMarquee, ensureSelectedForDrag, startDragForNodeIds, startDrag, startFrameDrag, updateMarquee, legacyApplyMarqueeSelection, updateDrag, updatePan, isNodeVisibleInViewport, centerNodeElementInViewport, ensureNodeVisible, legacyResize, findContainingBlockOverride, suspendContainingBlock, restoreContainingBlock, setMaximized, fitView, focusNode, normalizeWheelDelta, applyWheelZoom, setZoomPercent, setMenuScalePercent, toggleHelp, isManualDoubleActivation, handleNodeDoubleActivation, requestNodeOpen, legacyAttachEvents, hydrateState, refresh, getCanvasRuntimePrimitives, createFallbackHitRegistry, createCanvasHitRegistry, createCanvasSurfaceHost, destroyCanvasSurfaceHost, hexToRgba, resolveNodeAccentColor, resolveAnchorRect, buildRect, boundsToHitRect, projectSceneBounds, getNodeSceneBounds, clearSceneHotZones, registerSceneHotZone, getSceneHitAtPoint, getSceneHitAtEvent, resolveHitNode, clearScenePopoverHover, syncSceneHoverState, resolveCanvasNodeDetailMode, setCanvasFont, drawCanvasTextLines, drawRoundedPanel, requestSceneImage, buildCanvasSnapshotBounds, reconcileRetainedLayer, drawCanvasFrame, renderGroupFrames, drawCanvasLink, renderLinks, drawCanvasBadgePill, drawCanvasProgressBadge, drawCanvasAnnotationBadges, drawNodeMediaPreview, renderCanvasMicroNode, renderCanvasInlineTextNode, renderCanvasStandardNode } = shared;
    const { requestClipboardPaste } = shared;

    function finishCanvasInteraction(state) {
        const finishInteractionFn = shared.finishInteraction;
        if (typeof finishInteractionFn === "function") {
            return finishInteractionFn(state);
        }

        return Promise.resolve();
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

    function supportsConnectionAuthoring(state) {
        return resolveSurfaceMode(state) === "authoring";
    }

    function resolveConnectorAnchorHit(event) {
        const anchor = event?.target?.closest?.(".cw-connector-anchor");
        if (!anchor?.dataset?.nodeId) {
            return null;
        }

        return {
            type: "node-port",
            nodeId: anchor.dataset.nodeId,
            portId: anchor.dataset.portId || "",
            portLabel: anchor.title || "",
            portDirection: anchor.dataset.direction || "",
            portSide: anchor.dataset.side || "",
            bounds: resolveAnchorRect(anchor)
        };
    }

    function resolvePortDirection(side, direction) {
        if (direction === "input" || direction === "output") {
            return direction;
        }

        return side === "left" || side === "top"
            ? "input"
            : "output";
    }

    function resolveConnectionPortHit(state, event, sceneHit) {
        const anchorHit = resolveConnectorAnchorHit(event);
        if (anchorHit) {
            return anchorHit;
        }

        const hitTarget = sceneHit || getSceneHitAtEvent(state, event);
        return hitTarget?.type === "node-port"
            ? hitTarget
            : null;
    }

    function buildConnectionDescriptor(portHit) {
        if (!portHit?.nodeId) {
            return null;
        }

        const side = portHit.portSide || "right";
        return {
            nodeId: portHit.nodeId,
            portId: portHit.portId || "",
            label: portHit.portLabel || "",
            direction: resolvePortDirection(side, portHit.portDirection),
            side
        };
    }

    function isSameConnectionDescriptor(left, right) {
        if (!left && !right) {
            return true;
        }

        if (!left || !right) {
            return false;
        }

        return left.nodeId === right.nodeId &&
            (left.portId || "") === (right.portId || "") &&
            (left.direction || "") === (right.direction || "") &&
            (left.side || "") === (right.side || "");
    }

    function clearConnectionDraft(state, shouldRender = true) {
        if (!state.connectionDraft && !state.connectionTarget) {
            return;
        }

        state.connectionDraft = null;
        state.connectionTarget = null;
        if (shouldRender) {
            render(state);
        }
    }

    function resolveOppositeConnectionDirection(direction) {
        return direction === "input" ? "output" : "input";
    }

    function startConnectionDraft(state, descriptor) {
        if (!descriptor || (descriptor.direction !== "output" && descriptor.direction !== "input")) {
            return false;
        }

        clearContextMenu(state);
        clearScenePopoverHover(state);
        state.connectionDraft = descriptor;
        state.connectionTarget = null;
        render(state);
        ensureHostFocus(state);
        return true;
    }

    function updateConnectionDraftTarget(state, event, sceneHit) {
        if (!state.connectionDraft) {
            if (!state.connectionTarget) {
                return false;
            }

            state.connectionTarget = null;
            return true;
        }

        const portHit = resolveConnectionPortHit(state, event, sceneHit);
        const descriptor = buildConnectionDescriptor(portHit);
        const targetDirection = resolveOppositeConnectionDirection(state.connectionDraft.direction);
        const nextTarget = descriptor &&
            descriptor.direction === targetDirection &&
            descriptor.nodeId !== state.connectionDraft.nodeId
            ? descriptor
            : null;
        if (isSameConnectionDescriptor(state.connectionTarget, nextTarget)) {
            return false;
        }

        state.connectionTarget = nextTarget;
        return true;
    }

    function dispatchConnectionCreate(state, targetDescriptor) {
        if (!state.connectionDraft || !targetDescriptor) {
            return false;
        }

        const sourceDescriptor = state.connectionDraft.direction === "input"
            ? targetDescriptor
            : state.connectionDraft;
        const inputDescriptor = state.connectionDraft.direction === "input"
            ? state.connectionDraft
            : targetDescriptor;
        dispatchContextActionRequest(state, {
            nodeId: inputDescriptor.nodeId,
            actionId: "connection:create",
            x: 0,
            y: 0,
            targetKind: "link",
            linkSourceId: sourceDescriptor.nodeId,
            linkTargetId: inputDescriptor.nodeId,
            linkKind: "flow",
            linkSourcePortId: sourceDescriptor.portId || "",
            linkTargetPortId: inputDescriptor.portId || ""
        });
        setSelection(state, [inputDescriptor.nodeId], true);
        state.connectionDraft = null;
        state.connectionTarget = null;
        render(state);
        ensureHostFocus(state);
        return true;
    }

    function attachEvents(state) {
        state.handlers = {
            pointerDown: event => {
                shared.flushDeferredViewportRender?.(state);

                const sceneHit = getSceneHitAtEvent(state, event);
                const portHit = resolveConnectionPortHit(state, event, sceneHit);
                const portDescriptor = buildConnectionDescriptor(portHit);
                const overlayAnchorHit = resolveConnectorAnchorHit(event);
                if (state.connectionDraft && event.button === 0) {
                    if (event.cancelable) {
                        event.preventDefault();
                    }

                    if (portDescriptor &&
                        portDescriptor.direction === resolveOppositeConnectionDirection(state.connectionDraft.direction) &&
                        portDescriptor.nodeId !== state.connectionDraft.nodeId) {
                        dispatchConnectionCreate(state, portDescriptor);
                        return;
                    }

                    if (portDescriptor &&
                        (portDescriptor.direction === "output" || portDescriptor.direction === "input")) {
                        startConnectionDraft(state, portDescriptor);
                        return;
                    }

                    clearConnectionDraft(state);
                    return;
                }

                if (supportsConnectionAuthoring(state) &&
                    event.button === 0 &&
                    portDescriptor &&
                    (portDescriptor.direction === "output" || portDescriptor.direction === "input")) {
                    if (event.cancelable) {
                        event.preventDefault();
                    }

                    startConnectionDraft(state, portDescriptor);
                    return;
                }

                if (isOverlayTarget(event.target) && !overlayAnchorHit) {
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

                const hitTarget = sceneHit;
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
                            linkKind: deleteTarget.link.kind,
                            linkSourcePortId: deleteTarget.link.sourcePortId || "",
                            linkTargetPortId: deleteTarget.link.targetPortId || ""
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
                        clearScenePopoverHover(state);
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

                    if (state.connectionDraft) {
                        syncSceneHoverState(state, event);
                        updateConnectionDraftTarget(state, event);
                        clearScenePopoverHover(state);
                        render(state);
                        return;
                    }

                    if (!isOverlayTarget(event.target) || resolveConnectorAnchorHit(event)) {
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
                clearConnectionDraft(state, false);
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
                const sceneHit = getSceneHitAtEvent(state, event);
                if (isOverlayTarget(event.target) && !resolveConnectorAnchorHit(event)) {
                    return;
                }

                event.preventDefault();
                const hitTarget = sceneHit;
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
                        clearConnectionDraft(state, false);
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

                if ((event.key === "Delete" || event.key === "Backspace") &&
                    !event.shiftKey &&
                    !event.ctrlKey &&
                    !event.metaKey &&
                    !event.altKey) {
                    const selectedNodeId = state.ui?.selectedNodeIds?.length === 1
                        ? state.ui.selectedNodeIds[0]
                        : null;
                    const selectedNode = selectedNodeId
                        ? state.lookups?.byId?.get?.(selectedNodeId) || null
                        : null;
                    if (selectedNode?.isInlineTextNode) {
                        event.preventDefault();
                        clearContextMenu(state);
                        dispatchContextActionRequest(state, {
                            nodeId: selectedNodeId,
                            actionId: "delete",
                            x: 0,
                            y: 0,
                            targetKind: "node"
                        });
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
                            const hadConnectionDraft = !!state.connectionDraft;
                            clearContextMenu(state);
                            closeComposer(state);
                            clearConnectionDraft(state, false);
                            shared.clearNodeHighlights?.(state, { render: true, publish: true });
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
                            else if (hadConnectionDraft) {
                                render(state);
                                ensureHostFocus(state);
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

    Object.assign(shared, {
        openNodeMetadataMenu,
        copyCompactPath,
        resolveSurfaceMode,
        isDeleteMode,
        isDependencyMode,
        syncWorkbenchMode,
        setWorkbenchToolMode,
        updatePointerHostPoint,
        dispatchContextActionRequest,
        distancePointToSegment,
        cubicBezierPoint,
        isPointNearRenderedLink,
        hitTestRenderedLink,
        resolveDeleteModeHitTarget,
        updateDeleteHoverState,
        supportsConnectionAuthoring,
        resolveConnectorAnchorHit,
        resolveConnectionPortHit,
        buildConnectionDescriptor,
        clearConnectionDraft,
        startConnectionDraft,
        updateConnectionDraftTarget,
        dispatchConnectionCreate,
        attachEvents
    });
})();
