(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 02-layout-and-legacy-render.js.'); }
    const lateRuntime = new Proxy({}, {
        get(_target, property) {
            return shared[property];
        }
    });
    const workbenchInternals = new Proxy({}, {
        get(_target, property) {
            return shared.workbenchInternals?.[property];
        }
    });
    const { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition } = shared;
    function renderState(state) {
        const renderStateFn = lateRuntime.render;
        if (typeof renderStateFn === "function") {
            renderStateFn(state);
        }
    }

    function publishStateNow(state) {
        const publishStateFn = lateRuntime.publishState;
        if (typeof publishStateFn === "function") {
            publishStateFn(state);
        }
    }

    function renderConnectorAnchors(state, visibleNodes) {
        const renderConnectorAnchorOverlayFn =
            workbenchInternals.overlayRenderer?.renderConnectorAnchorOverlay ||
            lateRuntime.renderConnectorAnchorOverlay ||
            shared.renderConnectorAnchorOverlay ||
            shared.legacyRenderConnectorAnchorOverlay;
        if (typeof renderConnectorAnchorOverlayFn === "function") {
            renderConnectorAnchorOverlayFn(state, visibleNodes);
        }
    }

    function measureRenderedNodeSizes(state) {
        const measureFn = lateRuntime.measureRenderedNodeSizes;
        return typeof measureFn === "function"
            ? !!measureFn(state)
            : legacyMeasureRenderedNodeSizes(state);
    }

    function showPopoverLayer(state, anchorElement, annotation) {
        const showPopoverFn = lateRuntime.showPopover;
        if (typeof showPopoverFn === "function") {
            showPopoverFn(state, anchorElement, annotation);
            return;
        }

        legacyShowPopover(state, anchorElement, annotation);
    }

    function openNodeMetadata(state, node, section, anchorElement) {
        const openNodeMetadataFn = lateRuntime.openNodeMetadataMenu;
        if (typeof openNodeMetadataFn === "function") {
            openNodeMetadataFn(state, node, section, anchorElement);
        }
    }

    function buildDiagnosticsSnapshot(state, bounds) {
        const buildDiagnosticsSnapshotFn = lateRuntime.buildDiagnosticsSnapshot;
        return typeof buildDiagnosticsSnapshotFn === "function"
            ? buildDiagnosticsSnapshotFn(state, bounds)
            : legacyBuildDiagnosticsSnapshot(state, bounds);
    }

    function getNodePortCollection(node, direction) {
        if (direction === "input") {
            return Array.isArray(node?.inputPorts) ? node.inputPorts : [];
        }

        if (direction === "output") {
            return Array.isArray(node?.outputPorts) ? node.outputPorts : [];
        }

        return [];
    }

    function resolveNodePortSide(port, fallbackSide) {
        const side = (port?.side || fallbackSide || "").toString().trim().toLowerCase();
        if (side === "left" || side === "right" || side === "top" || side === "bottom") {
            return side;
        }

        return fallbackSide || "right";
    }

    function resolvePortAnchorY(position, size, index, totalCount) {
        const portTop = position.y - (size.height / 2) + Math.min(86, size.height * 0.34);
        const portBottom = position.y + (size.height / 2) - Math.min(24, size.height * 0.12);
        if (totalCount <= 1) {
            return position.y;
        }

        return portTop + ((portBottom - portTop) * index / Math.max(1, totalCount - 1));
    }

    function resolvePortAnchorHostY(hostBounds, index, totalCount) {
        const portTop = hostBounds.top + Math.min(86, hostBounds.height * 0.34);
        const portBottom = hostBounds.bottom - Math.min(24, hostBounds.height * 0.12);
        if (totalCount <= 1) {
            return hostBounds.top + (hostBounds.height / 2);
        }

        return portTop + ((portBottom - portTop) * index / Math.max(1, totalCount - 1));
    }

    function resolveAnchorDirection(side, fallbackDirection) {
        if (fallbackDirection === "input" || fallbackDirection === "output") {
            return fallbackDirection;
        }

        return side === "left" || side === "top"
            ? "input"
            : "output";
    }

    function resolveStandardAnchorPortId(side) {
        switch ((side || "").toLowerCase()) {
            case "left":
                return "anchor:left";
            case "top":
                return "anchor:top";
            case "bottom":
                return "anchor:bottom";
            default:
                return "anchor:right";
        }
    }

    function normalizeNodeAnchorSide(side) {
        switch ((side || "").toLowerCase()) {
            case "left":
                return "left";
            case "top":
                return "top";
            case "bottom":
                return "bottom";
            default:
                return "right";
        }
    }

    function resolveAnchorDetailMode(options) {
        if (typeof options === "string") {
            return options;
        }

        return typeof options?.detailMode === "string"
            ? options.detailMode
            : "";
    }

    function buildNodeSideAnchorPoint(state, node, side, horizontalInset, verticalInset) {
        const position = getNodePosition(state, node);
        const size = getNodeSize(state, node);
        const resolvedSide = normalizeNodeAnchorSide(side);
        const resolvedHorizontalInset = Math.max(0, horizontalInset || 0);
        const resolvedVerticalInset = Math.max(0, verticalInset || 0);
        switch (resolvedSide) {
            case "left":
                return {
                    x: position.x - (size.width / 2) + resolvedHorizontalInset,
                    y: position.y,
                    side: resolvedSide
                };
            case "top":
                return {
                    x: position.x,
                    y: position.y - (size.height / 2) + resolvedVerticalInset,
                    side: resolvedSide
                };
            case "bottom":
                return {
                    x: position.x,
                    y: position.y + (size.height / 2) - resolvedVerticalInset,
                    side: resolvedSide
                };
            default:
                return {
                    x: position.x + (size.width / 2) - resolvedHorizontalInset,
                    y: position.y,
                    side: resolvedSide
                };
        }
    }

    function resolveLinkAnchorSides(state, source, target, detailMode) {
        const sourcePosition = getNodePosition(state, source);
        const targetPosition = getNodePosition(state, target);
        const deltaX = targetPosition.x - sourcePosition.x;
        const deltaY = targetPosition.y - sourcePosition.y;
        if ((detailMode || "").toLowerCase() === "micro" && Math.abs(deltaY) > Math.abs(deltaX)) {
            const sourceSide = deltaY >= 0 ? "bottom" : "top";
            return {
                sourceSide,
                targetSide: sourceSide === "bottom" ? "top" : "bottom"
            };
        }

        const sourceSide = deltaX >= 0 ? "right" : "left";
        return {
            sourceSide,
            targetSide: sourceSide === "right" ? "left" : "right"
        };
    }

    function resolveLinkControlVector(side) {
        switch (normalizeNodeAnchorSide(side)) {
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

    function buildLinkControlPoint(anchor, side, offset) {
        const vector = resolveLinkControlVector(side);
        return {
            x: anchor.x + (offset * vector.x),
            y: anchor.y + (offset * vector.y)
        };
    }

    function buildPortAnchorPoint(state, node, port, index, totalCount, fallbackSide, fallbackDirection, detailMode) {
        const position = getNodePosition(state, node);
        const size = getNodeSize(state, node);
        const side = resolveNodePortSide(port, fallbackSide);
        const direction = resolveAnchorDirection(side, fallbackDirection);
        const inputPorts = getNodePortCollection(node, "input");
        const outputPorts = getNodePortCollection(node, "output");
        if ((inputPorts.length > 0 || outputPorts.length > 0) &&
            (side === "left" || side === "right")) {
            const zoom = Math.max(state?.ui?.zoom || 1, 0.01);
            const hostCenter = worldToHostPoint(state, position);
            const hostWidth = size.width * zoom;
            const hostHeight = size.height * zoom;
            const hostLeft = hostCenter.x - (hostWidth / 2);
            const hostRight = hostCenter.x + (hostWidth / 2);
            const hostTop = hostCenter.y - (hostHeight / 2);
            const hostBottom = hostCenter.y + (hostHeight / 2);
            const hostBounds = {
                left: hostLeft,
                top: hostTop,
                right: hostRight,
                bottom: hostBottom,
                width: hostWidth,
                height: hostHeight
            };
            const advancedLayout = lateRuntime.getCanvasAdvancedNodePortLayout?.(state, node, hostBounds, detailMode);
            let anchorHostPoint = null;
            if (advancedLayout) {
                const entries = side === "right"
                    ? advancedLayout.outputEntries || []
                    : advancedLayout.inputEntries || [];
                const matchingEntry = entries.find(entry => entry?.port?.id === (port?.id || "")) || entries[index] || null;
                if (matchingEntry?.bounds) {
                    anchorHostPoint = {
                        x: side === "right"
                            ? matchingEntry.bounds.right - Math.max(10, 12 * zoom)
                            : matchingEntry.bounds.left + Math.max(10, 12 * zoom),
                        y: matchingEntry.bounds.top + (matchingEntry.bounds.height / 2)
                    };
                }
            }

            if (!anchorHostPoint) {
                const padding = Math.max(12, 18 * zoom);
                const contentWidth = Math.max(48, hostWidth - (padding * 2));
                const columnGap = Math.max(12, 16 * zoom);
                const hasBothSides = inputPorts.length > 0 && outputPorts.length > 0;
                const columnWidth = hasBothSides
                    ? Math.max(72, (contentWidth - columnGap) / 2)
                    : contentWidth;
                const inputColumnLeft = hostLeft + padding;
                const outputColumnLeft = hasBothSides
                    ? hostRight - padding - columnWidth
                    : inputColumnLeft;
                const anchorInset = Math.max(10, 12 * zoom);
                const rowCount = Math.max(inputPorts.length, outputPorts.length, totalCount || 0, 1);
                anchorHostPoint = {
                    x: side === "right"
                        ? outputColumnLeft + columnWidth - anchorInset
                        : inputColumnLeft + anchorInset,
                    y: resolvePortAnchorHostY(hostBounds, index, rowCount)
                };
            }

            const anchorWorldPoint = hostToWorldPoint(state, anchorHostPoint);
            return {
                x: anchorWorldPoint.x,
                y: anchorWorldPoint.y,
                side,
                portId: port?.id || "",
                label: port?.label || "",
                direction,
                tone: port?.tone || "",
                accentColor: port?.accentColor || "",
                categoryKey: port?.categoryKey || ""
            };
        }

        const horizontalInset = Math.min(28, size.width * 0.11);
        const verticalInset = Math.min(22, size.height * 0.18);
        return {
            x: side === "right"
                ? position.x + (size.width / 2) - horizontalInset
                : side === "left"
                    ? position.x - (size.width / 2) + horizontalInset
                    : position.x,
            y: side === "top"
                ? position.y - (size.height / 2) + verticalInset
                : side === "bottom"
                    ? position.y + (size.height / 2) - verticalInset
                    : resolvePortAnchorY(position, size, index, totalCount),
            side,
            portId: port?.id || "",
            label: port?.label || "",
            direction,
            tone: port?.tone || "",
            accentColor: port?.accentColor || "",
            categoryKey: port?.categoryKey || ""
        };
    }

    function getLinkAnchorPoint(state, node, side, portId, direction, options) {
        const detailMode = resolveAnchorDetailMode(options);
        const resolvedSide = normalizeNodeAnchorSide(side);
        if (detailMode.toLowerCase() === "micro") {
            return buildNodeSideAnchorPoint(state, node, resolvedSide, 0, 0);
        }

        if ((node?.family || "").toLowerCase() === "workflow-decision" ||
            (node?.paletteKey || "").toLowerCase() === "workflow-decision") {
            return buildNodeSideAnchorPoint(state, node, resolvedSide, 0, 0);
        }

        const ports = getNodePortCollection(node, direction);
        if (portId && ports.length > 0) {
            const portIndex = ports.findIndex(port => port?.id === portId);
            if (portIndex >= 0) {
                return buildPortAnchorPoint(state, node, ports[portIndex], portIndex, ports.length, resolvedSide, direction, detailMode);
            }
        }

        const size = getNodeSize(state, node);
        const horizontalInset = Math.min(28, size.width * 0.11);
        const verticalInset = Math.min(22, size.height * 0.18);
        return buildNodeSideAnchorPoint(state, node, resolvedSide, horizontalInset, verticalInset);
    }

    function getCollapseAnchorTargets(state, node) {
        if (!state?.surface || !node?.id) {
            return [];
        }

        const surfaceNodes = Array.isArray(state.surface.nodes) ? state.surface.nodes : [];
        const directChildren = surfaceNodes.filter(candidate => candidate?.parentId === node.id && candidate.id !== node.id);
        if (directChildren.length > 0) {
            return directChildren;
        }

        const nodeLookup = state.lookups?.byId;
        const surfaceLinks = Array.isArray(state.surface.links) ? state.surface.links : [];
        const seen = new Set();
        const fallbackTargets = [];
        for (const link of surfaceLinks) {
            if (!link || link.sourceId !== node.id || !link.targetId || seen.has(link.targetId)) {
                continue;
            }

            const target = nodeLookup?.get?.(link.targetId) || null;
            if (!target) {
                continue;
            }

            seen.add(link.targetId);
            fallbackTargets.push(target);
        }

        return fallbackTargets;
    }

    function resolveCollapseAnchorInfo(state, node) {
        const position = getNodePosition(state, node);
        const size = getNodeSize(state, node);
        const targets = getCollapseAnchorTargets(state, node);
        let side = "right";
        if (targets.length > 0) {
            let rightCount = 0;
            let leftCount = 0;
            let rightDistance = 0;
            let leftDistance = 0;

            for (const target of targets) {
                const targetPosition = getNodePosition(state, target);
                const deltaX = targetPosition.x - position.x;
                if (deltaX < 0) {
                    leftCount += 1;
                    leftDistance += Math.abs(deltaX);
                    continue;
                }

                rightCount += 1;
                rightDistance += Math.abs(deltaX);
            }

            if (leftCount > 0 && rightCount === 0) {
                side = "left";
            }
            else if (rightCount > 0 && leftCount === 0) {
                side = "right";
            }
            else if (leftCount !== rightCount) {
                side = rightCount > leftCount ? "right" : "left";
            }
            else if (leftDistance !== rightDistance) {
                side = rightDistance >= leftDistance ? "right" : "left";
            }
        }

        return {
            side,
            world: {
                x: side === "right"
                    ? position.x + (size.width / 2)
                    : position.x - (size.width / 2),
                y: position.y
            }
        };
    }

    function getLinkRetainedKey(link, index) {
        if (link?.sourceId || link?.targetId || link?.kind || link?.sourcePortId || link?.targetPortId || link?.label || link?.tone) {
            return `${link?.sourceId || ""}|${link?.sourcePortId || ""}|${link?.targetId || ""}|${link?.targetPortId || ""}|${link?.kind || ""}|${link?.label || ""}|${link?.tone || ""}|${link?.isUserAuthored ? "1" : "0"}`;
        }

        return `link:${index}`;
    }

    function getLinkPathData(state, source, target, link, options) {
        const detailMode = resolveAnchorDetailMode(options);
        const anchorSides = resolveLinkAnchorSides(state, source, target, detailMode);
        const sourceAnchor = getLinkAnchorPoint(state, source, anchorSides.sourceSide, link?.sourcePortId, "output", detailMode);
        const targetAnchor = getLinkAnchorPoint(state, target, anchorSides.targetSide, link?.targetPortId, "input", detailMode);
        const sourceAnchorSide = sourceAnchor.side || anchorSides.sourceSide;
        const targetAnchorSide = targetAnchor.side || anchorSides.targetSide;
        const controlOffset = Math.max(
            92,
            Math.max(Math.abs(targetAnchor.x - sourceAnchor.x), Math.abs(targetAnchor.y - sourceAnchor.y)) * 0.38);
        const sourceControl = buildLinkControlPoint(sourceAnchor, sourceAnchorSide, controlOffset);
        const targetControl = buildLinkControlPoint(targetAnchor, targetAnchorSide, controlOffset);
        return [
            `M ${sourceAnchor.x} ${sourceAnchor.y}`,
            `C ${sourceControl.x} ${sourceControl.y}`,
            `${targetControl.x} ${targetControl.y}`,
            `${targetAnchor.x} ${targetAnchor.y}`
        ].join(" ");
    }

    function updateLinkElement(path, link, pathData) {
        path.setAttribute("d", pathData);
        path.setAttribute("fill", "none");
        path.setAttribute("stroke", link.isUserAuthored ? "rgba(14, 165, 233, 0.78)" : "rgba(100, 116, 139, 0.4)");
        path.setAttribute("stroke-width", link.isUserAuthored ? "3" : "2");
        path.setAttribute("stroke-linecap", "round");
        path.setAttribute("stroke-linejoin", "round");
        path.setAttribute("class", link.isUserAuthored ? "cw-link-path is-flow" : "cw-link-path");
        if (shouldRenderArrow(link)) {
            path.setAttribute("marker-end", link.isUserAuthored ? "url(#cw-link-arrow-user)" : "url(#cw-link-arrow-system)");
        }
        else {
            path.removeAttribute("marker-end");
        }
    }

    function shouldRenderArrow(link) {
        if (!link) {
            return false;
        }

        const kind = (link.kind || "").toLowerCase();
        return !!link.isUserAuthored ||
            kind === "dependson" ||
            kind === "derivedfrom" ||
            kind === "uses";
    }

    function getExpandedFrameNodeIds(state, frame) {
        const expanded = new Set();
        const queue = [...(frame?.anchorNodeIds || [])];

        while (queue.length > 0) {
            const nodeId = queue.shift();
            if (!nodeId || expanded.has(nodeId) || !state.lookups.byId.has(nodeId)) {
                continue;
            }

            expanded.add(nodeId);
            const children = state.lookups.children.get(nodeId) || [];
            for (const childId of children) {
                queue.push(childId);
            }
        }

        return [...expanded];
    }

    function getFrameRetainedKey(frame, index) {
        return frame?.id || `frame:${index}`;
    }

    function createFrameElement(state, frameId) {
        const frameElement = createElement(state.document, "div", "cw-group-frame");
        frameElement.dataset.frameId = frameId;

        const label = createElement(state.document, "div", "cw-group-frame__label");
        label.dataset.frameId = frameId;

        const labelText = createElement(state.document, "span", "", "");
        const count = createElement(state.document, "span", "cw-group-frame__count", "0");
        label.appendChild(labelText);
        label.appendChild(count);
        frameElement.appendChild(label);

        for (const edge of ["top", "right", "bottom", "left"]) {
            const handle = createElement(state.document, "div", `cw-group-frame__handle is-${edge}`);
            handle.dataset.frameId = frameId;
            handle.setAttribute("aria-hidden", "true");
            frameElement.appendChild(handle);
        }

        return {
            element: frameElement,
            label,
            labelText,
            count
        };
    }

    function updateFrameElement(entry, frame, frameId, memberNodes, bounds) {
        entry.element.className = `cw-group-frame tone-${frame.tone || "accent"}`;
        entry.element.dataset.frameId = frameId;
        entry.element.style.left = `${round(bounds.minX)}px`;
        entry.element.style.top = `${round(bounds.minY)}px`;
        entry.element.style.width = `${round(bounds.width)}px`;
        entry.element.style.height = `${round(bounds.height)}px`;
        entry.label.dataset.frameId = frameId;
        entry.labelText.textContent = frame.label || "Group border";
        entry.count.textContent = `${memberNodes.length}`;
    }

    function getFrameBounds(state, memberNodes) {
        if (!Array.isArray(memberNodes) || memberNodes.length === 0) {
            return null;
        }

        let minX = Number.POSITIVE_INFINITY;
        let maxX = Number.NEGATIVE_INFINITY;
        let minY = Number.POSITIVE_INFINITY;
        let maxY = Number.NEGATIVE_INFINITY;

        for (const node of memberNodes) {
            const position = getNodePosition(state, node);
            const size = getNodeSize(state, node);
            minX = Math.min(minX, position.x - (size.width / 2));
            maxX = Math.max(maxX, position.x + (size.width / 2));
            minY = Math.min(minY, position.y - (size.height / 2));
            maxY = Math.max(maxY, position.y + (size.height / 2));
        }

        const paddingX = 38;
        const paddingY = 42;
        return {
            minX: minX - paddingX,
            minY: minY - paddingY,
            width: (maxX - minX) + (paddingX * 2),
            height: (maxY - minY) + (paddingY * 2)
        };
    }

    function legacyRenderGroupFrames(state, visibleNodes) {
        if (!state.frameLayer) {
            return;
        }

        const metrics = state.metrics;
        const retainedFrames = state.retainedFrameElements;
        const activeKeys = new Set();
        state.renderedFrames = new Map();
        const visibleLookup = new Map(visibleNodes.map(node => [node.id, node]));
        let renderedFrameCount = 0;

        for (const [index, frame] of (state.ui.groupFrames || []).entries()) {
            const memberNodes = getExpandedFrameNodeIds(state, frame)
                .map(nodeId => visibleLookup.get(nodeId))
                .filter(Boolean);
            if (!memberNodes.length) {
                continue;
            }

            const bounds = getFrameBounds(state, memberNodes);
            if (!bounds) {
                continue;
            }

            const retainedKey = getFrameRetainedKey(frame, index);
            let entry = retainedFrames.get(retainedKey) || null;
            if (!entry) {
                entry = createFrameElement(state, retainedKey);
                retainedFrames.set(retainedKey, entry);
                incrementMetric(metrics, "frameLayerRebuildCount");
            }

            activeKeys.add(retainedKey);
            updateFrameElement(entry, frame, retainedKey, memberNodes, bounds);
            state.frameLayer.appendChild(entry.element);
            state.renderedFrames.set(retainedKey, {
                frame,
                nodeIds: memberNodes.map(node => node.id)
            });
            renderedFrameCount += 1;
        }

        for (const [key, entry] of retainedFrames) {
            if (activeKeys.has(key)) {
                continue;
            }

            entry.element.remove();
            retainedFrames.delete(key);
            incrementMetric(metrics, "frameLayerRebuildCount");
        }

        if (metrics) {
            metrics.lastRenderedFrameCount = renderedFrameCount;
        }
    }

    function resolveChipToneClass(tone) {
        switch ((tone || "").toLowerCase()) {
            case "success":
                return "cw-node__chip tone-success";
            case "warn":
            case "warning":
                return "cw-node__chip tone-warning";
            case "danger":
                return "cw-node__chip tone-danger";
            case "accent":
            case "info":
                return "cw-node__chip tone-accent";
            default:
                return "cw-node__chip";
        }
    }

    function createProgressMarker(state, node) {
        const percent = normalizeProgressPercent(node?.progressPercent);
        const normalizedMode = (node?.progressMode || "na").toLowerCase();
        const isComplete = normalizedMode === "complete" || percent >= 100;
        const mode = isComplete ? "complete" : (normalizedMode === "progress" ? "progress" : "na");
        const marker = createElement(state.document, "span", `cw-node__progress is-${mode}`);
        marker.style.setProperty("--cw-progress-angle", `${round((percent / 100) * 360)}deg`);
        marker.title = isComplete
            ? "Done"
            : mode === "progress"
                ? `${percent}% complete`
                : "Not applicable";

        const center = createElement(state.document, "span", "cw-node__progress-center", isComplete ? "✓" : (mode === "na" ? "-" : ""));
        marker.appendChild(center);
        return marker;
    }

    function resolveProgressDisplay(progressMode, progressPercent) {
        const percent = normalizeProgressPercent(progressPercent);
        const normalizedMode = (progressMode || "na").toLowerCase();
        if (normalizedMode === "complete" || percent >= 100) {
            return { mode: "complete", angle: 360, centerText: "\u2713", title: "Done" };
        }

        if (normalizedMode === "started") {
            return { mode: "started", angle: 42, centerText: "\u25B6", title: "Started" };
        }

        if (normalizedMode === "progress") {
            return { mode: "progress", angle: round((percent / 100) * 360), centerText: "", title: `${percent}% complete` };
        }

        return { mode: "na", angle: 360, centerText: "-", title: "Not applicable" };
    }

    function createProgressBadge(document, progressMode, progressPercent, extraClassName, centerTextOverride, titleOverride) {
        const display = resolveProgressDisplay(progressMode, progressPercent);
        const marker = createElement(document, "span", `cw-node__progress is-${display.mode}${extraClassName ? ` ${extraClassName}` : ""}`);
        marker.style.setProperty("--cw-progress-angle", `${display.angle}deg`);
        const centerText = typeof centerTextOverride === "string"
            ? centerTextOverride
            : display.centerText;
        marker.title = titleOverride || display.title;
        if (centerText.length === 0) {
            marker.classList.add("is-empty-center");
        }
        else if (centerText.length > 2) {
            marker.classList.add("has-long-text");
        }
        marker.appendChild(createElement(document, "span", "cw-node__progress-center", centerText));
        return marker;
    }

    function resolveProgressPresetBadgeOptions(iconKey) {
        const token = iconKey.substring("progress-".length);
        const numericPercent = Number(token);
        if (token === "na") {
            return {
                progressMode: "na",
                progressPercent: 0,
                centerText: "N/A",
                title: "Not applicable"
            };
        }

        if (token === "started") {
            return {
                progressMode: "started",
                progressPercent: 0,
                centerText: "",
                title: "Started"
            };
        }

        const progressPercent = Number.isFinite(numericPercent)
            ? clamp(Math.round(numericPercent), 0, 100)
            : 0;
        return {
            progressMode: progressPercent >= 100 ? "complete" : "progress",
            progressPercent,
            centerText: `${progressPercent}%`,
            title: `${progressPercent}% complete`
        };
    }

    function resolveMarkerGlyph(markerIcon) {
        switch ((markerIcon || "").toLowerCase()) {
            case "question":
                return "?";
            case "alert":
                return "!";
            case "thumbs-up":
                return "\uD83D\uDC4D";
            case "thumbs-down":
                return "\uD83D\uDC4E";
            case "pause":
                return "\u23F8";
            case "stop":
                return "\u25A0";
            case "money":
                return "$";
            case "car":
                return "\uD83D\uDE97";
            case "idea":
                return "\u2726";
            case "risk":
                return "\u26A0";
            default:
                return "";
        }
    }

    function resolveNodeMarkers(node) {
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

    function createMarkerBadgeElement(state, marker, extraClass) {
        const glyph = resolveMarkerGlyph(marker?.icon);
        if (!glyph) {
            return null;
        }

        const tone = (marker?.tone || "accent").toLowerCase();
        const badge = createElement(state.document, "span", `cw-node__badge cw-node__marker tone-${tone}${extraClass ? ` ${extraClass}` : ""}`, glyph);
        badge.title = marker?.label || "Marker";
        return badge;
    }

    function createMarkerBadge(state, node) {
        const markers = resolveNodeMarkers(node);
        return markers.length === 0
            ? null
            : createMarkerBadgeElement(state, markers[0], "");
    }

    function createMarkerBadges(state, node, maxVisible) {
        const markers = resolveNodeMarkers(node);
        if (markers.length === 0) {
            return [];
        }

        const limit = Math.max(1, maxVisible || 3);
        const badges = markers
            .slice(0, limit)
            .map(marker => createMarkerBadgeElement(state, marker, ""))
            .filter(Boolean);
        const overflowCount = markers.length - limit;
        if (overflowCount > 0) {
            const overflowBadge = createElement(state.document, "span", "cw-node__badge cw-node__marker tone-primary cw-node__marker-overflow", `+${overflowCount}`);
            overflowBadge.title = `${overflowCount} more markers`;
            badges.push(overflowBadge);
        }

        return badges;
    }

    function createPriorityBadge(state, node) {
        const priority = clamp(Math.round(node?.priority || 0), 0, 6);
        if (priority <= 0) {
            return null;
        }

        const badge = createElement(state.document, "span", `cw-node__badge cw-node__priority is-level-${priority}`, `${priority}`);
        badge.title = `Priority ${priority}`;
        return badge;
    }

    function appendNodeIndicators(state, node, container) {
        const progressBadge = createProgressBadge(state.document, node?.progressMode, node?.progressPercent, "");
        const openProgressMetadata = event => {
            event.preventDefault();
            event.stopPropagation();
            state.recentDoubleActivationAt = Date.now();
            openNodeMetadata(state, node, "progress", progressBadge);
        };
        progressBadge.addEventListener("pointerdown", event => {
            if (event.button !== 0 || event.detail < 2) {
                return;
            }

            openProgressMetadata(event);
        });
        progressBadge.addEventListener("dblclick", openProgressMetadata);
        container.appendChild(progressBadge);
        for (const markerBadge of createMarkerBadges(state, node, 3)) {
            container.appendChild(markerBadge);
        }

        const priorityBadge = createPriorityBadge(state, node);
        if (priorityBadge) {
            container.appendChild(priorityBadge);
        }
    }

    function renderInlineTextNode(state, node, nodeElement) {
        nodeElement.classList.add("is-inline-text");
        const surface = createElement(state.document, "div", "cw-node__surface");
        const noteText = node.inlineText || node.title || node.leadText || "Write note";
        surface.appendChild(createElement(state.document, "p", "cw-note-node__text", noteText));
        renderNodeAnnotations(state, node, surface);

        if (node.statusPill || node.progressMode || resolveNodeMarkers(node).length > 0 || node.priority > 0) {
            const meta = createElement(state.document, "div", "cw-note-node__meta");
            appendNodeIndicators(state, node, meta);
            if (node.statusPill) {
                meta.appendChild(createElement(state.document, "span", "cw-node__chip tone-accent", node.statusPill));
            }
            surface.appendChild(meta);
        }

        appendCollapseButton(state, node, surface);
        nodeElement.appendChild(surface);
    }

    function createNodeMedia(state, node) {
        if (!node?.mediaKind || !node?.mediaPreviewUrl) {
            return null;
        }

        const media = createElement(state.document, `div`, `cw-node__media cw-node__media--${node.mediaKind}`);
        if (node.mediaKind === "image") {
            const image = createElement(state.document, "img", "cw-node__media-image");
            image.src = node.mediaPreviewUrl;
            image.alt = node.mediaPreviewAlt || node.title || node.mediaFileName || "Uploaded image";
            image.loading = "lazy";
            image.decoding = "async";
            image.draggable = false;
            media.appendChild(image);
        }
        else if (node.mediaKind === "video") {
            const placeholder = createElement(state.document, "div", "cw-node__media-video");
            placeholder.appendChild(createElement(state.document, "span", "cw-node__media-video-icon", "\u25B6"));
            placeholder.appendChild(createElement(state.document, "span", "cw-node__media-video-label", "Preview"));
            media.appendChild(placeholder);
        }

        media.appendChild(createElement(state.document, "span", "cw-node__media-badge", node.mediaKind === "image" ? "Image" : "Video"));
        return media;
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

        if (button.__cwCopyResetHandle) {
            window.clearTimeout(button.__cwCopyResetHandle);
        }

        button.dataset.copied = "true";
        const icon = button.querySelector(".cw-node__path-action");
        if (icon) {
            icon.textContent = resolveActionGlyph("qa");
        }

        showStatusNotice(state, `${compactPath.label || "Path"} copied`, "success");
        button.__cwCopyResetHandle = window.setTimeout(() => {
            button.dataset.copied = "false";
            if (icon) {
                icon.textContent = resolveActionGlyph("copy");
            }

            button.__cwCopyResetHandle = 0;
        }, 2000);
    }

    function createCompactPathButton(state, node) {
        const compactPath = node?.compactPath;
        if (!compactPath?.fullPath) {
            return null;
        }

        const button = applyFullTextTooltip(
            createElement(state.document, "button", "cw-node__path-button"),
            compactPath.fullPath);
        button.type = "button";
        button.dataset.copied = "false";
        button.setAttribute("aria-label", `${compactPath.label || "Path"}: ${compactPath.fullPath}`);
        button.addEventListener("pointerdown", event => event.stopPropagation());
        button.addEventListener("pointerup", event => event.stopPropagation());
        button.addEventListener("dblclick", event => event.stopPropagation());
        button.addEventListener("click", event => {
            event.preventDefault();
            event.stopPropagation();
            void copyCompactPath(state, button, compactPath);
        });

        const text = createElement(state.document, "span", "cw-node__path-text", compactPath.displayText || compactPath.fullPath);
        const action = createElement(state.document, "span", "cw-node__path-action", resolveActionGlyph("copy"));
        action.setAttribute("aria-hidden", "true");
        button.appendChild(text);
        button.appendChild(action);
        return button;
    }

    function appendCollapseButton(state, node, surface) {
        if (!node?.isCollapsible) {
            return;
        }

        const isCollapsed = state.collapsedIds.has(node.id);
        const collapse = createElement(state.document, "button", "cw-node__collapse", isCollapsed ? "+" : "-");
        collapse.type = "button";
        collapse.dataset.nodeId = node.id;
        collapse.setAttribute("aria-label", isCollapsed ? "Show child nodes" : "Hide child nodes");
        collapse.addEventListener("pointerdown", event => event.stopPropagation());
        collapse.addEventListener("pointerup", event => event.stopPropagation());
        collapse.addEventListener("click", event => {
            event.stopPropagation();
            toggleCollapse(state, node.id);
        });
        surface.appendChild(collapse);
    }

    function renderStandardNode(state, node, nodeElement) {
        const surface = createElement(state.document, "div", "cw-node__surface");
        const header = createElement(state.document, "div", "cw-node__header");
        const eyebrow = createElement(state.document, "div", "cw-node__eyebrow");
        const icon = createElement(state.document, "span", "cw-node__icon", node.icon || node.kind || "node");
        const kicker = createElement(state.document, "span", "cw-node__kicker", node.kind || node.family || "item");
        eyebrow.appendChild(icon);
        eyebrow.appendChild(kicker);
        header.appendChild(eyebrow);

        const rightMeta = createElement(state.document, "div", "cw-chip-row");
        appendNodeIndicators(state, node, rightMeta);
        if (node.durationLabel) {
            rightMeta.appendChild(createElement(state.document, "span", "cw-node__pill", node.durationLabel));
        }

        if (node.statusPill) {
            rightMeta.appendChild(createElement(state.document, "span", "cw-node__pill", node.statusPill));
        }

        header.appendChild(rightMeta);
        surface.appendChild(header);
        const media = createNodeMedia(state, node);
        if (media) {
            surface.appendChild(media);
        }

        const title = applyFullTextTooltip(
            createElement(state.document, "h3", "cw-node__title", node.title || "Untitled"),
            node.title || "Untitled");
        surface.appendChild(title);

        if (node.subtitle) {
            surface.appendChild(applyFullTextTooltip(
                createElement(state.document, "p", "cw-node__subtitle", node.subtitle),
                node.subtitle));
        }

        if (node.compactPath?.promotedText &&
            node.compactPath.promotedText !== node.title &&
            node.compactPath.promotedText !== node.subtitle) {
            surface.appendChild(applyFullTextTooltip(
                createElement(state.document, "p", "cw-node__path-file", node.compactPath.promotedText),
                node.compactPath.promotedText));
        }

        if (node.leadText) {
            surface.appendChild(applyFullTextTooltip(
                createElement(state.document, "p", "cw-node__lead", node.leadText),
                node.leadText));
        }

        const compactPathButton = createCompactPathButton(state, node);
        if (compactPathButton) {
            surface.appendChild(compactPathButton);
        }

        renderNodeAnnotations(state, node, surface);

        if (node.chips.length > 0) {
            const chipRow = createElement(state.document, "div", "cw-node__chips");
            for (const chip of node.chips) {
                chipRow.appendChild(createElement(state.document, "span", resolveChipToneClass(chip.tone), chip.text));
            }

            surface.appendChild(chipRow);
        }

        const footer = createElement(state.document, "div", "cw-node__footer");
        const footerLeft = createElement(state.document, "div", "cw-chip-row");
        footerLeft.appendChild(createElement(state.document, "span", "cw-node__chip", node.isRequired ? "required" : "optional"));
        if (node.branchLabel) {
            footerLeft.appendChild(createElement(state.document, "span", "cw-node__chip", node.branchLabel));
        }

        footer.appendChild(footerLeft);
        const footerRight = createElement(state.document, "div", "cw-chip-row");
        for (const chip of node.footerChips) {
            footerRight.appendChild(createElement(state.document, "span", resolveChipToneClass(chip.tone), chip.text));
        }

        footer.appendChild(footerRight);
        surface.appendChild(footer);

        appendCollapseButton(state, node, surface);
        nodeElement.appendChild(surface);
    }

    function syncNodeCollapseAffordance(state, node, nodeElement) {
        const collapse = nodeElement.querySelector(".cw-node__collapse");
        if (!node?.isCollapsible || !collapse) {
            delete nodeElement.dataset.collapseSide;
            return;
        }

        const anchor = resolveCollapseAnchorInfo(state, node);
        nodeElement.dataset.collapseSide = anchor.side;
        collapse.dataset.side = anchor.side;
        collapse.setAttribute("aria-label", state.collapsedIds.has(node.id) ? "Show child nodes" : "Hide child nodes");
    }

    function createRetainedNodeElement(state, nodeId) {
        const nodeElement = createElement(state.document, "div", "cw-node");
        nodeElement.dataset.nodeId = nodeId;
        nodeElement.addEventListener("pointerenter", () => updateConnectorAnchorHover(state, nodeId));
        nodeElement.addEventListener("pointerleave", () => updateConnectorAnchorHover(state, null));
        return nodeElement;
    }

    function getNodeRetainedContentKey(node, isCollapsed) {
        if (!node) {
            return `collapsed:${isCollapsed ? "1" : "0"}`;
        }

        return JSON.stringify({
            ...node,
            x: null,
            y: null,
            collapsed: !!isCollapsed
        });
    }

    function updateNodeElementChrome(state, node, nodeElement, position) {
        nodeElement.className = "cw-node";
        nodeElement.dataset.nodeId = node.id;
        nodeElement.dataset.family = node.family || "item";
        nodeElement.dataset.palette = node.paletteKey || "neutral";
        nodeElement.style.left = `${position.x}px`;
        nodeElement.style.top = `${position.y}px`;
        nodeElement.style.setProperty("--cw-node-accent", node.accentColor || "#7c3aed");

        if (node.isInlineTextNode) {
            nodeElement.classList.add("is-inline-text");
        }

        if (node.isReadOnly) {
            nodeElement.classList.add("is-readonly");
            nodeElement.dataset.readOnly = "true";
        }
        else {
            delete nodeElement.dataset.readOnly;
        }

        if (node.isPreviewOnly) {
            nodeElement.classList.add("is-preview-only");
            nodeElement.dataset.previewOnly = "true";
        }
        else {
            delete nodeElement.dataset.previewOnly;
        }

        if (state.selectedIds.has(node.id)) {
            nodeElement.classList.add("is-selected");
        }

        if (state.collapsedIds.has(node.id)) {
            nodeElement.classList.add("is-collapsed");
        }

        syncNodeCollapseAffordance(state, node, nodeElement);
    }

    function renderNodeElementContent(state, node, nodeElement) {
        clear(nodeElement);
        if (node.isInlineTextNode) {
            renderInlineTextNode(state, node, nodeElement);
        }
        else {
            renderStandardNode(state, node, nodeElement);
        }

        syncNodeCollapseAffordance(state, node, nodeElement);
    }

    function buildActiveDragContext(state) {
        const interaction = state?.interaction;
        if (!interaction || (interaction.kind !== "drag" && interaction.kind !== "frame-drag")) {
            return null;
        }

        const visibleNodes = getVisibleNodes(state);
        const projectedNodes = getProjectedNodes(state, visibleNodes);
        const overlayNodeIds = new Set(projectedNodes.map(node => node.id));
        const movedNodeIds = new Set(interaction.nodeIds || []);
        const dirtyDebugNodeIds = new Set(interaction.nodeIds || []);
        for (const nodeId of movedNodeIds) {
            overlayNodeIds.add(nodeId);
        }

        const overlayNodes = visibleNodes.filter(node => overlayNodeIds.has(node.id));
        const movedNodes = (interaction.nodeIds || [])
            .map(nodeId => state.lookups.byId.get(nodeId))
            .filter(Boolean);
        const dirtyLinks = [];
        for (const [index, link] of state.surface.links.entries()) {
            const retainedKey = getLinkRetainedKey(link, index);
            if (!state.retainedLinkElements.has(retainedKey)) {
                continue;
            }

            if (!movedNodeIds.has(link.sourceId) && !movedNodeIds.has(link.targetId)) {
                continue;
            }

            dirtyLinks.push({ retainedKey, link });
            dirtyDebugNodeIds.add(link.sourceId);
            dirtyDebugNodeIds.add(link.targetId);
        }

        const dirtyFrames = [];
        for (const [index, frame] of (state.ui.groupFrames || []).entries()) {
            const retainedKey = getFrameRetainedKey(frame, index);
            const renderedFrame = state.renderedFrames?.get(retainedKey);
            if (!renderedFrame?.nodeIds?.length) {
                continue;
            }

            if (!renderedFrame.nodeIds.some(nodeId => movedNodeIds.has(nodeId))) {
                continue;
            }

            dirtyFrames.push({
                retainedKey,
                frame,
                nodeIds: renderedFrame.nodeIds.slice()
            });
        }

        return {
            overlayNodes,
            projectedNodeCount: projectedNodes.length,
            movedNodes,
            dirtyLinks,
            dirtyFrames,
            dirtyDebugNodes: [...dirtyDebugNodeIds]
                .map(nodeId => state.lookups.byId.get(nodeId))
                .filter(Boolean)
        };
    }

    function positionFloatingOverlayWithinHost(state, element, anchorRect) {
        if (!state?.host || !element || !anchorRect) {
            return;
        }

        const hostRect = state.host.getBoundingClientRect();
        const elementRect = element.getBoundingClientRect();
        const margin = 12;
        let left = anchorRect.left - hostRect.left + (anchorRect.width / 2) - (elementRect.width / 2);
        let top = anchorRect.top - hostRect.top - elementRect.height - 10;

        if (top < margin) {
            top = anchorRect.bottom - hostRect.top + 10;
        }

        left = clamp(left, margin, Math.max(margin, hostRect.width - elementRect.width - margin));
        top = clamp(top, margin, Math.max(margin, hostRect.height - elementRect.height - margin));
        element.style.left = `${round(left)}px`;
        element.style.top = `${round(top)}px`;
    }

    function hidePopover(state) {
        if (!state?.popover) {
            return;
        }

        state.popover.style.display = "none";
        state.popoverAnchor = null;
        state.hoveredAnnotationKey = "";
    }

    function legacyShowPopover(state, anchorElement, annotation) {
        if (!state?.host?.isConnected ||
            !state?.popover ||
            !state.popover.isConnected ||
            !state.popoverTitle ||
            !state.popoverBody ||
            !anchorElement ||
            !anchorElement.isConnected ||
            !annotation) {
            hidePopover(state);
            return false;
        }

        if (state.surface?.chrome?.tooltipPopover?.isEnabled === false) {
            hidePopover(state);
            return false;
        }

        const anchorRect = anchorElement.getBoundingClientRect();
        state.popover.dataset.kind = annotation.kind || "info";
        state.popover.dataset.tone = annotation.tone || "accent";
        state.popoverTitle.textContent = annotation.label || annotation.kind || "Signal";
        state.popoverBody.textContent = annotation.description || annotation.label || "Shared workbench signal";
        state.popover.style.display = "grid";
        state.popoverAnchor = anchorElement;
        positionFloatingOverlayWithinHost(state, state.popover, anchorRect);
        return true;
    }

    function invokeAnnotationAction(state, node, annotation) {
        if (!annotation?.actionId) {
            return;
        }

        const point = getNodePosition(state, node);
        state.dotNetRef.invokeMethodAsync("OnContextAction", node.id, annotation.actionId, round(point.x), round(point.y));
    }

    function renderNodeAnnotations(state, node, container) {
        if (!Array.isArray(node?.annotations) || node.annotations.length === 0) {
            return;
        }

        const tooltipPopover = state.surface.chrome.tooltipPopover || {};
        const row = createElement(state.document, "div", "cw-node__annotations");
        for (const annotation of node.annotations) {
            const badge = createElement(state.document, "button", `cw-node__annotation tone-${annotation.tone || "accent"}`);
            badge.type = "button";
            badge.dataset.kind = annotation.kind || "info";
            badge.textContent = annotation.icon
                ? `${annotation.icon} ${annotation.label || annotation.kind || "Signal"}`
                : (annotation.label || annotation.kind || "Signal");
            badge.addEventListener("pointerdown", event => event.stopPropagation());
            if (tooltipPopover.isEnabled !== false) {
                badge.addEventListener("pointerenter", () => showPopoverLayer(state, badge, annotation));
                badge.addEventListener("pointerleave", () => hidePopover(state));
                if (tooltipPopover.focusTriggers !== false) {
                    badge.addEventListener("focus", () => showPopoverLayer(state, badge, annotation));
                    badge.addEventListener("blur", () => hidePopover(state));
                }
            }
            badge.addEventListener("click", event => {
                event.preventDefault();
                event.stopPropagation();
                hidePopover(state);
                invokeAnnotationAction(state, node, annotation);
            });
            row.appendChild(badge);
        }

        container.appendChild(row);
    }

    function updateConnectorAnchorHover(state, nodeId) {
        const nextNodeId = nodeId || null;
        if ((state?.hoveredNodeId || null) === nextNodeId) {
            return;
        }

        state.hoveredNodeId = nextNodeId;
        renderConnectorAnchors(state, getVisibleNodes(state));
    }

    function getConnectorAnchorPoints(state, node, placementMode) {
        const inputPorts = Array.isArray(node?.inputPorts) ? node.inputPorts : [];
        const outputPorts = Array.isArray(node?.outputPorts) ? node.outputPorts : [];
        if (inputPorts.length > 0 || outputPorts.length > 0) {
            return [
                ...inputPorts.map((port, index) => buildPortAnchorPoint(state, node, port, index, inputPorts.length, "left", "input")),
                ...outputPorts.map((port, index) => buildPortAnchorPoint(state, node, port, index, outputPorts.length, "right", "output"))
            ];
        }

        const position = getNodePosition(state, node);
        const size = getNodeSize(state, node);
        const horizontalInset = Math.min(28, size.width * 0.11);
        const verticalInset = Math.min(22, size.height * 0.18);
        const points = [
            {
                side: "left",
                x: position.x - (size.width / 2) + horizontalInset,
                y: position.y,
                portId: resolveStandardAnchorPortId("left"),
                direction: "input",
                label: "Input"
            },
            {
                side: "right",
                x: position.x + (size.width / 2) - horizontalInset,
                y: position.y,
                portId: resolveStandardAnchorPortId("right"),
                direction: "output",
                label: "Output"
            }
        ];

        if ((placementMode || "edges") !== "horizontal") {
            points.push(
                {
                    side: "top",
                    x: position.x,
                    y: position.y - (size.height / 2) + verticalInset,
                    portId: resolveStandardAnchorPortId("top"),
                    direction: "input",
                    label: "Input"
                },
                {
                    side: "bottom",
                    x: position.x,
                    y: position.y + (size.height / 2) - verticalInset,
                    portId: resolveStandardAnchorPortId("bottom"),
                    direction: "output",
                    label: "Output"
                });
        }

        return points;
    }

    function hideStatusNotice(state) {
        if (!state?.statusNotice) {
            return;
        }

        if (state.statusNoticeTimer) {
            window.clearTimeout(state.statusNoticeTimer);
            state.statusNoticeTimer = 0;
        }

        state.statusNotice.style.display = "none";
        state.statusNotice.textContent = "";
        delete state.statusNotice.dataset.tone;
    }

    function showStatusNotice(state, message, tone) {
        if (!state?.statusNotice || !message) {
            return;
        }

        hideStatusNotice(state);
        state.statusNotice.textContent = message;
        state.statusNotice.dataset.tone = tone || "accent";
        state.statusNotice.style.display = "block";
        state.statusNoticeTimer = window.setTimeout(() => hideStatusNotice(state), 1800);
    }

    function renderEmptyStateOverlay(state, visibleNodes) {
        if (!state?.emptyState) {
            return;
        }

        const shouldShow = visibleNodes.length === 0;
        state.emptyState.style.display = shouldShow ? "grid" : "none";
        if (!shouldShow) {
            return;
        }

        state.emptyStateKicker.textContent = state.surface.chrome.emptyStateKicker || "Canvas";
        state.emptyStateTitle.textContent = state.surface.chrome.emptyStateTitle || "No nodes yet";
        state.emptyStateBody.textContent = state.surface.chrome.emptyStateDescription || "Use quick create to start building the scene.";
    }

    function clearSnapGuides(state) {
        state.snapGuides = [];
    }

    function legacyRenderSnapGuides(state) {
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

        const bounds = getSceneBounds(state) || { minX: -200, maxX: 200, minY: -200, maxY: 200 };
        const padding = 180;
        for (const guide of state.snapGuides) {
            const element = createElement(state.document, "div", `cw-snap-guide is-${guide.orientation || "vertical"}`);
            if (guide.orientation === "horizontal") {
                element.style.left = `${round(bounds.minX - padding)}px`;
                element.style.top = `${round(guide.value)}px`;
                element.style.width = `${round((bounds.maxX - bounds.minX) + (padding * 2))}px`;
            }
            else {
                element.style.left = `${round(guide.value)}px`;
                element.style.top = `${round(bounds.minY - padding)}px`;
                element.style.height = `${round((bounds.maxY - bounds.minY) + (padding * 2))}px`;
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

    function legacyRenderConnectorAnchorOverlay(state, visibleNodes) {
        if (!state?.anchorLayer) {
            return;
        }

        state.anchorLayer.innerHTML = "";
        state.anchorLayer.style.opacity = "1";
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
                const anchor = createElement(state.document, "div", `cw-connector-anchor is-${point.side}`);
                anchor.dataset.nodeId = nodeId;
                anchor.dataset.side = point.side;
                anchor.title = `${node.title || node.kind || "Node"} ${point.side} anchor`;
                if (isPrimary) {
                    anchor.classList.add("is-primary");
                }

                if (point.accentColor) {
                    const anchorRing = lateRuntime.hexToRgba?.(point.accentColor, 0.2) || "rgba(125, 211, 252, 0.16)";
                    const anchorShadow = lateRuntime.hexToRgba?.(point.accentColor, 0.18) || "rgba(14, 165, 233, 0.18)";
                    anchor.style.setProperty("--cw-connector-anchor-accent", point.accentColor);
                    anchor.style.setProperty("--cw-connector-anchor-ring", anchorRing);
                    anchor.style.setProperty("--cw-connector-anchor-shadow", anchorShadow);
                }

                anchor.style.left = `${round(point.x)}px`;
                anchor.style.top = `${round(point.y)}px`;
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

    function getSelectionBounds(state, visibleNodes) {
        const selectedNodes = (visibleNodes || []).filter(node => state.selectedIds.has(node.id));
        if (selectedNodes.length === 0) {
            return null;
        }

        let minX = Number.POSITIVE_INFINITY;
        let minY = Number.POSITIVE_INFINITY;
        let maxX = Number.NEGATIVE_INFINITY;
        let maxY = Number.NEGATIVE_INFINITY;
        let isReadOnly = true;

        for (const node of selectedNodes) {
            const position = getNodePosition(state, node);
            const size = getNodeSize(state, node);
            minX = Math.min(minX, position.x - (size.width / 2));
            minY = Math.min(minY, position.y - (size.height / 2));
            maxX = Math.max(maxX, position.x + (size.width / 2));
            maxY = Math.max(maxY, position.y + (size.height / 2));
            isReadOnly = isReadOnly && !!node.isReadOnly;
        }

        return {
            minX,
            minY,
            maxX,
            maxY,
            width: Math.max(0, maxX - minX),
            height: Math.max(0, maxY - minY),
            selectedCount: selectedNodes.length,
            isReadOnly
        };
    }

    function legacyRenderTransformHandlesOverlay(state, visibleNodes) {
        if (!state?.transformLayer) {
            return;
        }

        state.transformLayer.innerHTML = "";
        const handles = state.surface?.chrome?.transformHandles || {};
        if (!handles.isEnabled) {
            return;
        }

        const selectionBounds = getSelectionBounds(state, visibleNodes);
        if (!selectionBounds) {
            return;
        }

        const frame = createElement(state.document, "div", "cw-transform-frame");
        frame.style.left = `${round(selectionBounds.minX)}px`;
        frame.style.top = `${round(selectionBounds.minY)}px`;
        frame.style.width = `${round(selectionBounds.width)}px`;
        frame.style.height = `${round(selectionBounds.height)}px`;
        frame.dataset.selectedCount = `${selectionBounds.selectedCount}`;
        if (selectionBounds.isReadOnly) {
            frame.classList.add("is-read-only");
        }

        state.transformLayer.appendChild(frame);

        if (handles.showResizeHandles) {
            for (const position of ["nw", "n", "ne", "e", "se", "s", "sw", "w"]) {
                const handle = createElement(state.document, "div", `cw-transform-handle is-${position}`);
                if (selectionBounds.isReadOnly) {
                    handle.classList.add("is-read-only");
                }

                handle.setAttribute("aria-hidden", "true");
                frame.appendChild(handle);
            }
        }

        if (handles.showRotateHandle) {
            const stem = createElement(state.document, "div", "cw-transform-rotate-stem");
            const rotate = createElement(state.document, "div", "cw-transform-rotate-handle");
            if (selectionBounds.isReadOnly) {
                stem.classList.add("is-read-only");
                rotate.classList.add("is-read-only");
            }

            frame.appendChild(stem);
            frame.appendChild(rotate);
        }
    }

    function resolveSnapAdjustment(state, interaction, deltaX, deltaY) {
        const snapGuides = state.surface.chrome.snapGuides || {};
        if (!snapGuides.isEnabled) {
            return {
                deltaX,
                deltaY,
                guides: []
            };
        }

        const movingIds = new Set(interaction?.nodeIds || []);
        const movingNodes = (interaction?.nodeIds || [])
            .map(nodeId => state.lookups.byId.get(nodeId))
            .filter(Boolean);
        const stationaryNodes = getVisibleNodes(state).filter(node => !movingIds.has(node.id));
        const tolerance = (snapGuides.tolerance || 18) / Math.max(state.ui.zoom || 1, 0.25);
        let bestX = null;
        let bestY = null;

        for (const movingNode of movingNodes) {
            const startPosition = interaction.startPositions?.[movingNode.id];
            if (!startPosition) {
                continue;
            }

            const movingPoint = {
                x: startPosition.x + deltaX,
                y: startPosition.y + deltaY
            };

            for (const stationaryNode of stationaryNodes) {
                const stationaryPoint = getNodePosition(state, stationaryNode);
                const offsetX = stationaryPoint.x - movingPoint.x;
                if (Math.abs(offsetX) <= tolerance && (!bestX || Math.abs(offsetX) < Math.abs(bestX.offset))) {
                    bestX = { offset: offsetX, value: stationaryPoint.x };
                }

                const offsetY = stationaryPoint.y - movingPoint.y;
                if (Math.abs(offsetY) <= tolerance && (!bestY || Math.abs(offsetY) < Math.abs(bestY.offset))) {
                    bestY = { offset: offsetY, value: stationaryPoint.y };
                }
            }
        }

        const adjustedDeltaX = bestX ? deltaX + bestX.offset : deltaX;
        const adjustedDeltaY = bestY ? deltaY + bestY.offset : deltaY;
        const guides = [];
        if (bestX) {
            guides.push({ orientation: "vertical", value: bestX.value });
        }

        if (bestY) {
            guides.push({ orientation: "horizontal", value: bestY.value });
        }

        return {
            deltaX: adjustedDeltaX,
            deltaY: adjustedDeltaY,
            guides
        };
    }

    function legacyRenderDebugDecorations(state, visibleNodes) {
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
            for (const node of visibleNodes) {
                const position = getNodePosition(state, node);
                const size = getNodeSize(state, node);
                const bounds = createElement(state.document, "div", "cw-debug-bounds");
                bounds.style.left = `${round(position.x - (size.width / 2))}px`;
                bounds.style.top = `${round(position.y - (size.height / 2))}px`;
                bounds.style.width = `${round(size.width)}px`;
                bounds.style.height = `${round(size.height)}px`;
                state.debugLayer.appendChild(bounds);
            }
        }

        if (diagnostics.showConnectorAnchors) {
            const visibleLookup = new Set(visibleNodes.map(node => node.id));
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
                for (const point of [
                    getLinkAnchorPoint(state, source, sourceSide, link.sourcePortId, "output"),
                    getLinkAnchorPoint(state, target, targetSide, link.targetPortId, "input")
                ]) {
                    const dot = createElement(state.document, "div", "cw-debug-anchor");
                    dot.style.left = `${round(point.x)}px`;
                    dot.style.top = `${round(point.y)}px`;
                    state.debugLayer.appendChild(dot);
                }
            }
        }
    }

    function legacyBuildDiagnosticsSnapshot(state, bounds) {
        return {
            isVisible: !!(state?.surface?.chrome?.diagnostics?.isEnabled && state?.ui?.showDiagnostics),
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
            metrics: cloneWorkbenchMetrics(state?.metrics)
        };
    }

    function renderDiagnosticsOverlay(state, visibleNodes) {
        if (!state?.diagnosticsPanel) {
            return;
        }

        const diagnostics = state.surface.chrome.diagnostics || {};
        const enabled = diagnostics.isEnabled && state.ui.showDiagnostics;
        state.diagnosticsPanel.style.display = enabled ? "grid" : "none";
        if (!enabled) {
            return;
        }

        const bounds = getSceneBounds(state);
        const snapshot = buildDiagnosticsSnapshot(state, bounds);
        state.diagnosticsBody.innerHTML = "";
        const rows = [
            ["Nodes", `${snapshot.visibleNodeCount}/${snapshot.totalNodeCount}`],
            ["Links", `${snapshot.totalLinkCount}`],
            ["Selected", `${snapshot.selectedCount}`],
            ["Interaction", snapshot.interaction],
            ["Zoom", `${snapshot.zoomPercent}%`],
            ["Pan", `${snapshot.panX}, ${snapshot.panY}`],
            ["Render count", `${snapshot.metrics.renderCount}`],
            ["Node rebuilds", `${snapshot.metrics.nodeLayerRebuildCount}`],
            ["Link rebuilds", `${snapshot.metrics.linkLayerRebuildCount}`],
            ["Frame rebuilds", `${snapshot.metrics.frameLayerRebuildCount}`],
            ["Drag patches", `${snapshot.metrics.dragPatchCount}`],
            ["Last drag patch", `${snapshot.metrics.lastDragPatchedNodeCount}/${snapshot.metrics.lastDragPatchedLinkCount}/${snapshot.metrics.lastDragPatchedFrameCount}`],
            ["State commits", `${snapshot.metrics.statePublishCommitCount}`],
            ["Last publish", snapshot.metrics.lastStatePublishMode || "none"],
            ["Last render", formatMetricDuration(snapshot.metrics.lastRenderDurationMs)]
        ];

        if (diagnostics.showViewportStats && bounds) {
            rows.push(["Bounds", `${round(bounds.minX)}:${round(bounds.minY)} to ${round(bounds.maxX)}:${round(bounds.maxY)}`]);
        }

        for (const [label, value] of rows) {
            const row = createElement(state.document, "div", "cw-diagnostics__row");
            row.appendChild(createElement(state.document, "span", "cw-diagnostics__label", label));
            row.appendChild(createElement(state.document, "strong", "cw-diagnostics__value", value));
            state.diagnosticsBody.appendChild(row);
        }
    }

    function navigateViaMinimap(state, event) {
        if (!state?.minimapMetrics) {
            return;
        }

        const rect = state.minimapCanvas.getBoundingClientRect();
        const x = clamp(event.clientX - rect.left, 0, rect.width);
        const y = clamp(event.clientY - rect.top, 0, rect.height);
        const metrics = state.minimapMetrics;
        const worldX = metrics.bounds.minX + ((x - metrics.offsetX) / metrics.scale);
        const worldY = metrics.bounds.minY + ((y - metrics.offsetY) / metrics.scale);
        const hostRect = state.host.getBoundingClientRect();
        setPan(
            state,
            (hostRect.width / 2) - (worldX * state.ui.zoom),
            (hostRect.height / 2) - (worldY * state.ui.zoom));
        renderState(state);
        publishStateNow(state);
    }

    async function writeClipboardText(payload) {
        if (!payload) {
            return false;
        }

        if (typeof window.__canvasClipboardWrite === "function") {
            try {
                await window.__canvasClipboardWrite(payload);
                return true;
            }
            catch {
            }
        }

        if (!navigator?.clipboard?.writeText) {
            return false;
        }

        try {
            await navigator.clipboard.writeText(payload);
            return true;
        }
        catch {
            return false;
        }
    }

    async function readClipboardText() {
        if (typeof window.__canvasClipboardRead === "function") {
            try {
                return await window.__canvasClipboardRead();
            }
            catch {
            }
        }

        if (!navigator?.clipboard?.readText) {
            return "";
        }

        try {
            return await navigator.clipboard.readText();
        }
        catch {
            return "";
        }
    }

    function resolveClipboardAnchor(state) {
        const rect = state.host.getBoundingClientRect();
        return getWorldPoint(state, rect.left + (rect.width / 2), rect.top + (rect.height / 2));
    }

    function buildClipboardPayload(state, operation = "copy") {
        const selectedNodeIds = [...state.selectedIds];
        const selectedNodes = selectedNodeIds
            .map(nodeId => state.lookups.byId.get(nodeId))
            .filter(Boolean)
            .map(node => ({
                id: node.id,
                title: node.title || "",
                kind: node.kind || "",
                family: node.family || "",
                position: getNodePosition(state, node)
            }));

        return {
            operation,
            format: state.surface.chrome.clipboard.format,
            surfaceId: state.surface.surfaceId,
            capturedAtUtc: new Date().toISOString(),
            selectedNodeIds,
            selectedNodes
        };
    }

    function copySelectionToClipboard(state) {
        const clipboard = state.surface.chrome.clipboard || {};
        if (!clipboard.isEnabled || !clipboard.allowCopy || state.selectedIds.size === 0) {
            return;
        }

        const payload = JSON.stringify(buildClipboardPayload(state, "copy"));
        state.localClipboard = payload;
        void writeClipboardText(payload);
        state.dotNetRef.invokeMethodAsync("OnClipboardAction", "copy", payload);
        showStatusNotice(state, `Copied ${state.selectedIds.size} node(s)`, "accent");
    }

    function requestClipboardCut(state) {
        const clipboard = state.surface.chrome.clipboard || {};
        if (!clipboard.isEnabled || !clipboard.allowCopy || !clipboard.allowPaste || state.selectedIds.size === 0) {
            return;
        }

        const payload = JSON.stringify(buildClipboardPayload(state, "cut"));
        state.localClipboard = payload;
        void writeClipboardText(payload);
        state.dotNetRef.invokeMethodAsync("OnClipboardAction", "cut", payload);
        showStatusNotice(state, `Cut ${state.selectedIds.size} node(s)`, "warn");
    }

    async function requestClipboardPaste(state) {
        const clipboard = state.surface.chrome.clipboard || {};
        if (!clipboard.isEnabled || !clipboard.allowPaste) {
            return;
        }

        let payload = state.localClipboard || "";
        if (!payload) {
            payload = await readClipboardText();
        }

        if (!payload) {
            showStatusNotice(state, "Clipboard is empty", "warn");
            return;
        }

        const envelope = JSON.stringify({
            payloadJson: payload,
            anchorWorld: resolveClipboardAnchor(state),
            surfaceId: state.surface.surfaceId
        });
        state.dotNetRef.invokeMethodAsync("OnClipboardAction", "paste", envelope);
        showStatusNotice(state, "Paste routed through the shared canvas bridge", "success");
    }

    function requestClipboardDuplicate(state) {
        const clipboard = state.surface.chrome.clipboard || {};
        if (!clipboard.isEnabled || !clipboard.allowDuplicate || state.selectedIds.size === 0) {
            return;
        }

        state.dotNetRef.invokeMethodAsync("OnClipboardAction", "duplicate", JSON.stringify(buildClipboardPayload(state, "duplicate")));
        showStatusNotice(state, "Duplicate request sent to the workspace", "accent");
    }

    function toggleMinimap(state) {
        state.ui.showMinimap = state.ui.showMinimap === false;
        renderState(state);
        publishStateNow(state);
    }

    function toggleDiagnostics(state) {
        state.ui.showDiagnostics = !state.ui.showDiagnostics;
        hidePopover(state);
        renderState(state);
        publishStateNow(state);
    }

    function invalidateMeasuredLayout(state) {
        state.layoutPositions = null;
        state.layoutKey = "";
    }

    function legacyMeasureRenderedNodeSizes(state) {
        if (!state.nodeLayer) {
            return false;
        }

        const zoom = Math.max(state.ui.zoom || 1, 0.01);
        const nextSizes = new Map(state.measuredNodeSizes);
        let changed = false;

        for (const element of state.nodeLayer.querySelectorAll(".cw-node")) {
            const nodeId = element.dataset.nodeId;
            if (!nodeId) {
                continue;
            }

            const rect = element.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) {
                continue;
            }

            const measured = {
                width: round(rect.width / zoom),
                height: round(rect.height / zoom)
            };
            const previous = nextSizes.get(nodeId);
            if (!previous ||
                Math.abs(previous.width - measured.width) > 1 ||
                Math.abs(previous.height - measured.height) > 1) {
                nextSizes.set(nodeId, measured);
                changed = true;
            }
        }

        if (changed) {
            state.measuredNodeSizes = nextSizes;
            invalidateMeasuredLayout(state);
        }

        return changed;
    }

    function legacyScheduleNodeMeasurement(state) {
        if (state.measureLayoutFrame) {
            return;
        }

        state.measureLayoutFrame = window.requestAnimationFrame(() => {
            state.measureLayoutFrame = 0;
            if (measureRenderedNodeSizes(state)) {
                renderState(state);
            }
        });
    }

    function getHostPoint(state, clientX, clientY) {
        const rect = state.host.getBoundingClientRect();
        return {
            x: clientX - rect.left,
            y: clientY - rect.top
        };
    }

    function worldToHostPoint(state, point) {
        const viewportController = getViewportControllerService();
        if (viewportController?.sceneToHost) {
            return viewportController.sceneToHost({
                pointX: point.x,
                pointY: point.y,
                panX: state.ui.panX,
                panY: state.ui.panY,
                zoom: state.ui.zoom
            });
        }

        return {
            x: (point.x * state.ui.zoom) + state.ui.panX,
            y: (point.y * state.ui.zoom) + state.ui.panY
        };
    }

    function hostToWorldPoint(state, point) {
        const zoom = Math.max(state?.ui?.zoom || 1, 0.01);
        const viewportController = getViewportControllerService();
        if (viewportController?.hostToScene) {
            return viewportController.hostToScene({
                pointX: point.x,
                pointY: point.y,
                panX: state.ui.panX,
                panY: state.ui.panY,
                zoom
            });
        }

        return {
            x: (point.x - state.ui.panX) / zoom,
            y: (point.y - state.ui.panY) / zoom
        };
    }

    function getWorldPoint(state, clientX, clientY) {
        const hostPoint = getHostPoint(state, clientX, clientY);
        const viewportController = getViewportControllerService();
        if (viewportController?.hostToScene) {
            return viewportController.hostToScene({
                pointX: hostPoint.x,
                pointY: hostPoint.y,
                panX: state.ui.panX,
                panY: state.ui.panY,
                zoom: state.ui.zoom
            });
        }

        return {
            x: (hostPoint.x - state.ui.panX) / state.ui.zoom,
            y: (hostPoint.y - state.ui.panY) / state.ui.zoom
        };
    }

    Object.assign(shared, { getLinkAnchorPoint, resolveLinkAnchorSides, resolveCollapseAnchorInfo, getLinkRetainedKey, getLinkPathData, updateLinkElement, shouldRenderArrow, getExpandedFrameNodeIds, getFrameRetainedKey, createFrameElement, updateFrameElement, getFrameBounds, legacyRenderGroupFrames, resolveChipToneClass, createProgressMarker, resolveProgressDisplay, createProgressBadge, resolveProgressPresetBadgeOptions, resolveMarkerGlyph, resolveNodeMarkers, createMarkerBadge, createMarkerBadges, createPriorityBadge, appendNodeIndicators, renderInlineTextNode, createNodeMedia, createCompactPathButton, renderStandardNode, createRetainedNodeElement, getNodeRetainedContentKey, updateNodeElementChrome, renderNodeElementContent, buildActiveDragContext, positionFloatingOverlayWithinHost, hidePopover, legacyShowPopover, invokeAnnotationAction, renderNodeAnnotations, updateConnectorAnchorHover, getConnectorAnchorPoints, hideStatusNotice, showStatusNotice, renderEmptyStateOverlay, clearSnapGuides, legacyRenderSnapGuides, legacyRenderConnectorAnchorOverlay, getSelectionBounds, legacyRenderTransformHandlesOverlay, resolveSnapAdjustment, legacyRenderDebugDecorations, legacyBuildDiagnosticsSnapshot, renderDiagnosticsOverlay, navigateViaMinimap, resolveClipboardAnchor, buildClipboardPayload, writeClipboardText, readClipboardText, copySelectionToClipboard, requestClipboardCut, requestClipboardDuplicate, toggleMinimap, toggleDiagnostics, invalidateMeasuredLayout, legacyMeasureRenderedNodeSizes, legacyScheduleNodeMeasurement, getHostPoint, worldToHostPoint, getWorldPoint });
})();
