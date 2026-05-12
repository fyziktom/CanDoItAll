(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 06-canvas-renderers.js.'); }
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition, getLinkAnchorPoint, resolveCollapseAnchorInfo, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, createMarkerBadge, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, copySelectionToClipboard, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint, hitTestNode, hitTestFrameHandle, hitTestProgressBadge, isOverlayTarget, applyFullTextTooltip, reconcileSelection, applySelection, selectSingleNode, publishSelection, clearViewportStateCommit, createSerializedStateSnapshot, invokeStateChanged, publishState, publishStateNow, scheduleViewportStateCommit, publishNodesMoved, setSelection, toggleSelection, toggleCollapse, clearContextMenu, closeComposer, ensureHostFocus, deferHostFocus, resolveComposerAnchor, layoutComposer, render, getContextActions, isCreateAction, buildCreateRequest, resolveMenuLabel, getMenuScale, isCompactHiveLayout, resolveMenuActionVariant, getActionMetrics, applyProgressPresetTone, fitContextMenuLabel, resolveActionGlyph, createMenuActionIcon, resolveMenuActionAriaLabel, getRadialOffsets, buildCompactHiveCoordinates, getCompactHiveOffsets, resolveContextMenuOffsets, resolveContextMenuSafeTop, getContextMenuLayerBounds, clampLayerBoundsToHost, positionContextMenu, getContextMenuOrbitRadius, getContextMenuLocalPoint, isPointInContextMenuLayer, closeContextMenuLayersFrom, syncContextMenuLayers, resolveSubmenuOrigin, ensureSubmenuLoadingIndicator, clearSubmenuLoadingIndicator, cancelPendingContextSubmenu, scheduleContextSubmenuOpen, clampLayerOriginToHost, getToolboxPanelSize, getToolboxPanelBounds, clampToolboxPanelOriginToHost, resolveToolboxPanelOrigin, createContextMenuLayer, shiftContextMenuLayerOrigin, nudgeContextMenuLayerIntoVisibleHost, resolveQuickCreateSourceNode, submitCreateRequest, submitNodeEdit, readFileAsUpload, commitComposer, decorateComposerShell, createComposerWizard, createComposerSection, updateComposerFileState, openCreateComposer, openInlineNoteComposer, buildChildNotePlacement, buildSiblingNotePlacement, openKeyboardNoteComposer, openExistingNoteEditor, executeContextAction, openContextSubmenu, openContextSubmenuByActionId, renderContextMenuLayer, renderToolboxPreview, renderToolboxPanelLayer, showContextMenu, legacyOpenNodeMetadataMenu, startPan, isMarqueeModifierPressed, startMarquee, ensureSelectedForDrag, startDragForNodeIds, startDrag, startFrameDrag, updateMarquee, legacyApplyMarqueeSelection, updateDrag, updatePan, isNodeVisibleInViewport, centerNodeElementInViewport, ensureNodeVisible, legacyResize, findContainingBlockOverride, suspendContainingBlock, restoreContainingBlock, setMaximized, fitView, focusNode, normalizeWheelDelta, applyWheelZoom, setZoomPercent, setMenuScalePercent, toggleHelp, isManualDoubleActivation, handleNodeDoubleActivation, legacyAttachEvents, hydrateState, refresh } = shared;
    const { getCanvasRuntimePrimitives, createFallbackHitRegistry, createCanvasHitRegistry, createCanvasSurfaceHost, destroyCanvasSurfaceHost, hexToRgba, resolveNodeAccentColor, resolveCanvasNodePaletteStyle, resolveAnchorRect, buildRect, boundsToHitRect, projectSceneBounds, getNodeSceneBounds, clearSceneHotZones, registerSceneHotZone, getSceneHitAtPoint, getSceneHitAtEvent, resolveHitNode, clearScenePopoverHover, syncSceneHoverState } = shared;
    function isActiveCanvasDrag(state) {
        const kind = state?.interaction?.kind || "";
        return kind === "drag" ||
            kind === "frame-drag" ||
            kind === "dependency-drag";
    }

    function resolveCanvasNodeDetailMode(state, projectedNodeCount) {
        if (isActiveCanvasDrag(state)) {
            return (state.ui.zoom || 1) <= 0.9 || projectedNodeCount >= 24
                ? "micro"
                : "compact";
        }

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

    function wrapCanvasInlineTextParagraphs(primitives, context, text, maxWidth, maxLines) {
        const normalized = typeof text === "string"
            ? text.replace(/\r\n/g, "\n").replace(/\r/g, "\n")
            : "";
        const paragraphs = normalized.split("\n");
        const lines = [];
        let remainingLines = Math.max(1, maxLines || 1);

        for (let index = 0; index < paragraphs.length; index += 1) {
            if (remainingLines <= 0) {
                break;
            }

            const paragraph = paragraphs[index];
            if (!paragraph.trim()) {
                lines.push("");
                remainingLines -= 1;
                continue;
            }

            const wrappedLines = primitives?.wrapText
                ? primitives.wrapText(context, paragraph, maxWidth, remainingLines)
                : [paragraph];
            const normalizedLines = Array.isArray(wrappedLines) && wrappedLines.length > 0
                ? wrappedLines
                : [paragraph];

            for (const line of normalizedLines) {
                if (remainingLines <= 0) {
                    break;
                }

                lines.push(line);
                remainingLines -= 1;
            }
        }

        return lines.length > 0
            ? lines
            : [""];
    }

    function countCanvasInlineTextMetaItems(node) {
        const markerCount = Array.isArray(node?.markers) && node.markers.length > 0
            ? Math.min(3, node.markers.length)
            : node?.markerIcon
                ? 1
                : 0;
        const annotationCount = Array.isArray(node?.annotations)
            ? Math.min(3, node.annotations.length)
            : 0;
        const statusCount = node?.statusPill ? 1 : 0;
        const priorityCount = node?.priority > 0 ? 1 : 0;

        return 1 + markerCount + annotationCount + statusCount + priorityCount;
    }

    function traceRoundedPanelPath(context, bounds, radius) {
        const width = Math.max(0, bounds.width || 0);
        const height = Math.max(0, bounds.height || 0);
        const safeRadius = Math.max(0, Math.min(radius || 0, width / 2, height / 2));
        if (typeof context.roundRect === "function") {
            context.beginPath();
            context.roundRect(bounds.left, bounds.top, width, height, safeRadius);
            return;
        }

        context.beginPath();
        context.moveTo(bounds.left + safeRadius, bounds.top);
        context.lineTo(bounds.left + width - safeRadius, bounds.top);
        context.quadraticCurveTo(bounds.left + width, bounds.top, bounds.left + width, bounds.top + safeRadius);
        context.lineTo(bounds.left + width, bounds.top + height - safeRadius);
        context.quadraticCurveTo(bounds.left + width, bounds.top + height, bounds.left + width - safeRadius, bounds.top + height);
        context.lineTo(bounds.left + safeRadius, bounds.top + height);
        context.quadraticCurveTo(bounds.left, bounds.top + height, bounds.left, bounds.top + height - safeRadius);
        context.lineTo(bounds.left, bounds.top + safeRadius);
        context.quadraticCurveTo(bounds.left, bounds.top, bounds.left + safeRadius, bounds.top);
        context.closePath();
    }

    function drawRoundedPanel(context, bounds, radius, fill, stroke, lineWidth, shadowColor) {
        context.save();
        if (shadowColor) {
            context.shadowColor = shadowColor;
            context.shadowBlur = 20;
            context.shadowOffsetY = 8;
        }

        traceRoundedPanelPath(context, bounds, radius);
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
            highlighted: !!extra?.highlighted,
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

    function resolveCanvasLinkControlVector(side) {
        switch ((side || "").toLowerCase()) {
            case "left":
                return { x: -1, y: 0 };
            case "top":
                return { x: 0, y: -1 };
            case "bottom":
                return { x: 0, y: 1 };
            default:
                return { x: 1, y: 0 };
        }
    }

    function resolveCanvasAnchorSideFromHostBounds(bounds, point, detailMode) {
        const centerX = bounds.left + (bounds.width / 2);
        const centerY = bounds.top + (bounds.height / 2);
        const pointX = Number.isFinite(point?.x) ? point.x : centerX;
        const pointY = Number.isFinite(point?.y) ? point.y : centerY;
        const deltaX = pointX - centerX;
        const deltaY = pointY - centerY;
        if ((detailMode || "").toLowerCase() === "micro" && Math.abs(deltaY) > Math.abs(deltaX)) {
            return deltaY >= 0 ? "bottom" : "top";
        }

        return deltaX >= 0 ? "right" : "left";
    }

    function buildCanvasLinkGeometry(startPoint, endPoint, sourceSide, targetSide, routeOffset) {
        const controlOffset = Math.max(
            56,
            Math.max(Math.abs(endPoint.x - startPoint.x), Math.abs(endPoint.y - startPoint.y)) * 0.38);
        const sourceVector = resolveCanvasLinkControlVector(sourceSide);
        const targetVector = resolveCanvasLinkControlVector(targetSide);
        const routeOffsetY = Number.isFinite(routeOffset) ? routeOffset : 0;
        const controlPoint1 = {
            x: startPoint.x + (controlOffset * sourceVector.x),
            y: startPoint.y + (controlOffset * sourceVector.y) + routeOffsetY
        };
        const controlPoint2 = {
            x: endPoint.x + (controlOffset * targetVector.x),
            y: endPoint.y + (controlOffset * targetVector.y) + routeOffsetY
        };
        const midPoint = sampleCubicBezierPoint(startPoint, controlPoint1, controlPoint2, endPoint, 0.5);
        const labelPoint = sampleCubicBezierPoint(
            startPoint,
            controlPoint1,
            controlPoint2,
            endPoint,
            Math.abs(routeOffsetY) > 0.1 ? 0.32 : 0.5);
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
            labelPoint: {
                x: round(labelPoint.x),
                y: round(labelPoint.y)
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

    function resolveCanvasPortVisualStyle(port, paletteStyle) {
        const accentColor = (port?.accentColor || "").trim();
        if (accentColor) {
            return {
                categoryKey: port?.categoryKey || "",
                accentColor,
                fill: hexToRgba(accentColor, 0.16),
                stroke: hexToRgba(accentColor, 0.44),
                text: accentColor,
                anchor: hexToRgba(accentColor, 0.96),
                halo: hexToRgba(accentColor, 0.2),
                line: hexToRgba(accentColor, 0.88)
            };
        }

        switch ((port?.tone || "").toLowerCase()) {
            case "accent":
                return {
                    categoryKey: port?.categoryKey || "",
                    accentColor: "#3b82f6",
                    fill: "rgba(59, 130, 246, 0.18)",
                    stroke: "rgba(37, 99, 235, 0.48)",
                    text: "rgba(219, 234, 254, 0.98)",
                    anchor: "rgba(59, 130, 246, 0.94)",
                    halo: "rgba(59, 130, 246, 0.18)",
                    line: "rgba(59, 130, 246, 0.88)"
                };
            case "success":
                return {
                    categoryKey: port?.categoryKey || "",
                    accentColor: "#22c55e",
                    fill: "rgba(34, 197, 94, 0.18)",
                    stroke: "rgba(22, 163, 74, 0.48)",
                    text: "rgba(220, 252, 231, 0.98)",
                    anchor: "rgba(34, 197, 94, 0.92)",
                    halo: "rgba(34, 197, 94, 0.18)",
                    line: "rgba(34, 197, 94, 0.86)"
                };
            case "warning":
            case "warn":
                return {
                    categoryKey: port?.categoryKey || "",
                    accentColor: "#f59e0b",
                    fill: "rgba(245, 158, 11, 0.18)",
                    stroke: "rgba(217, 119, 6, 0.48)",
                    text: "rgba(254, 243, 199, 0.98)",
                    anchor: "rgba(245, 158, 11, 0.92)",
                    halo: "rgba(245, 158, 11, 0.18)",
                    line: "rgba(245, 158, 11, 0.88)"
                };
            case "danger":
                return {
                    categoryKey: port?.categoryKey || "",
                    accentColor: "#ef4444",
                    fill: "rgba(239, 68, 68, 0.18)",
                    stroke: "rgba(220, 38, 38, 0.48)",
                    text: "rgba(254, 226, 226, 0.98)",
                    anchor: "rgba(239, 68, 68, 0.92)",
                    halo: "rgba(239, 68, 68, 0.18)",
                    line: "rgba(239, 68, 68, 0.88)"
                };
            default:
                return {
                    categoryKey: port?.categoryKey || "",
                    accentColor: "",
                    fill: paletteStyle?.subtleFill || "rgba(255, 255, 255, 0.94)",
                    stroke: paletteStyle?.subtleStroke || "rgba(148, 163, 184, 0.34)",
                    text: paletteStyle?.subtleText || "rgba(71, 85, 105, 0.96)",
                    anchor: paletteStyle?.iconFill || "rgba(71, 85, 105, 0.88)",
                    halo: "rgba(148, 163, 184, 0.16)",
                    line: paletteStyle?.iconFill || "rgba(71, 85, 105, 0.82)"
                };
        }
    }

    function findNodePort(node, portId, direction) {
        if (!node || !portId) {
            return null;
        }

        const ports = direction === "input"
            ? (Array.isArray(node.inputPorts) ? node.inputPorts : [])
            : (Array.isArray(node.outputPorts) ? node.outputPorts : []);
        return ports.find(port => port?.id === portId) || null;
    }

    function resolveCanvasLinkConnectionStyle(sourcePort, targetPort) {
        if (sourcePort) {
            return resolveCanvasPortVisualStyle(sourcePort, null);
        }

        if (targetPort) {
            return resolveCanvasPortVisualStyle(targetPort, null);
        }

        return null;
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
            if (options?.connectionStyle) {
                return {
                    stroke: options.connectionStyle.line,
                    arrowFill: options.connectionStyle.anchor,
                    lineWidth: 3,
                    lineDash: [10, 6]
                };
            }

            return {
                stroke: "rgba(124, 58, 237, 0.92)",
                arrowFill: "rgba(109, 40, 217, 0.96)",
                lineWidth: 3,
                lineDash: [10, 6]
            };
        }

        if (options?.connectionStyle) {
            return {
                stroke: options.connectionStyle.line,
                arrowFill: options.connectionStyle.anchor,
                lineWidth: link?.isUserAuthored ? 3 : 2.4,
                lineDash: link?.isUserAuthored ? [12, 8] : []
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

        switch ((link?.tone || "").toLowerCase()) {
            case "success":
            case "true":
                return {
                    stroke: "rgba(20, 184, 166, 0.9)",
                    arrowFill: "rgba(15, 118, 110, 0.96)",
                    lineWidth: 3.2,
                    lineDash: []
                };
            case "danger":
            case "else":
                return {
                    stroke: "rgba(244, 63, 94, 0.9)",
                    arrowFill: "rgba(225, 29, 72, 0.96)",
                    lineWidth: 3.2,
                    lineDash: []
                };
            case "default":
            case "warning":
                return {
                    stroke: "rgba(245, 158, 11, 0.94)",
                    arrowFill: "rgba(217, 119, 6, 0.98)",
                    lineWidth: 3.2,
                    lineDash: []
                };
            case "fanout":
            case "info":
                return {
                    stroke: "rgba(14, 165, 233, 0.9)",
                    arrowFill: "rgba(2, 132, 199, 0.98)",
                    lineWidth: 3.2,
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

    function resolveCanvasLinkLabelStyle(link, fallbackStyle) {
        switch ((link?.tone || "").toLowerCase()) {
            case "success":
            case "true":
                return {
                    fill: "rgba(236, 253, 245, 0.97)",
                    stroke: "rgba(20, 184, 166, 0.42)",
                    text: "rgba(15, 118, 110, 0.98)"
                };
            case "warning":
            case "default":
                return {
                    fill: "rgba(255, 251, 235, 0.98)",
                    stroke: "rgba(245, 158, 11, 0.46)",
                    text: "rgba(146, 64, 14, 0.98)"
                };
            case "danger":
                return {
                    fill: "rgba(254, 242, 242, 0.98)",
                    stroke: "rgba(244, 63, 94, 0.44)",
                    text: "rgba(159, 18, 57, 0.98)"
                };
            case "fanout":
            case "info":
                return {
                    fill: "rgba(240, 249, 255, 0.98)",
                    stroke: "rgba(14, 165, 233, 0.42)",
                    text: "rgba(3, 105, 161, 0.98)"
                };
            default:
                return {
                    fill: "rgba(255, 255, 255, 0.96)",
                    stroke: fallbackStyle?.stroke || "rgba(100, 116, 139, 0.32)",
                    text: "rgba(30, 41, 59, 0.9)"
                };
        }
    }

    function drawCanvasLinkLabel(context, link, geometry, fallbackStyle) {
        const rawLabel = (link?.label || "").trim();
        const labelPoint = geometry?.labelPoint || geometry?.midPoint;
        if (!rawLabel || !labelPoint) {
            return;
        }

        const label = rawLabel.length > 32
            ? `${rawLabel.slice(0, 29)}...`
            : rawLabel;
        const style = resolveCanvasLinkLabelStyle(link, fallbackStyle);
        context.save();
        setCanvasFont(context, 800, 11);
        const textWidth = Math.min(180, Math.ceil(context.measureText(label).width));
        const width = textWidth + 20;
        const height = 24;
        const bounds = buildRect(
            labelPoint.x - (width / 2),
            labelPoint.y - (height / 2),
            width,
            height);
        drawRoundedPanel(context, bounds, 12, style.fill, style.stroke, 1, "rgba(15, 23, 42, 0.1)");
        context.fillStyle = style.text;
        context.textBaseline = "middle";
        context.textAlign = "center";
        context.fillText(label, labelPoint.x, labelPoint.y + 0.5);
        context.restore();
    }

    function renderCanvasLinkLabels(context, renderedLinks) {
        if (!context || !Array.isArray(renderedLinks)) {
            return;
        }

        for (const link of renderedLinks) {
            drawCanvasLinkLabel(context, link, link, resolveCanvasLinkStyle(link, null));
        }
    }

    function drawCanvasLink(context, link, startPoint, endPoint, options) {
        const geometry = buildCanvasLinkGeometry(
            startPoint,
            endPoint,
            options?.sourceSide || (endPoint.x >= startPoint.x ? "right" : "left"),
            options?.targetSide || (endPoint.x >= startPoint.x ? "left" : "right"),
            options?.routeOffset || 0);
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
            drawCanvasLinkLabel(context, link, geometry, style);
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

        drawCanvasLinkLabel(context, link, geometry, style);
        return geometry;
    }

    function renderLinks(state, visibleNodes, options) {
        const surface = state.linkSurface;
        if (!surface) {
            return;
        }

        surface.clear();
        state.renderedLinks = [];
        state.previewLink = null;
        const linkFilter = typeof options?.linkFilter === "function"
            ? options.linkFilter
            : null;
        const includePreviewLinks = options?.includePreviewLinks !== false;
        // Keep links visible when one endpoint moves outside the viewport.
        // Only collapsed or otherwise hidden nodes should suppress a connection.
        const graphVisibleNodeIds = new Set(getVisibleNodes(state).map(node => node.id));
        const detailMode = resolveCanvasNodeDetailMode(state, (visibleNodes || []).length);
        const nextEntries = new Map();
        let renderedLinkCount = 0;
        const outgoingLinksBySource = new Map();
        for (const [index, link] of state.surface.links.entries()) {
            if (!link?.sourceId || !link?.targetId) {
                continue;
            }

            const sourceLinks = outgoingLinksBySource.get(link.sourceId) || [];
            sourceLinks.push({ index, link });
            outgoingLinksBySource.set(link.sourceId, sourceLinks);
        }

        const routeOffsetsByLinkIndex = new Map();
        for (const sourceLinks of outgoingLinksBySource.values()) {
            if (sourceLinks.length <= 1) {
                continue;
            }

            const zoom = Math.max(state.ui.zoom || 1, 0.01);
            const offsetStep = Math.max(28, Math.min(52, 42 * zoom));
            const centerIndex = (sourceLinks.length - 1) / 2;
            for (let sourceLinkIndex = 0; sourceLinkIndex < sourceLinks.length; sourceLinkIndex += 1) {
                routeOffsetsByLinkIndex.set(
                    sourceLinks[sourceLinkIndex].index,
                    (sourceLinkIndex - centerIndex) * offsetStep);
            }
        }

        for (const [index, link] of state.surface.links.entries()) {
            if (!graphVisibleNodeIds.has(link.sourceId) || !graphVisibleNodeIds.has(link.targetId)) {
                continue;
            }

            if (linkFilter && !linkFilter(link, index)) {
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
            const sourceAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, source, anchorSides.sourceSide, link.sourcePortId, "output", detailMode));
            const targetAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, target, anchorSides.targetSide, link.targetPortId, "input", detailMode));
            const connectionStyle = resolveCanvasLinkConnectionStyle(
                findNodePort(source, link.sourcePortId, "output"),
                findNodePort(target, link.targetPortId, "input"));
            const retainedKey = getLinkRetainedKey(link, index);
            const routeOffset = routeOffsetsByLinkIndex.get(index) || 0;
            const geometry = drawCanvasLink(surface.context, link, sourceAnchor, targetAnchor, {
                key: retainedKey,
                sourceSide: sourceAnchor.side || anchorSides.sourceSide,
                targetSide: targetAnchor.side || anchorSides.targetSide,
                routeOffset,
                isHovered: state.surface?.mode === "delete" && state.hoveredDeleteLinkKey === retainedKey,
                connectionStyle
            });
            nextEntries.set(retainedKey, {
                signature: JSON.stringify({
                    sourceId: link.sourceId,
                    sourcePortId: link.sourcePortId || "",
                    targetId: link.targetId,
                    targetPortId: link.targetPortId || "",
                    kind: link.kind || "",
                    label: link.label || "",
                    tone: link.tone || "",
                    routeOffset,
                    flow: !!link.isUserAuthored,
                    hovered: state.hoveredDeleteLinkKey === retainedKey
                })
            });
            state.renderedLinks.push({
                key: retainedKey,
                sourceId: link.sourceId,
                sourcePortId: link.sourcePortId || "",
                targetId: link.targetId,
                targetPortId: link.targetPortId || "",
                kind: link.kind || "",
                label: link.label || "",
                tone: link.tone || "",
                isUserAuthored: !!link.isUserAuthored,
                startPoint: geometry.startPoint,
                endPoint: geometry.endPoint,
                controlPoint1: geometry.controlPoint1,
                controlPoint2: geometry.controlPoint2,
                midPoint: geometry.midPoint,
                labelPoint: geometry.labelPoint,
                bounds: geometry.bounds
            });
            renderedLinkCount += 1;
        }

        const dependencySourceId = state.surface?.dependencySourceId || "";
        if (includePreviewLinks &&
            state.surface?.mode === "dependency" &&
            dependencySourceId &&
            graphVisibleNodeIds.has(dependencySourceId) &&
            state.lookups.byId.has(dependencySourceId)) {
            const previewSource = state.lookups.byId.get(dependencySourceId);
            const hoveredTargetId = state.hoveredNodeId &&
                state.hoveredNodeId !== dependencySourceId &&
                graphVisibleNodeIds.has(state.hoveredNodeId)
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
                const anchorSides = shared.resolveLinkAnchorSides?.(state, previewSource, previewTarget, detailMode) || {
                    sourceSide: "right",
                    targetSide: "left"
                };
                const sourceAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, previewSource, anchorSides.sourceSide, previewLink.sourcePortId, "output", detailMode));
                const targetAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, previewTarget, anchorSides.targetSide, previewLink.targetPortId, "input", detailMode));
                const previewConnectionStyle = resolveCanvasLinkConnectionStyle(
                    findNodePort(previewSource, previewLink.sourcePortId, "output"),
                    findNodePort(previewTarget, previewLink.targetPortId, "input"));
                const previewGeometry = drawCanvasLink(surface.context, previewLink, sourceAnchor, targetAnchor, {
                    isPreview: true,
                    sourceSide: sourceAnchor.side || anchorSides.sourceSide,
                    targetSide: targetAnchor.side || anchorSides.targetSide,
                    forceArrow: true,
                    connectionStyle: previewConnectionStyle
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
                const sourceSide = resolveCanvasAnchorSideFromHostBounds(previewSourceBounds, state.pointerHostPoint, detailMode);
                const sourceAnchor = worldToHostPoint(state, getLinkAnchorPoint(state, previewSource, sourceSide, previewLink.sourcePortId, "output", detailMode));
                const previewConnectionStyle = resolveCanvasLinkConnectionStyle(
                    findNodePort(previewSource, previewLink.sourcePortId, "output"),
                    null);
                const previewGeometry = drawCanvasLink(surface.context, previewLink, sourceAnchor, state.pointerHostPoint, {
                    isPreview: true,
                    sourceSide: sourceAnchor.side || sourceSide,
                    targetSide: "left",
                    forceArrow: true,
                    connectionStyle: previewConnectionStyle
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

        if (includePreviewLinks && state.connectionDraft) {
            const outputDescriptor = state.connectionDraft.direction === "input"
                ? state.connectionTarget
                : state.connectionDraft;
            const inputDescriptor = state.connectionDraft.direction === "input"
                ? state.connectionDraft
                : state.connectionTarget;
            const previewSource = outputDescriptor
                ? resolveConnectionAnchorHostPoint(state, outputDescriptor, "output")
                : state.pointerHostPoint;
            const previewTarget = inputDescriptor
                ? resolveConnectionAnchorHostPoint(state, inputDescriptor, "input")
                : null;
            const previewEndpoint = previewTarget || state.pointerHostPoint;
            if (previewSource && previewEndpoint) {
                const previewSourceNode = outputDescriptor?.nodeId
                    ? state.lookups.byId.get(outputDescriptor.nodeId) || null
                    : null;
                const previewTargetNode = inputDescriptor?.nodeId
                    ? state.lookups.byId.get(inputDescriptor.nodeId) || null
                    : null;
                const previewConnectionStyle = resolveCanvasLinkConnectionStyle(
                    findNodePort(previewSourceNode, outputDescriptor?.portId || "", "output"),
                    findNodePort(previewTargetNode, inputDescriptor?.portId || "", "input"));
                const previewGeometry = drawCanvasLink(
                    surface.context,
                    {
                        sourceId: outputDescriptor?.nodeId || "",
                        sourcePortId: outputDescriptor?.portId || "",
                        targetId: inputDescriptor?.nodeId || "",
                        targetPortId: inputDescriptor?.portId || "",
                        kind: "flow",
                        isUserAuthored: true
                    },
                    previewSource,
                    previewEndpoint,
                    {
                        isPreview: true,
                        sourceSide: previewSource.side || outputDescriptor?.side || "right",
                        targetSide: previewTarget?.side || inputDescriptor?.side || "left",
                        forceArrow: true,
                        connectionStyle: previewConnectionStyle
                    });
                state.previewLink = {
                    sourceId: outputDescriptor?.nodeId || null,
                    sourcePortId: outputDescriptor?.portId || "",
                    targetId: inputDescriptor?.nodeId || null,
                    targetPortId: inputDescriptor?.portId || "",
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

    function isWorkflowDecisionNode(node) {
        const family = (node?.family || "").toLowerCase();
        const paletteKey = (node?.paletteKey || "").toLowerCase();
        return family === "workflow-decision" ||
            paletteKey === "workflow-decision";
    }

    function traceCanvasDecisionDiamond(context, centerX, centerY, radius) {
        context.beginPath();
        context.moveTo(centerX, centerY - radius);
        context.lineTo(centerX + radius, centerY);
        context.lineTo(centerX, centerY + radius);
        context.lineTo(centerX - radius, centerY);
        context.closePath();
    }

    function drawCanvasDecisionCues(context, state, node, hostBounds, paletteStyle) {
        if (!isWorkflowDecisionNode(node)) {
            return;
        }

        const zoom = Math.max(state?.ui?.zoom || 1, 0.01);
        const markerRadius = Math.max(5, 7 * zoom);
        const centerY = hostBounds.top + (hostBounds.height / 2);
        const leftX = hostBounds.left - Math.max(2, 3 * zoom);
        const rightX = hostBounds.right + Math.max(2, 3 * zoom);

        context.save();
        context.lineWidth = Math.max(1.1, 1.5 * zoom);
        context.fillStyle = paletteStyle.iconFill || "rgba(255, 251, 235, 0.9)";
        context.strokeStyle = paletteStyle.iconStroke || "rgba(245, 158, 11, 0.56)";
        context.shadowColor = "rgba(15, 118, 110, 0.16)";
        context.shadowBlur = Math.max(4, 8 * zoom);
        traceCanvasDecisionDiamond(context, leftX, centerY, markerRadius);
        context.fill();
        context.stroke();
        traceCanvasDecisionDiamond(context, rightX, centerY, markerRadius);
        context.fill();
        context.stroke();
        context.restore();

        const forkX = hostBounds.right - Math.max(28, 34 * zoom);
        const branchX = hostBounds.right - Math.max(10, 14 * zoom);
        const forkGap = Math.max(8, 12 * zoom);
        const dotRadius = Math.max(2.2, 3.2 * zoom);
        context.save();
        context.globalAlpha = 0.82;
        context.strokeStyle = "rgba(245, 158, 11, 0.58)";
        context.fillStyle = "rgba(15, 118, 110, 0.78)";
        context.lineWidth = Math.max(1.2, 1.8 * zoom);
        context.lineCap = "round";
        context.beginPath();
        context.moveTo(forkX, centerY);
        context.lineTo(branchX, centerY - forkGap);
        context.moveTo(forkX, centerY);
        context.lineTo(branchX, centerY + forkGap);
        context.stroke();
        context.beginPath();
        context.moveTo(forkX + dotRadius, centerY);
        context.arc(forkX, centerY, dotRadius, 0, Math.PI * 2);
        context.moveTo(branchX + dotRadius, centerY - forkGap);
        context.arc(branchX, centerY - forkGap, dotRadius, 0, Math.PI * 2);
        context.moveTo(branchX + dotRadius, centerY + forkGap);
        context.arc(branchX, centerY + forkGap, dotRadius, 0, Math.PI * 2);
        context.fill();
        context.restore();
    }

    function renderCanvasDecisionNode(context, state, node, hostBounds, accent, detailMode, meta) {
        const isSelected = state.selectedIds.has(node.id);
        const paletteStyle = resolveCanvasNodePaletteStyle(node, accent, isSelected);
        const zoom = Math.max(state?.ui?.zoom || 1, 0.01);
        const centerX = hostBounds.left + (hostBounds.width / 2);
        const centerY = hostBounds.top + (hostBounds.height / 2);
        const radius = Math.max(42 * zoom, Math.min(hostBounds.width, hostBounds.height) * 0.46);

        context.save();
        context.shadowColor = isSelected ? "rgba(15, 118, 110, 0.2)" : "rgba(15, 23, 42, 0.12)";
        context.shadowBlur = Math.max(10, 16 * zoom);
        context.shadowOffsetY = Math.max(4, 8 * zoom);
        traceCanvasDecisionDiamond(context, centerX, centerY, radius);
        context.fillStyle = "rgba(255, 255, 255, 0.98)";
        context.fill();
        context.lineWidth = isSelected ? Math.max(2.2, 3 * zoom) : Math.max(1.6, 2.2 * zoom);
        context.strokeStyle = accent || "rgba(20, 184, 166, 0.9)";
        context.stroke();
        context.restore();

        const innerRadius = radius * 0.42;
        context.save();
        context.lineWidth = Math.max(1.6, 2.2 * zoom);
        context.lineCap = "round";
        context.lineJoin = "round";
        context.strokeStyle = "rgba(51, 65, 85, 0.9)";
        context.fillStyle = "rgba(20, 184, 166, 0.9)";
        context.beginPath();
        context.moveTo(centerX - innerRadius * 0.45, centerY);
        context.lineTo(centerX + innerRadius * 0.08, centerY);
        context.lineTo(centerX + innerRadius * 0.52, centerY - innerRadius * 0.34);
        context.moveTo(centerX + innerRadius * 0.08, centerY);
        context.lineTo(centerX + innerRadius * 0.52, centerY + innerRadius * 0.34);
        context.stroke();
        for (const point of [
            { x: centerX - innerRadius * 0.45, y: centerY },
            { x: centerX + innerRadius * 0.52, y: centerY - innerRadius * 0.34 },
            { x: centerX + innerRadius * 0.52, y: centerY + innerRadius * 0.34 }
        ]) {
            context.beginPath();
            context.arc(point.x, point.y, Math.max(2.4, 3.3 * zoom), 0, Math.PI * 2);
            context.fill();
        }

        context.restore();

        context.save();
        setCanvasFont(context, 800, Math.max(10, 13.5 * zoom));
        context.fillStyle = "rgba(15, 23, 42, 0.92)";
        context.textAlign = "center";
        context.textBaseline = "middle";
        const label = (node.title || node.kind || "Decision").trim().toUpperCase();
        const primitives = getCanvasRuntimePrimitives();
        const displayLabel = primitives?.fitText
            ? primitives.fitText(context, label, radius * 1.1, "...")
            : label;
        context.fillText(displayLabel, centerX, centerY + radius * 0.33);
        context.restore();

        const anchorRadius = Math.max(5, 6.5 * zoom);
        const anchors = [
            { x: centerX - radius, y: centerY, direction: "input", side: "left" },
            { x: centerX + radius, y: centerY, direction: "output", side: "right" }
        ];
        for (const anchor of anchors) {
            context.save();
            context.beginPath();
            context.arc(anchor.x, anchor.y, anchorRadius, 0, Math.PI * 2);
            context.fillStyle = "rgba(255, 255, 255, 0.98)";
            context.fill();
            context.lineWidth = Math.max(1.4, 2 * zoom);
            context.strokeStyle = accent || "rgba(20, 184, 166, 0.92)";
            context.stroke();
            context.restore();

            const bounds = buildRect(
                anchor.x - anchorRadius - Math.max(8, 10 * zoom),
                anchor.y - anchorRadius - Math.max(8, 10 * zoom),
                (anchorRadius + Math.max(8, 10 * zoom)) * 2,
                (anchorRadius + Math.max(8, 10 * zoom)) * 2);
            registerCanvasPortHotZone(
                state,
                node,
                bounds,
                anchor.direction === "input"
                    ? (Array.isArray(node.inputPorts) ? node.inputPorts[0] : null)
                    : (Array.isArray(node.outputPorts) ? node.outputPorts[0] : null),
                anchor.side,
                anchor.direction);
        }

        if ((node.branchLabel || "").trim() && detailMode !== "compact") {
            const labelHeight = Math.max(19, 22 * zoom);
            const labelWidth = Math.min(radius * 1.45, Math.max(74, (node.branchLabel.length * 6.5 * zoom) + 22));
            drawCanvasBadgePill(
                context,
                buildRect(centerX - (labelWidth / 2), centerY - radius - (labelHeight / 2), labelWidth, labelHeight),
                node.branchLabel,
                "rgba(240, 253, 250, 0.98)",
                "rgba(20, 184, 166, 0.38)",
                "rgba(15, 118, 110, 0.98)",
                Math.max(8, 9.5 * zoom));
        }

        meta.portCount = Math.max(
            Array.isArray(node.inputPorts) ? node.inputPorts.length : 0,
            Array.isArray(node.outputPorts) ? node.outputPorts.length : 0);
    }

    function drawCanvasBranchLabelPill(context, state, node, startX, top, maxWidth, height, paletteStyle) {
        const branchLabel = (node?.branchLabel || "").trim();
        if (!branchLabel || maxWidth <= 0) {
            return;
        }

        const zoom = Math.max(state?.ui?.zoom || 1, 0.01);
        const fontSize = Math.max(8, 9.5 * zoom);
        const horizontalPadding = Math.max(16, 20 * zoom);
        context.save();
        setCanvasFont(context, 700, fontSize);
        const measuredWidth = Math.ceil(context.measureText(branchLabel).width + horizontalPadding);
        const width = Math.min(maxWidth, Math.max(58, measuredWidth));
        if (width <= 28) {
            context.restore();
            return;
        }

        const textWidth = Math.max(8, width - horizontalPadding);
        const primitives = getCanvasRuntimePrimitives();
        const displayLabel = primitives?.fitText
            ? primitives.fitText(context, branchLabel, textWidth, "...")
            : branchLabel;
        context.restore();

        drawCanvasBadgePill(
            context,
            buildRect(startX, top, width, height),
            displayLabel,
            paletteStyle.iconFill,
            paletteStyle.iconStroke,
            paletteStyle.iconText,
            fontSize);
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
        drawCanvasDecisionCues(context, state, node, hostBounds, paletteStyle);
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
        drawCanvasDecisionCues(context, state, node, hostBounds, paletteStyle);
        context.save();
        const padding = Math.max(12, 18 * state.ui.zoom);
        const contentWidth = Math.max(24, hostBounds.width - (padding * 2));
        const noteText = node.inlineText || node.title || node.leadText || "Write note";
        const lineHeight = Math.max(14, 18 * state.ui.zoom);
        const metaRows = Math.max(1, Math.ceil(countCanvasInlineTextMetaItems(node) / 4));
        const reservedBottom = Math.max(26, 30 * state.ui.zoom) + ((metaRows - 1) * Math.max(12, 16 * state.ui.zoom));
        const maxTextLines = detailMode === "compact"
            ? 2
            : Math.max(2, Math.floor(Math.max(lineHeight, hostBounds.height - (padding * 2) - reservedBottom) / lineHeight));
        setCanvasFont(context, 600, Math.max(10, 13 * state.ui.zoom));
        const primitives = getCanvasRuntimePrimitives();
        const lines = wrapCanvasInlineTextParagraphs(
            primitives,
            context,
            noteText,
            contentWidth,
            maxTextLines);
        drawCanvasTextLines(
            context,
            lines,
            hostBounds.left + padding,
            hostBounds.top + padding + Math.max(10, 14 * state.ui.zoom),
            lineHeight,
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

    function measureCanvasWrappedLines(text, maxWidth, maxLines, font) {
        const normalizedText = (text || "").trim();
        if (!normalizedText) {
            return [];
        }

        const measureService = getTextMeasureService();
        if (measureService && typeof measureService.measure === "function") {
            const result = measureService.measure({
                text: normalizedText,
                maxWidth,
                maxLines,
                font
            });
            if (Array.isArray(result?.lines) && result.lines.length > 0) {
                return result.lines.map(line => line.text);
            }
        }

        return [normalizedText];
    }

    function getCanvasAdvancedNodePortLayout(state, node, hostBounds, detailMode) {
        const zoom = Math.max(state?.ui?.zoom || 1, 0.01);
        const resolvedDetailMode = detailMode ||
            (zoom <= 0.3
                ? "micro"
                : zoom <= 0.55
                    ? "compact"
                    : "full");
        const padding = Math.max(10, 14 * zoom);
        const contentLeft = hostBounds.left + padding;
        const contentWidth = Math.max(48, hostBounds.width - (padding * 2));
        const inputPorts = Array.isArray(node?.inputPorts) ? node.inputPorts : [];
        const outputPorts = Array.isArray(node?.outputPorts) ? node.outputPorts : [];
        const portCount = Math.max(inputPorts.length, outputPorts.length);
        const hasBothSides = inputPorts.length > 0 && outputPorts.length > 0;
        const columnGap = Math.max(10, 12 * zoom);
        const columnWidth = hasBothSides
            ? Math.max(72, (contentWidth - columnGap) / 2)
            : contentWidth;
        const inputColumnLeft = contentLeft;
        const outputColumnLeft = hasBothSides
            ? hostBounds.right - padding - columnWidth
            : contentLeft;
        const kindBadgeHeight = Math.max(20, 24 * zoom);
        const titleLineHeight = Math.max(12, 16 * zoom);
        const supportingLineHeight = Math.max(9, 12 * zoom);
        const titleGap = Math.max(3, 4 * zoom);
        const supportingGap = Math.max(5, 6 * zoom);
        const headerGap = Math.max(5, 6 * zoom);
        const portSectionGap = Math.max(6, 7 * zoom);
        const titleLines = measureCanvasWrappedLines(
            node?.title || "Untitled",
            contentWidth,
            resolvedDetailMode === "compact" ? 1 : 2,
            {
                family: "\"DM Sans\", \"Segoe UI\", sans-serif",
                sizePx: Math.max(10, 16 * zoom),
                weight: 700,
                lineHeightPx: titleLineHeight
            });
        const supportingText = node?.subtitle || node?.leadText || "";
        const supportingLines = measureCanvasWrappedLines(
            supportingText,
            contentWidth,
            resolvedDetailMode === "compact" ? 1 : 2,
            {
                family: "\"DM Sans\", \"Segoe UI\", sans-serif",
                sizePx: Math.max(8, 11.5 * zoom),
                weight: 500,
                lineHeightPx: supportingLineHeight
            });
        const titleTop = hostBounds.top + padding + kindBadgeHeight + Math.max(8, 10 * zoom);
        const supportingTop = titleTop +
            (titleLines.length * titleLineHeight) +
            titleGap +
            Math.max(3, 5 * zoom);

        let cursorY = hostBounds.top + padding;
        cursorY += kindBadgeHeight;
        cursorY += (titleLines.length * titleLineHeight) + titleGap;
        if (supportingLines.length > 0) {
            cursorY += (supportingLines.length * supportingLineHeight) + supportingGap;
        }

        const headerBaselineY = cursorY + headerGap;
        const portAreaTop = headerBaselineY + portSectionGap;
        const footerHeight = Math.max(18, 20 * zoom);
        const footerTop = hostBounds.bottom - padding - footerHeight;
        const minimumRowCount = Math.max(1, portCount);
        let portHeight = Math.max(18, 20 * zoom);
        let portGap = Math.max(3, 4.5 * zoom);
        const availableHeight = Math.max(portHeight, footerTop - portAreaTop);
        let requiredHeight = (minimumRowCount * portHeight) + (Math.max(0, minimumRowCount - 1) * portGap);
        if (requiredHeight > availableHeight && minimumRowCount > 1) {
            portGap = Math.max(1.5 * zoom, (availableHeight - (minimumRowCount * portHeight)) / (minimumRowCount - 1));
            requiredHeight = (minimumRowCount * portHeight) + (Math.max(0, minimumRowCount - 1) * portGap);
            if (requiredHeight > availableHeight) {
                portHeight = Math.max(14, (availableHeight - (Math.max(0, minimumRowCount - 1) * Math.max(1.5 * zoom, portGap))) / minimumRowCount);
                portGap = Math.max(1.5 * zoom, (availableHeight - (minimumRowCount * portHeight)) / Math.max(1, minimumRowCount - 1));
            }
        }

        const buildPortEntries = (ports, side, columnLeft) => ports.map((port, index) => {
            const top = portAreaTop + (index * (portHeight + portGap));
            return {
                port,
                side,
                bounds: buildRect(columnLeft, top, columnWidth, portHeight)
            };
        });

        return {
            padding,
            contentLeft,
            contentWidth,
            titleLines,
            titleLineHeight,
            supportingLines,
            supportingLineHeight,
            kindBadgeHeight,
            titleTop,
            supportingTop,
            inputColumnLeft,
            outputColumnLeft,
            columnWidth,
            headerBaselineY,
            footerHeight,
            footerTop,
            inputEntries: buildPortEntries(inputPorts, "left", inputColumnLeft),
            outputEntries: buildPortEntries(outputPorts, "right", outputColumnLeft)
        };
    }

    function buildCanvasPortAnchorBounds(bounds, side, zoom) {
        const anchorRadius = Math.max(3, 4 * zoom);
        const anchorX = side === "right"
            ? bounds.right - Math.max(10, 12 * zoom)
            : bounds.left + Math.max(10, 12 * zoom);
        const anchorY = bounds.top + (bounds.height / 2);
        const hitRadius = Math.max(anchorRadius + 6, 10 * zoom);
        return buildRect(anchorX - hitRadius, anchorY - hitRadius, hitRadius * 2, hitRadius * 2);
    }

    function registerCanvasPortHotZone(state, node, bounds, port, side, direction) {
        registerSceneHotZone(state, buildCanvasPortAnchorBounds(bounds, side, state.ui.zoom), {
            type: "node-port",
            nodeId: node.id,
            portId: port?.id || "",
            portLabel: port?.label || "",
            portDirection: direction,
            portSide: side
        });
    }

    function resolveConnectionAnchorHostPoint(state, descriptor, fallbackDirection) {
        if (!descriptor?.nodeId || !state.lookups?.byId?.has(descriptor.nodeId)) {
            return null;
        }

        const node = state.lookups.byId.get(descriptor.nodeId);
        const anchorSide = descriptor.side ||
            (descriptor.direction === "input" ? "left" : "right");
        const anchorPoint = worldToHostPoint(
            state,
            getLinkAnchorPoint(
                state,
                node,
                anchorSide,
                descriptor.portId || "",
                descriptor.direction || fallbackDirection || (anchorSide === "left" || anchorSide === "top" ? "input" : "output")));

        return {
            x: anchorPoint.x,
            y: anchorPoint.y,
            side: anchorPoint.side || anchorSide
        };
    }

    function drawCanvasPortPill(context, bounds, port, side, paletteStyle, zoom) {
        const toneStyle = resolveCanvasPortVisualStyle(port, paletteStyle);
        drawRoundedPanel(
            context,
            bounds,
            Math.max(10, 12 * zoom),
            toneStyle.fill,
            toneStyle.stroke,
            1,
            "");

        const anchorRadius = Math.max(3, 4 * zoom);
        const anchorX = side === "right"
            ? bounds.right - Math.max(10, 12 * zoom)
            : bounds.left + Math.max(10, 12 * zoom);
        const anchorY = bounds.top + (bounds.height / 2);
        context.save();
        context.beginPath();
        context.arc(anchorX, anchorY, anchorRadius, 0, Math.PI * 2);
        context.fillStyle = toneStyle.anchor;
        context.fill();
        context.restore();

        context.save();
        setCanvasFont(context, 700, Math.max(8, 10 * zoom));
        context.fillStyle = toneStyle.text;
        context.textAlign = side === "right" ? "right" : "left";
        context.textBaseline = "middle";
        const textX = side === "right"
            ? bounds.right - Math.max(20, 24 * zoom)
            : bounds.left + Math.max(20, 24 * zoom);
        context.fillText(port?.label || port?.id || "Port", textX, anchorY);
        context.restore();
    }

    function renderCanvasAdvancedNode(context, state, node, hostBounds, accent, detailMode, meta) {
        const isSelected = state.selectedIds.has(node.id);
        const paletteStyle = resolveCanvasNodePaletteStyle(node, accent, isSelected);
        const inputPorts = Array.isArray(node.inputPorts) ? node.inputPorts : [];
        const outputPorts = Array.isArray(node.outputPorts) ? node.outputPorts : [];
        const portCount = Math.max(inputPorts.length, outputPorts.length);
        const layout = getCanvasAdvancedNodePortLayout(state, node, hostBounds, detailMode);

        drawRoundedPanel(
            context,
            hostBounds,
            Math.max(18, 22 * state.ui.zoom),
            paletteStyle.surfaceFill,
            paletteStyle.surfaceStroke,
            isSelected ? Math.max(1.6, 2.4 * state.ui.zoom) : 1,
            paletteStyle.surfaceShadow);
        drawCanvasDecisionCues(context, state, node, hostBounds, paletteStyle);

        context.save();
        context.fillStyle = "rgba(59, 130, 246, 0.18)";
        context.fillRect(hostBounds.left + 8, hostBounds.top + 8, Math.max(6, 8 * state.ui.zoom), Math.max(30, hostBounds.height - 16));
        context.restore();

        let cursorY = hostBounds.top + layout.padding;
        context.save();
        setCanvasFont(context, 700, Math.max(8, 10.5 * state.ui.zoom));
        context.fillStyle = paletteStyle.labelText;
        context.fillText(node.kind || node.family || "item", layout.contentLeft + Math.max(20, 26 * state.ui.zoom), cursorY + Math.max(7, 10 * state.ui.zoom));
        drawCanvasBadgePill(
            context,
            buildRect(layout.contentLeft, cursorY, Math.max(18, 22 * state.ui.zoom), Math.max(18, 22 * state.ui.zoom)),
            (node.icon || node.kind || "n").slice(0, 1).toUpperCase(),
            paletteStyle.iconFill,
            paletteStyle.iconStroke,
            paletteStyle.iconText,
            Math.max(8, 9 * state.ui.zoom));
        context.restore();

        context.save();
        setCanvasFont(context, 700, Math.max(10, 16 * state.ui.zoom));
        drawCanvasTextLines(
            context,
            layout.titleLines,
            layout.contentLeft,
            layout.titleTop,
            layout.titleLineHeight,
            paletteStyle.titleText);
        context.restore();

        if (layout.supportingLines.length > 0) {
            context.save();
            setCanvasFont(context, 500, Math.max(8, 11.5 * state.ui.zoom));
            context.fillStyle = paletteStyle.secondaryText;
            drawCanvasTextLines(
                context,
                layout.supportingLines,
                layout.contentLeft,
                layout.supportingTop,
                layout.supportingLineHeight,
                paletteStyle.secondaryText);
            context.restore();
        }

        context.save();
        setCanvasFont(context, 700, Math.max(8, 9 * state.ui.zoom));
        context.fillStyle = paletteStyle.labelText;
        if (inputPorts.length > 0) {
            context.textAlign = "left";
            context.fillText("INPUTS", layout.inputColumnLeft, layout.headerBaselineY);
        }

        if (outputPorts.length > 0) {
            context.textAlign = "right";
            context.fillText("OUTPUTS", layout.outputColumnLeft + layout.columnWidth, layout.headerBaselineY);
        }
        context.restore();

        meta.portCount = portCount;
        for (const entry of layout.inputEntries) {
            drawCanvasPortPill(
                context,
                entry.bounds,
                entry.port,
                "left",
                paletteStyle,
                state.ui.zoom);
            registerCanvasPortHotZone(state, node, entry.bounds, entry.port, "left", "input");
        }

        for (const entry of layout.outputEntries) {
            drawCanvasPortPill(
                context,
                entry.bounds,
                entry.port,
                "right",
                paletteStyle,
                state.ui.zoom);
            registerCanvasPortHotZone(state, node, entry.bounds, entry.port, "right", "output");
        }

        const footerPillBounds = buildRect(layout.contentLeft, layout.footerTop, Math.max(74, 88 * state.ui.zoom), layout.footerHeight);
        drawCanvasBadgePill(
            context,
            footerPillBounds,
            node.isRequired ? "required" : "optional",
            paletteStyle.subtleFill,
            paletteStyle.subtleStroke,
            paletteStyle.subtleText,
            Math.max(8, 9.5 * state.ui.zoom));
        drawCanvasBranchLabelPill(
            context,
            state,
            node,
            footerPillBounds.right + Math.max(6, 8 * state.ui.zoom),
            layout.footerTop,
            hostBounds.right - layout.padding - footerPillBounds.right - Math.max(6, 8 * state.ui.zoom),
            layout.footerHeight,
            paletteStyle);

        drawCanvasCollapseControl(context, state, node, paletteStyle);
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
        drawCanvasDecisionCues(context, state, node, hostBounds, paletteStyle);
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
        drawCanvasBranchLabelPill(
            context,
            state,
            node,
            footerPillBounds.right + Math.max(6, 8 * state.ui.zoom),
            footerTop,
            hostBounds.right - padding - footerPillBounds.right - Math.max(6, 8 * state.ui.zoom),
            footerHeight,
            paletteStyle);

        drawCanvasCollapseControl(context, state, node, paletteStyle);
    }

    Object.assign(shared, { getCanvasRuntimePrimitives, createFallbackHitRegistry, createCanvasHitRegistry, createCanvasSurfaceHost, destroyCanvasSurfaceHost, hexToRgba, resolveNodeAccentColor, resolveAnchorRect, buildRect, boundsToHitRect, projectSceneBounds, getNodeSceneBounds, clearSceneHotZones, registerSceneHotZone, getSceneHitAtPoint, getSceneHitAtEvent, resolveHitNode, clearScenePopoverHover, syncSceneHoverState, resolveCanvasNodeDetailMode, setCanvasFont, drawCanvasTextLines, drawRoundedPanel, requestSceneImage, buildCanvasSnapshotBounds, reconcileRetainedLayer, drawCanvasFrame, renderGroupFrames, drawCanvasLink, renderLinks, renderCanvasLinkLabels, drawCanvasBadgePill, drawCanvasProgressBadge, drawCanvasAnnotationBadges, drawNodeMediaPreview, renderCanvasMicroNode, renderCanvasInlineTextNode, getCanvasAdvancedNodePortLayout, renderCanvasDecisionNode, renderCanvasStandardNode, renderCanvasAdvancedNode });
})();
