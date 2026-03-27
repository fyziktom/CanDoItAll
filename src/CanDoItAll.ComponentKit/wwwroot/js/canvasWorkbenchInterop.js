(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const MIN_ZOOM = 0.15;
    const MAX_ZOOM = 1.75;
    function getTextMeasureService() {
        return root.textMeasureService || null;
    }
    function getViewportControllerService() {
        return root.viewportController || null;
    }
    function getAnimationTimelineService() {
        return root.animationTimeline || null;
    }
    const selectionModel = root.selectionModel || {
        normalize(selectedNodeIds, primaryNodeId) {
            const seen = new Set();
            const ordered = [];

            for (const value of Array.isArray(selectedNodeIds) ? selectedNodeIds : []) {
                const normalized = typeof value === "string" ? value.trim() : "";
                if (!normalized || seen.has(normalized)) {
                    continue;
                }

                seen.add(normalized);
                ordered.push(normalized);
            }

            const normalizedPrimary = typeof primaryNodeId === "string" ? primaryNodeId.trim() : "";
            if (normalizedPrimary) {
                const existingIndex = ordered.indexOf(normalizedPrimary);
                if (existingIndex >= 0) {
                    ordered.splice(existingIndex, 1);
                }

                ordered.unshift(normalizedPrimary);
            }

            return {
                primaryNodeId: ordered[0] || null,
                selectedNodeIds: ordered
            };
        },
        replace(selectedNodeIds, primaryNodeId) {
            return this.normalize(selectedNodeIds, primaryNodeId);
        },
        selectOne(nodeId) {
            return this.normalize([nodeId], nodeId);
        },
        toggle(selectedNodeIds, nodeId, primaryNodeId) {
            const normalizedNodeId = typeof nodeId === "string" ? nodeId.trim() : "";
            const next = this.normalize(selectedNodeIds, primaryNodeId).selectedNodeIds;
            if (!normalizedNodeId) {
                return this.normalize(next, primaryNodeId);
            }

            const existingIndex = next.indexOf(normalizedNodeId);
            if (existingIndex >= 0) {
                next.splice(existingIndex, 1);
                return this.normalize(next, primaryNodeId === normalizedNodeId ? null : primaryNodeId);
            }

            next.push(normalizedNodeId);
            return this.normalize(next, primaryNodeId || normalizedNodeId);
        },
        clear() {
            return {
                primaryNodeId: null,
                selectedNodeIds: []
            };
        },
        removeMissing(selectedNodeIds, validNodeIds, primaryNodeId) {
            const valid = new Set(Array.isArray(validNodeIds) ? validNodeIds.filter(Boolean) : []);
            const filtered = (Array.isArray(selectedNodeIds) ? selectedNodeIds : []).filter(nodeId => valid.has(nodeId));
            return this.normalize(filtered, valid.has(primaryNodeId) ? primaryNodeId : null);
        }
    };

    function clear(element) {
        while (element.firstChild) {
            element.removeChild(element.firstChild);
        }
    }

    function createElement(document, tagName, className, text) {
        const element = document.createElement(tagName);
        if (className) {
            element.className = className;
        }

        if (typeof text === "string") {
            element.textContent = text;
        }

        return element;
    }

    function createSvgElement(document, tagName, className) {
        const element = document.createElementNS("http://www.w3.org/2000/svg", tagName);
        if (className) {
            element.setAttribute("class", className);
        }

        return element;
    }

    function normalizeInputField(field) {
        return {
            key: field?.key || "",
            label: field?.label || field?.key || "Value",
            placeholder: field?.placeholder || "",
            inputMode: field?.inputMode || "text",
            isRequired: !!field?.isRequired
        };
    }

    function normalizeInputValue(value) {
        return {
            key: value?.key || "",
            value: value?.value || ""
        };
    }

    function clamp(value, min, max) {
        return Math.max(min, Math.min(max, value));
    }

    function debounce(callback, delayMs) {
        let handle;
        return (...args) => {
            window.clearTimeout(handle);
            handle = window.setTimeout(() => callback(...args), delayMs);
        };
    }

    function round(value) {
        return Math.round(value * 100) / 100;
    }

    function normalizeAction(action) {
        return {
            ...action,
            description: action?.description || "",
            menuLabel: action?.menuLabel || "",
            menuSize: action?.menuSize || "normal",
            submenuLayout: action?.submenuLayout || "",
            requiresInput: !!action?.requiresInput,
            createMode: action?.createMode || "command",
            objectSubtype: action?.objectSubtype || "",
            titleLabel: action?.titleLabel || "Title",
            titlePlaceholder: action?.titlePlaceholder || "",
            subtitleLabel: action?.subtitleLabel || "Subtitle",
            subtitlePlaceholder: action?.subtitlePlaceholder || "",
            notesLabel: action?.notesLabel || "Notes",
            notesPlaceholder: action?.notesPlaceholder || "",
            showDefaultTextFields: action?.showDefaultTextFields !== false,
            submitLabel: action?.submitLabel || action?.label || "Create",
            requiresFile: !!action?.requiresFile,
            acceptedFileTypes: action?.acceptedFileTypes || "",
            filePrompt: action?.filePrompt || "Drop a file here or choose one.",
            supportsDragDrop: action?.supportsDragDrop !== false,
            inputFields: Array.isArray(action?.inputFields) ? action.inputFields.map(normalizeInputField) : [],
            defaultInputValues: Array.isArray(action?.defaultInputValues) ? action.defaultInputValues.map(normalizeInputValue) : [],
            children: Array.isArray(action?.children) ? action.children.map(normalizeAction) : []
        };
    }

    function normalizeAnnotation(annotation) {
        return {
            id: annotation?.id || "",
            kind: annotation?.kind || "info",
            tone: annotation?.tone || "accent",
            label: annotation?.label || "",
            description: annotation?.description || "",
            icon: annotation?.icon || "",
            actionId: annotation?.actionId || ""
        };
    }

    function normalizeDiagnosticsOptions(options) {
        return {
            isEnabled: !!options?.isEnabled,
            showNodeBounds: options?.showNodeBounds !== false,
            showConnectorAnchors: options?.showConnectorAnchors !== false,
            showViewportStats: options?.showViewportStats !== false
        };
    }

    function normalizeMinimapOptions(options) {
        return {
            isEnabled: options?.isEnabled !== false,
            title: options?.title || "Scene overview"
        };
    }

    function normalizeClipboardOptions(options) {
        return {
            isEnabled: options?.isEnabled !== false,
            allowCopy: options?.allowCopy !== false,
            allowPaste: options?.allowPaste !== false,
            allowDuplicate: options?.allowDuplicate !== false,
            format: options?.format || "application/vnd.candoitall.canvas+json"
        };
    }

    function normalizeTooltipPopoverOptions(options) {
        return {
            isEnabled: options?.isEnabled !== false,
            focusTriggers: options?.focusTriggers !== false,
            supportsRichPreview: options?.supportsRichPreview !== false
        };
    }

    function normalizeMarqueeOptions(options) {
        const modifierKey = (options?.modifierKey || "Alt").toString().trim().toLowerCase();
        const selectionMode = (options?.selectionMode || "Intersect").toString().trim().toLowerCase();
        return {
            isEnabled: options?.isEnabled !== false,
            modifierKey: modifierKey || "alt",
            selectionMode: selectionMode === "contain" ? "contain" : "intersect"
        };
    }

    function normalizeSnapGuideOptions(options) {
        const tolerance = typeof options?.tolerance === "number" && Number.isFinite(options.tolerance)
            ? Math.max(0, options.tolerance)
            : 18;
        const modifierPolicy = (options?.modifierPolicy || "ShiftBypassesSnap").toString().trim().toLowerCase();
        return {
            isEnabled: options?.isEnabled !== false,
            tolerance,
            modifierPolicy: modifierPolicy === "none" ? "none" : "shift-bypasses-snap"
        };
    }

    function normalizeConnectorAnchorOptions(options) {
        const placementMode = (options?.placementMode || "Edges").toString().trim().toLowerCase();
        return {
            isEnabled: options?.isEnabled !== false,
            showOnHover: options?.showOnHover !== false,
            showOnSelection: options?.showOnSelection !== false,
            placementMode: placementMode || "edges"
        };
    }

    function normalizeTransformHandleOptions(options) {
        const placementMode = (options?.placementMode || "SelectionBounds").toString().trim().toLowerCase();
        return {
            isEnabled: options?.isEnabled !== false,
            showResizeHandles: options?.showResizeHandles !== false,
            showRotateHandle: options?.showRotateHandle !== false,
            placementMode: placementMode || "selectionbounds"
        };
    }

    function normalizeGroupFrame(frame) {
        return {
            id: frame?.id || "",
            label: frame?.label || "Group",
            tone: frame?.tone || "accent",
            anchorNodeIds: Array.isArray(frame?.anchorNodeIds) ? frame.anchorNodeIds.filter(Boolean) : []
        };
    }

    function normalizeProgressPercent(value) {
        if (typeof value !== "number" || Number.isNaN(value)) {
            return 0;
        }

        return clamp(Math.round(value), 0, 100);
    }

    function normalizeMenuActionScale(value) {
        if (typeof value !== "number" || Number.isNaN(value)) {
            return 1;
        }

        return clamp(round(value), 0.8, 1.4);
    }

    function normalizeSurface(surface) {
        const normalizedSelection = selectionModel.normalize(
            surface?.uiState?.selectedNodeIds,
            Array.isArray(surface?.uiState?.selectedNodeIds) ? surface.uiState.selectedNodeIds[0] : null);
        const viewportController = getViewportControllerService();
        const normalizedViewport = viewportController?.normalizeUiState?.(surface?.uiState) || {
            zoom: typeof surface?.uiState?.zoom === "number" ? clamp(surface.uiState.zoom, MIN_ZOOM, MAX_ZOOM) : 1,
            panX: typeof surface?.uiState?.panX === "number" ? surface.uiState.panX : 90,
            panY: typeof surface?.uiState?.panY === "number" ? surface.uiState.panY : 110
        };

        return {
            surfaceId: surface?.surfaceId || "canvas-surface",
            mode: surface?.mode || "authoring",
            nodes: Array.isArray(surface?.nodes) ? surface.nodes.map(node => ({
                ...node,
                x: typeof node.x === "number" ? node.x : 120,
                y: typeof node.y === "number" ? node.y : 120,
                chips: Array.isArray(node.chips) ? node.chips : [],
                footerChips: Array.isArray(node.footerChips) ? node.footerChips : [],
                contextActions: Array.isArray(node.contextActions) ? node.contextActions.map(normalizeAction) : [],
                isInlineTextNode: !!node?.isInlineTextNode,
                inlineText: node?.inlineText || "",
                inlineTextPlaceholder: node?.inlineTextPlaceholder || "Write note",
                mediaKind: node?.mediaKind || "",
                mediaPreviewUrl: node?.mediaPreviewUrl || "",
                mediaPreviewAlt: node?.mediaPreviewAlt || node?.title || "",
                mediaContentType: node?.mediaContentType || "",
                mediaFileName: node?.mediaFileName || "",
                progressMode: node?.progressMode || "na",
                progressPercent: normalizeProgressPercent(node?.progressPercent),
                markerIcon: node?.markerIcon || "",
                markerTone: node?.markerTone || "",
                markerLabel: node?.markerLabel || "",
                priority: typeof node?.priority === "number" ? clamp(Math.round(node.priority), 0, 6) : 0,
                annotations: Array.isArray(node?.annotations) ? node.annotations.map(normalizeAnnotation) : []
            })) : [],
            links: Array.isArray(surface?.links) ? surface.links : [],
            uiState: {
                version: surface?.uiState?.version || "canvas-workbench.v1",
                selectedNodeIds: normalizedSelection.selectedNodeIds,
                collapsedNodeIds: Array.isArray(surface?.uiState?.collapsedNodeIds) ? [...surface.uiState.collapsedNodeIds] : [],
                groupFrames: Array.isArray(surface?.uiState?.groupFrames) ? surface.uiState.groupFrames.map(normalizeGroupFrame) : [],
                manualPositions: surface?.uiState?.manualPositions || {},
                windowStates: surface?.uiState?.windowStates || {},
                zoom: normalizedViewport.zoom,
                panX: normalizedViewport.panX,
                panY: normalizedViewport.panY,
                menuActionScale: normalizeMenuActionScale(surface?.uiState?.menuActionScale),
                isMaximized: !!surface?.uiState?.isMaximized,
                activeInspectorTab: surface?.uiState?.activeInspectorTab || "",
                showDiagnostics: !!surface?.uiState?.showDiagnostics,
                showMinimap: surface?.uiState?.showMinimap !== false
            },
            chrome: {
                quickCreateActions: Array.isArray(surface?.chrome?.quickCreateActions) ? surface.chrome.quickCreateActions.map(normalizeAction) : [],
                groupContextActions: Array.isArray(surface?.chrome?.groupContextActions) ? surface.chrome.groupContextActions.map(normalizeAction) : [],
                showQuickCreateRail: surface?.chrome?.showQuickCreateRail !== false,
                childNoteActionId: surface?.chrome?.childNoteActionId || "",
                siblingNoteActionId: surface?.chrome?.siblingNoteActionId || "",
                inlineNotePlaceholder: surface?.chrome?.inlineNotePlaceholder || "Write note",
                hintText: surface?.chrome?.hintText || "",
                emptyStateKicker: surface?.chrome?.emptyStateKicker || "Canvas",
                emptyStateTitle: surface?.chrome?.emptyStateTitle || "No nodes yet",
                emptyStateDescription: surface?.chrome?.emptyStateDescription || "Use quick create to start building the scene.",
                diagnostics: normalizeDiagnosticsOptions(surface?.chrome?.diagnostics),
                minimap: normalizeMinimapOptions(surface?.chrome?.minimap),
                clipboard: normalizeClipboardOptions(surface?.chrome?.clipboard),
                tooltipPopover: normalizeTooltipPopoverOptions(surface?.chrome?.tooltipPopover),
                marqueeSelection: normalizeMarqueeOptions(surface?.chrome?.marqueeSelection),
                snapGuides: normalizeSnapGuideOptions(surface?.chrome?.snapGuides),
                connectorAnchors: normalizeConnectorAnchorOptions(surface?.chrome?.connectorAnchors),
                transformHandles: normalizeTransformHandleOptions(surface?.chrome?.transformHandles)
            }
        };
    }

    function toSelectionSet(selectedNodeIds) {
        return new Set((selectedNodeIds || []).filter(Boolean));
    }

    function toCollapsedSet(collapsedNodeIds) {
        return new Set((collapsedNodeIds || []).filter(Boolean));
    }

    function getDefaultNodeSize(node) {
        const baseSize = resolveBaseNodeSize(node);
        return estimateNodeSizeFromText(node, baseSize);
    }

    function resolveBaseNodeSize(node) {
        if (node.isInlineTextNode) {
            return { width: 228, height: 108 };
        }

        switch ((node.family || "item").toLowerCase()) {
            case "root":
                return { width: 288, height: 210 };
            case "group":
                return { width: 272, height: 196 };
            case "special":
                return { width: 248, height: 178 };
            default:
                return { width: 256, height: 190 };
        }
    }

    function estimateNodeSizeFromText(node, baseSize) {
        const measureService = getTextMeasureService();
        if (!measureService || typeof measureService.measure !== "function") {
            return baseSize;
        }

        const family = (node.family || "item").toLowerCase();
        const titleText = node.isInlineTextNode
            ? (node.inlineText || node.title || node.subtitle || "Untitled")
            : (node.title || "Untitled");
        const subtitleText = node.isInlineTextNode
            ? (node.subtitle || "")
            : (node.subtitle || node.leadText || "");
        const chipText = Array.isArray(node.chips) && node.chips.length > 0
            ? node.chips[0].text
            : Array.isArray(node.footerChips) && node.footerChips.length > 0
                ? node.footerChips[0].text
                : "";
        const titleWidth = Math.max(124, baseSize.width - (family === "root" ? 86 : 72));
        const subtitleWidth = Math.max(120, baseSize.width - 72);
        const titleMeasure = measureService.measure({
            text: titleText,
            maxWidth: titleWidth,
            maxLines: node.isInlineTextNode ? 4 : 2,
            font: {
                family: "\"DM Sans\", sans-serif",
                sizePx: family === "root" ? 18 : node.isInlineTextNode ? 14 : 15,
                weight: 700,
                lineHeightPx: node.isInlineTextNode ? 20 : family === "root" ? 22 : 18
            }
        });
        const subtitleMeasure = subtitleText
            ? measureService.measure({
                text: subtitleText,
                maxWidth: subtitleWidth,
                maxLines: 2,
                font: {
                    family: "\"DM Sans\", sans-serif",
                    sizePx: 12,
                    weight: 600,
                    lineHeightPx: 16
                }
            })
            : null;
        const chipMeasure = chipText
            ? measureService.measure({
                text: chipText,
                maxWidth: Math.max(96, baseSize.width - 96),
                maxLines: 1,
                font: {
                    family: "\"DM Sans\", sans-serif",
                    sizePx: 11,
                    weight: 700,
                    lineHeightPx: 12
                }
            })
            : null;
        const annotationHeight = Array.isArray(node.annotations) ? Math.ceil(node.annotations.length / 2) * 14 : 0;
        const footerHeight = (Array.isArray(node.chips) && node.chips.length > 0 ? 18 : 0) +
            (Array.isArray(node.footerChips) && node.footerChips.length > 0 ? 18 : 0);
        const mediaHeight = node.mediaPreviewUrl ? 62 : 0;
        const bodyHeight = 92 +
            titleMeasure.estimatedHeight +
            (subtitleMeasure ? subtitleMeasure.estimatedHeight + 8 : 0) +
            annotationHeight +
            footerHeight +
            mediaHeight;
        const estimatedWidth = Math.ceil(Math.max(
            baseSize.width,
            Math.min(
                356,
                Math.max(
                    titleMeasure.estimatedWidth + (family === "root" ? 82 : 68),
                    subtitleMeasure ? subtitleMeasure.estimatedWidth + 68 : 0,
                    chipMeasure ? chipMeasure.estimatedWidth + 96 : 0))));
        const estimatedHeight = Math.ceil(Math.max(baseSize.height, Math.min(340, bodyHeight)));

        return {
            width: estimatedWidth,
            height: estimatedHeight
        };
    }

    function getNodeSize(state, node) {
        const measured = state?.measuredNodeSizes?.get(node.id);
        if (measured?.width > 0 && measured?.height > 0) {
            return measured;
        }

        return getDefaultNodeSize(node);
    }

    function buildNodeLookup(nodes) {
        const byId = new Map();
        const children = new Map();

        for (const node of nodes) {
            byId.set(node.id, node);
            if (node.parentId) {
                if (!children.has(node.parentId)) {
                    children.set(node.parentId, []);
                }

                children.get(node.parentId).push(node.id);
            }
        }

        return { byId, children };
    }

    function isNodeVisible(state, nodeId) {
        let current = state.lookups.byId.get(nodeId);
        while (current) {
            if (current.parentId && state.collapsedIds.has(current.parentId)) {
                return false;
            }

            current = current.parentId ? state.lookups.byId.get(current.parentId) : null;
        }

        return true;
    }

    function getVisibleNodes(state) {
        return state.surface.nodes.filter(node => isNodeVisible(state, node.id));
    }

    function getBaseNodePosition(state, node) {
        const manual = state.ui.manualPositions?.[node.id];
        return manual && typeof manual.x === "number" && typeof manual.y === "number"
            ? { x: manual.x, y: manual.y }
            : { x: node.x, y: node.y };
    }

    function getNodeDepth(state, nodeId, cache) {
        if (cache.has(nodeId)) {
            return cache.get(nodeId);
        }

        const node = state.lookups.byId.get(nodeId);
        if (!node || !node.parentId) {
            cache.set(nodeId, 0);
            return 0;
        }

        const depth = getNodeDepth(state, node.parentId, cache) + 1;
        cache.set(nodeId, depth);
        return depth;
    }

    function getNodeMobility(state, node) {
        if ((node.family || "").toLowerCase() === "root") {
            return 0.04;
        }

        if (node.isRequired) {
            return 0.18;
        }

        if (state.ui.manualPositions?.[node.id]) {
            return 0.42;
        }

        return 1;
    }

    function buildResolvedLayoutKey(state, visibleNodes) {
        return visibleNodes.map(node => {
            const base = getBaseNodePosition(state, node);
            return [
                node.id,
                node.parentId || "",
                round(base.x),
                round(base.y),
                node.family || "",
                node.isInlineTextNode ? "1" : "0"
            ].join("|");
        }).join(";");
    }

    function buildLayoutItems(state, visibleNodes) {
        const depthCache = new Map();
        const basePositions = new Map();
        for (const node of visibleNodes) {
            basePositions.set(node.id, getBaseNodePosition(state, node));
        }

        return visibleNodes.map(node => {
            const base = basePositions.get(node.id);
            const parentBase = node.parentId ? basePositions.get(node.parentId) : null;
            const horizontalDelta = parentBase ? (base.x - parentBase.x) : 0;
            const verticalDelta = parentBase ? (base.y - parentBase.y) : 0;
            return {
                id: node.id,
                node,
                parentId: node.parentId || null,
                size: getNodeSize(state, node),
                base,
                depth: getNodeDepth(state, node.id, depthCache),
                preferredSideX: horizontalDelta >= 0 ? 1 : -1,
                preferredSideY: Math.abs(verticalDelta) > 4 ? Math.sign(verticalDelta) : 1,
                mobility: getNodeMobility(state, node)
            };
        });
    }

    function getCollisionPaddingX(first, second) {
        let padding = 28;
        if (first.parentId === second.id || second.parentId === first.id) {
            padding += 26;
        }
        else if (first.parentId && first.parentId === second.parentId) {
            padding += 12;
        }

        if ((first.node.family || "").toLowerCase() === "root" || (second.node.family || "").toLowerCase() === "root") {
            padding += 18;
        }

        return padding;
    }

    function getCollisionPaddingY(first, second) {
        let padding = 24;
        if (first.parentId && first.parentId === second.parentId) {
            padding += 16;
        }

        return padding;
    }

    function getOverlapDelta(first, second, firstPosition, secondPosition) {
        const deltaX = secondPosition.x - firstPosition.x;
        const deltaY = secondPosition.y - firstPosition.y;
        const overlapX = ((first.size.width + second.size.width) / 2) + getCollisionPaddingX(first, second) - Math.abs(deltaX);
        const overlapY = ((first.size.height + second.size.height) / 2) + getCollisionPaddingY(first, second) - Math.abs(deltaY);
        return { deltaX, deltaY, overlapX, overlapY };
    }

    function chooseCollisionAxis(first, second, overlap) {
        if (first.parentId === second.id || second.parentId === first.id) {
            return "x";
        }

        if (first.parentId && first.parentId === second.parentId) {
            return overlap.overlapY <= (overlap.overlapX * 1.35) ? "y" : "x";
        }

        return overlap.overlapX <= overlap.overlapY ? "x" : "y";
    }

    function resolveCollisionDirection(first, second, axis, overlap) {
        const delta = axis === "x" ? overlap.deltaX : overlap.deltaY;
        if (Math.abs(delta) > 0.5) {
            return Math.sign(delta);
        }

        if (axis === "x") {
            if (first.parentId === second.id) {
                return -(first.preferredSideX || 1);
            }

            if (second.parentId === first.id) {
                return second.preferredSideX || 1;
            }

            const baseDeltaX = second.base.x - first.base.x;
            if (Math.abs(baseDeltaX) > 0.5) {
                return Math.sign(baseDeltaX);
            }
        }
        else {
            const baseDeltaY = second.base.y - first.base.y;
            if (Math.abs(baseDeltaY) > 0.5) {
                return Math.sign(baseDeltaY);
            }
        }

        return first.id.localeCompare(second.id) <= 0 ? 1 : -1;
    }

    function applyCollisionSeparation(first, second, firstPosition, secondPosition, axis, amount, direction) {
        if (amount <= 0) {
            return false;
        }

        const totalMobility = Math.max(0.001, first.mobility + second.mobility);
        const firstShare = second.mobility <= 0 ? 0 : (first.mobility / totalMobility);
        const secondShare = first.mobility <= 0 ? 0 : (second.mobility / totalMobility);
        const firstDelta = amount * (secondShare || 0);
        const secondDelta = amount * (firstShare || 0);

        if (axis === "x") {
            if (first.mobility > 0) {
                firstPosition.x -= direction * firstDelta;
            }

            if (second.mobility > 0) {
                secondPosition.x += direction * secondDelta;
            }
        }
        else {
            if (first.mobility > 0) {
                firstPosition.y -= direction * firstDelta;
            }

            if (second.mobility > 0) {
                secondPosition.y += direction * secondDelta;
            }
        }

        return firstDelta > 0 || secondDelta > 0;
    }

    function enforceParentClearance(itemsById, positions) {
        let moved = false;

        for (const item of itemsById.values()) {
            if (!item.parentId || !positions.has(item.parentId)) {
                continue;
            }

            const parent = itemsById.get(item.parentId);
            if (!parent) {
                continue;
            }

            const parentPosition = positions.get(parent.id);
            const itemPosition = positions.get(item.id);
            const preferredSide = item.preferredSideX || 1;
            const requiredDistance = ((parent.size.width + item.size.width) / 2) + 42;
            const targetX = parentPosition.x + (preferredSide * requiredDistance);

            if (preferredSide > 0 && itemPosition.x < targetX) {
                itemPosition.x = targetX;
                moved = true;
            }
            else if (preferredSide < 0 && itemPosition.x > targetX) {
                itemPosition.x = targetX;
                moved = true;
            }
        }

        return moved;
    }

    function enforceSiblingSpacing(itemsById, positions) {
        const groups = new Map();
        for (const item of itemsById.values()) {
            if (!item.parentId || !positions.has(item.parentId)) {
                continue;
            }

            if (!groups.has(item.parentId)) {
                groups.set(item.parentId, []);
            }

            groups.get(item.parentId).push(item);
        }

        let moved = false;
        for (const siblings of groups.values()) {
            siblings.sort((first, second) => {
                const firstPosition = positions.get(first.id);
                const secondPosition = positions.get(second.id);
                if (Math.abs(firstPosition.y - secondPosition.y) > 0.5) {
                    return firstPosition.y - secondPosition.y;
                }

                return first.base.y - second.base.y;
            });

            for (let index = 1; index < siblings.length; index++) {
                const previous = siblings[index - 1];
                const current = siblings[index];
                const previousPosition = positions.get(previous.id);
                const currentPosition = positions.get(current.id);
                const horizontalGap = Math.abs(currentPosition.x - previousPosition.x);
                const requiredHorizontalGap = ((previous.size.width + current.size.width) / 2) + 24;
                if (horizontalGap >= requiredHorizontalGap) {
                    continue;
                }

                const requiredVerticalGap = ((previous.size.height + current.size.height) / 2) + 28;
                const currentGap = currentPosition.y - previousPosition.y;
                if (currentGap < requiredVerticalGap) {
                    currentPosition.y = previousPosition.y + requiredVerticalGap;
                    moved = true;
                }
            }

            const desiredCenter = siblings.reduce((total, item) => total + item.base.y, 0) / Math.max(1, siblings.length);
            const actualCenter = siblings.reduce((total, item) => total + positions.get(item.id).y, 0) / Math.max(1, siblings.length);
            const shift = clamp(desiredCenter - actualCenter, -44, 44);
            if (Math.abs(shift) <= 0.5) {
                continue;
            }

            for (const item of siblings) {
                if (item.mobility <= 0) {
                    continue;
                }

                positions.get(item.id).y += shift * 0.18 * item.mobility;
                moved = true;
            }
        }

        return moved;
    }

    function relaxTowardBase(items, positions) {
        let moved = false;

        for (const item of items) {
            if (item.mobility <= 0) {
                continue;
            }

            const position = positions.get(item.id);
            const nextX = position.x + ((item.base.x - position.x) * 0.12 * item.mobility);
            const nextY = position.y + ((item.base.y - position.y) * 0.16 * item.mobility);
            if (Math.abs(nextX - position.x) > 0.4 || Math.abs(nextY - position.y) > 0.4) {
                position.x = nextX;
                position.y = nextY;
                moved = true;
            }
        }

        return moved;
    }

    function computeResolvedNodePositions(state, visibleNodes) {
        const items = buildLayoutItems(state, visibleNodes);
        const positions = new Map(items.map(item => [item.id, { x: item.base.x, y: item.base.y }]));
        const itemsById = new Map(items.map(item => [item.id, item]));

        for (let iteration = 0; iteration < 14; iteration++) {
            let moved = false;
            moved = enforceParentClearance(itemsById, positions) || moved;
            moved = enforceSiblingSpacing(itemsById, positions) || moved;

            for (let index = 0; index < items.length; index++) {
                for (let compareIndex = index + 1; compareIndex < items.length; compareIndex++) {
                    const first = items[index];
                    const second = items[compareIndex];
                    const firstPosition = positions.get(first.id);
                    const secondPosition = positions.get(second.id);
                    const overlap = getOverlapDelta(first, second, firstPosition, secondPosition);
                    if (overlap.overlapX <= 0 || overlap.overlapY <= 0) {
                        continue;
                    }

                    const axis = chooseCollisionAxis(first, second, overlap);
                    const direction = resolveCollisionDirection(first, second, axis, overlap);
                    const amount = (axis === "x" ? overlap.overlapX : overlap.overlapY) + 10;
                    moved = applyCollisionSeparation(first, second, firstPosition, secondPosition, axis, amount, direction) || moved;
                }
            }

            moved = relaxTowardBase(items, positions) || moved;
            if (!moved) {
                break;
            }
        }

        enforceParentClearance(itemsById, positions);
        enforceSiblingSpacing(itemsById, positions);
        return positions;
    }

    function ensureLayoutPositions(state, visibleNodes) {
        const nodes = Array.isArray(visibleNodes) ? visibleNodes : getVisibleNodes(state);
        const key = buildResolvedLayoutKey(state, nodes);
        if (state.layoutPositions && state.layoutKey === key) {
            return state.layoutPositions;
        }

        state.layoutPositions = computeResolvedNodePositions(state, nodes);
        state.layoutKey = key;
        return state.layoutPositions;
    }

    function getNodePosition(state, node, visibleNodes) {
        const resolved = ensureLayoutPositions(state, visibleNodes).get(node.id);
        return resolved
            ? { x: resolved.x, y: resolved.y }
            : getBaseNodePosition(state, node);
    }

    function getSceneBounds(state, visibleNodes) {
        const nodes = Array.isArray(visibleNodes) ? visibleNodes : getVisibleNodes(state);
        if (!nodes.length) {
            return null;
        }

        ensureLayoutPositions(state, nodes);

        let minX = Number.POSITIVE_INFINITY;
        let maxX = Number.NEGATIVE_INFINITY;
        let minY = Number.POSITIVE_INFINITY;
        let maxY = Number.NEGATIVE_INFINITY;

        for (const node of nodes) {
            const position = getNodePosition(state, node, nodes);
            const size = getNodeSize(state, node);
            minX = Math.min(minX, position.x - (size.width / 2));
            maxX = Math.max(maxX, position.x + (size.width / 2));
            minY = Math.min(minY, position.y - (size.height / 2));
            maxY = Math.max(maxY, position.y + (size.height / 2));
        }

        return { minX, maxX, minY, maxY };
    }

    function clampPanToScene(state, panX, panY, zoom) {
        const bounds = getSceneBounds(state);
        const rect = state.host.getBoundingClientRect();
        const viewportController = getViewportControllerService();
        if (viewportController?.clampPanToScene) {
            const clamped = viewportController.clampPanToScene({
                bounds,
                hostWidth: rect.width,
                hostHeight: rect.height,
                panX,
                panY,
                zoom: zoom || state.ui.zoom
            });

            return {
                x: round(clamped.panX),
                y: round(clamped.panY)
            };
        }

        if (!bounds || rect.width <= 0 || rect.height <= 0) {
            return { x: panX, y: panY };
        }

        const nextZoom = zoom || state.ui.zoom;
        const marginX = Math.max(160, rect.width * 0.5);
        const marginY = Math.max(140, rect.height * 0.5);
        const contentWidth = (bounds.maxX - bounds.minX) * nextZoom;
        const contentHeight = (bounds.maxY - bounds.minY) * nextZoom;

        let x = panX;
        let y = panY;

        const minPanX = rect.width - marginX - (bounds.maxX * nextZoom);
        const maxPanX = marginX - (bounds.minX * nextZoom);
        const minPanY = rect.height - marginY - (bounds.maxY * nextZoom);
        const maxPanY = marginY - (bounds.minY * nextZoom);

        x = clamp(panX, Math.min(minPanX, maxPanX), Math.max(minPanX, maxPanX));
        y = clamp(panY, Math.min(minPanY, maxPanY), Math.max(minPanY, maxPanY));

        return { x: round(x), y: round(y) };
    }

    function setPan(state, panX, panY, zoom) {
        const clamped = clampPanToScene(state, panX, panY, zoom);
        state.ui.panX = clamped.x;
        state.ui.panY = clamped.y;
    }

    function syncMenuScaleCss(state) {
        if (!state?.host) {
            return;
        }

        state.host.style.setProperty("--cw-menu-scale", `${normalizeMenuActionScale(state.ui?.menuActionScale)}`);
    }

    function serializeState(state) {
        return JSON.stringify({
            version: state.ui.version || "canvas-workbench.v1",
            selectedNodeIds: [...state.selectedIds],
            collapsedNodeIds: [...state.collapsedIds],
            groupFrames: Array.isArray(state.ui.groupFrames) ? state.ui.groupFrames.map(normalizeGroupFrame) : [],
            manualPositions: state.ui.manualPositions || {},
            windowStates: state.ui.windowStates || {},
            zoom: round(state.ui.zoom),
            panX: round(state.ui.panX),
            panY: round(state.ui.panY),
            menuActionScale: normalizeMenuActionScale(state.ui.menuActionScale),
            isMaximized: !!state.ui.isMaximized,
            activeInspectorTab: state.ui.activeInspectorTab || "",
            showDiagnostics: !!state.ui.showDiagnostics,
            showMinimap: state.ui.showMinimap !== false
        });
    }

    function applySceneTransform(state) {
        state.scene.style.transform = `translate(${state.ui.panX}px, ${state.ui.panY}px) scale(${state.ui.zoom})`;
    }

    function cancelViewportAnimation(state) {
        state.animationTimeline?.cancel?.("viewport");
    }

    function updateViewportTransform(state, viewport) {
        state.ui.zoom = viewport.zoom;
        setPan(state, viewport.panX, viewport.panY, viewport.zoom);
        applySceneTransform(state);
    }

    function animateViewportTransition(state, target, options) {
        if (!target) {
            return false;
        }

        const current = {
            panX: state.ui.panX,
            panY: state.ui.panY,
            zoom: state.ui.zoom
        };

        const hasMeaningfulChange = Math.abs(current.panX - target.panX) > 0.5
            || Math.abs(current.panY - target.panY) > 0.5
            || Math.abs(current.zoom - target.zoom) > 0.001;

        if (!hasMeaningfulChange) {
            updateViewportTransform(state, target);
            render(state);
            if (options?.publish !== false) {
                publishState(state);
            }

            return false;
        }

        if (!state.animationTimeline) {
            updateViewportTransform(state, target);
            render(state);
            if (options?.publish !== false) {
                publishState(state);
            }

            return true;
        }

        state.animationTimeline.animateViewport({
            key: options?.key || "viewport",
            from: current,
            to: target,
            durationMs: options?.durationMs ?? 320,
            easing: options?.easing || "softInOut",
            apply(next) {
                updateViewportTransform(state, next);
            },
            complete() {
                render(state);
                if (options?.publish !== false) {
                    publishState(state);
                }
            }
        });

        return true;
    }

    function getLinkAnchorPoint(state, node, side) {
        const position = getNodePosition(state, node);
            const size = getNodeSize(state, node);
        const inset = Math.min(28, size.width * 0.11);
        return {
            x: side === "right"
                ? position.x + (size.width / 2) - inset
                : position.x - (size.width / 2) + inset,
            y: position.y
        };
    }

    function renderLinks(state, visibleNodes) {
        state.links.innerHTML = "";
        ensureLayoutPositions(state, visibleNodes);
        const visible = new Set(visibleNodes.map(node => node.id));

        for (const link of state.surface.links) {
            if (!visible.has(link.sourceId) || !visible.has(link.targetId)) {
                continue;
            }

            const source = state.lookups.byId.get(link.sourceId);
            const target = state.lookups.byId.get(link.targetId);
            if (!source || !target) {
                continue;
            }

            const sourcePosition = getNodePosition(state, source, visibleNodes);
            const targetPosition = getNodePosition(state, target, visibleNodes);
            const sourceSide = targetPosition.x >= sourcePosition.x ? "right" : "left";
            const targetSide = sourceSide === "right" ? "left" : "right";
            const sourceAnchor = getLinkAnchorPoint(state, source, sourceSide);
            const targetAnchor = getLinkAnchorPoint(state, target, targetSide);
            const controlOffset = Math.max(92, Math.abs(targetAnchor.x - sourceAnchor.x) * 0.38);
            const path = createSvgElement(state.document, "path");
            path.setAttribute("d", [
                `M ${sourceAnchor.x} ${sourceAnchor.y}`,
                `C ${sourceAnchor.x + (sourceSide === "right" ? controlOffset : -controlOffset)} ${sourceAnchor.y}`,
                `${targetAnchor.x + (targetSide === "right" ? controlOffset : -controlOffset)} ${targetAnchor.y}`,
                `${targetAnchor.x} ${targetAnchor.y}`
            ].join(" "));
            path.setAttribute("fill", "none");
            path.setAttribute("stroke", link.isUserAuthored ? "rgba(14, 165, 233, 0.78)" : "rgba(100, 116, 139, 0.4)");
            path.setAttribute("stroke-width", link.isUserAuthored ? "3" : "2");
            path.setAttribute("stroke-linecap", "round");
            path.setAttribute("stroke-linejoin", "round");
            path.setAttribute("class", link.isUserAuthored ? "cw-link-path is-flow" : "cw-link-path");
            state.links.appendChild(path);
        }
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

    function renderGroupFrames(state, visibleNodes) {
        if (!state.frameLayer) {
            return;
        }

        state.frameLayer.innerHTML = "";
        state.renderedFrames = new Map();
        ensureLayoutPositions(state, visibleNodes);
        const visibleLookup = new Map(visibleNodes.map(node => [node.id, node]));

        for (const frame of state.ui.groupFrames || []) {
            const memberNodes = getExpandedFrameNodeIds(state, frame)
                .map(nodeId => visibleLookup.get(nodeId))
                .filter(Boolean);
            if (!memberNodes.length) {
                continue;
            }

            let minX = Number.POSITIVE_INFINITY;
            let maxX = Number.NEGATIVE_INFINITY;
            let minY = Number.POSITIVE_INFINITY;
            let maxY = Number.NEGATIVE_INFINITY;

            for (const node of memberNodes) {
                const position = getNodePosition(state, node, visibleNodes);
                const size = getNodeSize(state, node);
                minX = Math.min(minX, position.x - (size.width / 2));
                maxX = Math.max(maxX, position.x + (size.width / 2));
                minY = Math.min(minY, position.y - (size.height / 2));
                maxY = Math.max(maxY, position.y + (size.height / 2));
            }

            const paddingX = 38;
            const paddingY = 42;
            const frameElement = createElement(state.document, "div", `cw-group-frame tone-${frame.tone || "accent"}`);
            frameElement.dataset.frameId = frame.id || "";
            frameElement.style.left = `${round(minX - paddingX)}px`;
            frameElement.style.top = `${round(minY - paddingY)}px`;
            frameElement.style.width = `${round((maxX - minX) + (paddingX * 2))}px`;
            frameElement.style.height = `${round((maxY - minY) + (paddingY * 2))}px`;

            const label = createElement(state.document, "div", "cw-group-frame__label", frame.label || "Group border");
            label.dataset.frameId = frame.id || "";
            label.appendChild(createElement(state.document, "span", "cw-group-frame__count", `${memberNodes.length}`));
            frameElement.appendChild(label);

            for (const edge of ["top", "right", "bottom", "left"]) {
                const handle = createElement(state.document, "div", `cw-group-frame__handle is-${edge}`);
                handle.dataset.frameId = frame.id || "";
                handle.setAttribute("aria-hidden", "true");
                frameElement.appendChild(handle);
            }

            state.frameLayer.appendChild(frameElement);
            state.renderedFrames.set(frame.id || "", {
                frame,
                nodeIds: memberNodes.map(node => node.id)
            });
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

    function createProgressBadge(document, progressMode, progressPercent, extraClassName) {
        const display = resolveProgressDisplay(progressMode, progressPercent);
        const marker = createElement(document, "span", `cw-node__progress is-${display.mode}${extraClassName ? ` ${extraClassName}` : ""}`);
        marker.style.setProperty("--cw-progress-angle", `${display.angle}deg`);
        marker.title = display.title;
        marker.appendChild(createElement(document, "span", "cw-node__progress-center", display.centerText));
        return marker;
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

    function createMarkerBadge(state, node) {
        const glyph = resolveMarkerGlyph(node?.markerIcon);
        if (!glyph) {
            return null;
        }

        const tone = (node?.markerTone || "accent").toLowerCase();
        const badge = createElement(state.document, "span", `cw-node__badge cw-node__marker tone-${tone}`, glyph);
        badge.title = node?.markerLabel || "Marker";
        return badge;
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
            openNodeMetadataMenu(state, node, "progress", progressBadge);
        };
        progressBadge.addEventListener("pointerdown", event => {
            if (event.button !== 0 || event.detail < 2) {
                return;
            }

            openProgressMetadata(event);
        });
        progressBadge.addEventListener("dblclick", openProgressMetadata);
        container.appendChild(progressBadge);
        const markerBadge = createMarkerBadge(state, node);
        if (markerBadge) {
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

        if (node.statusPill || node.progressMode || node.markerIcon || node.priority > 0) {
            const meta = createElement(state.document, "div", "cw-note-node__meta");
            appendNodeIndicators(state, node, meta);
            if (node.statusPill) {
                meta.appendChild(createElement(state.document, "span", "cw-node__chip tone-accent", node.statusPill));
            }
            surface.appendChild(meta);
        }

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

        if (node.leadText) {
            surface.appendChild(applyFullTextTooltip(
                createElement(state.document, "p", "cw-node__lead", node.leadText),
                node.leadText));
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

        if (node.isCollapsible) {
            const collapse = createElement(state.document, "button", "cw-node__collapse", state.collapsedIds.has(node.id) ? "+" : "-");
            collapse.type = "button";
            collapse.dataset.nodeId = node.id;
            collapse.addEventListener("pointerdown", event => event.stopPropagation());
            collapse.addEventListener("pointerup", event => event.stopPropagation());
            collapse.addEventListener("click", event => {
                event.stopPropagation();
                toggleCollapse(state, node.id);
            });
            surface.appendChild(collapse);
        }

        nodeElement.appendChild(surface);
    }

    function renderNodes(state, visibleNodes) {
        state.nodeLayer.innerHTML = "";
        ensureLayoutPositions(state, visibleNodes);

        for (const node of visibleNodes) {
            const position = getNodePosition(state, node, visibleNodes);
            const nodeElement = createElement(state.document, "div", "cw-node");
            nodeElement.dataset.nodeId = node.id;
            nodeElement.dataset.family = node.family || "item";
            nodeElement.dataset.palette = node.paletteKey || "neutral";
            nodeElement.style.left = `${position.x}px`;
            nodeElement.style.top = `${position.y}px`;
            nodeElement.style.setProperty("--cw-node-accent", node.accentColor || "#7c3aed");
            if (state.selectedIds.has(node.id)) {
                nodeElement.classList.add("is-selected");
            }

            nodeElement.addEventListener("pointerenter", () => updateConnectorAnchorHover(state, node.id));
            nodeElement.addEventListener("pointerleave", () => updateConnectorAnchorHover(state, null));

            if (state.collapsedIds.has(node.id)) {
                nodeElement.classList.add("is-collapsed");
            }

            if (node.isInlineTextNode) {
                renderInlineTextNode(state, node, nodeElement);
            }
            else {
                renderStandardNode(state, node, nodeElement);
            }

            state.nodeLayer.appendChild(nodeElement);
        }
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
    }

    function showPopover(state, anchorElement, annotation) {
        if (!state?.popover || !anchorElement || !annotation) {
            return;
        }

        if (state.surface?.chrome?.tooltipPopover?.isEnabled === false) {
            return;
        }

        state.popover.dataset.kind = annotation.kind || "info";
        state.popover.dataset.tone = annotation.tone || "accent";
        state.popoverTitle.textContent = annotation.label || annotation.kind || "Signal";
        state.popoverBody.textContent = annotation.description || annotation.label || "Shared workbench signal";
        state.popover.style.display = "grid";
        state.popoverAnchor = anchorElement;
        positionFloatingOverlayWithinHost(state, state.popover, anchorElement.getBoundingClientRect());
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
                badge.addEventListener("pointerenter", () => showPopover(state, badge, annotation));
                badge.addEventListener("pointerleave", () => hidePopover(state));
                if (tooltipPopover.focusTriggers !== false) {
                    badge.addEventListener("focus", () => showPopover(state, badge, annotation));
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
        renderConnectorAnchorOverlay(state, getVisibleNodes(state));
    }

    function getConnectorAnchorPoints(state, node, placementMode) {
        const position = getNodePosition(state, node);
        const size = getNodeSize(state, node);
        const horizontalInset = Math.min(28, size.width * 0.11);
        const verticalInset = Math.min(22, size.height * 0.18);
        const points = [
            { side: "left", x: position.x - (size.width / 2) + horizontalInset, y: position.y },
            { side: "right", x: position.x + (size.width / 2) - horizontalInset, y: position.y }
        ];

        if ((placementMode || "edges") !== "horizontal") {
            points.push(
                { side: "top", x: position.x, y: position.y - (size.height / 2) + verticalInset },
                { side: "bottom", x: position.x, y: position.y + (size.height / 2) - verticalInset });
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

    function renderConnectorAnchorOverlay(state, visibleNodes) {
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
            const position = getNodePosition(state, node, visibleNodes);
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

    function renderTransformHandlesOverlay(state, visibleNodes) {
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
            for (const node of visibleNodes) {
                const position = getNodePosition(state, node, visibleNodes);
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

                const sourcePosition = getNodePosition(state, source, visibleNodes);
                const targetPosition = getNodePosition(state, target, visibleNodes);
                const sourceSide = targetPosition.x >= sourcePosition.x ? "right" : "left";
                const targetSide = sourceSide === "right" ? "left" : "right";
                for (const point of [getLinkAnchorPoint(state, source, sourceSide), getLinkAnchorPoint(state, target, targetSide)]) {
                    const dot = createElement(state.document, "div", "cw-debug-anchor");
                    dot.style.left = `${round(point.x)}px`;
                    dot.style.top = `${round(point.y)}px`;
                    state.debugLayer.appendChild(dot);
                }
            }
        }
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
        state.diagnosticsBody.innerHTML = "";
        const rows = [
            ["Nodes", `${visibleNodes.length}/${state.surface.nodes.length}`],
            ["Links", `${state.surface.links.length}`],
            ["Selected", `${state.selectedIds.size}`],
            ["Interaction", state.interaction?.kind || "idle"],
            ["Zoom", `${Math.round((state.ui.zoom || 1) * 100)}%`],
            ["Pan", `${round(state.ui.panX)}, ${round(state.ui.panY)}`]
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

    function renderMinimap(state, visibleNodes) {
        if (!state?.minimapShell || !state?.minimapCanvas) {
            return;
        }

        const minimap = state.surface.chrome.minimap || {};
        const enabled = minimap.isEnabled && state.ui.showMinimap !== false && visibleNodes.length > 0;
        state.minimapShell.style.display = enabled ? "grid" : "none";
        if (!enabled) {
            state.minimapMetrics = null;
            return;
        }

        const svg = state.minimapCanvas;
        svg.innerHTML = "";
        state.minimapTitle.textContent = minimap.title || "Scene overview";

        const bounds = getSceneBounds(state) || { minX: 0, maxX: 320, minY: 0, maxY: 240 };
        const width = 200;
        const height = 128;
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
            bounds
        };

        const backdrop = createSvgElement(state.document, "rect", "cw-minimap__backdrop");
        backdrop.setAttribute("x", "0");
        backdrop.setAttribute("y", "0");
        backdrop.setAttribute("width", `${width}`);
        backdrop.setAttribute("height", `${height}`);
        svg.appendChild(backdrop);

        for (const node of visibleNodes) {
            const position = getNodePosition(state, node, visibleNodes);
            const size = getNodeSize(state, node);
            const rect = createSvgElement(state.document, "rect", "cw-minimap__node");
            rect.setAttribute("x", `${round(offsetX + ((position.x - (size.width / 2) - bounds.minX) * scale))}`);
            rect.setAttribute("y", `${round(offsetY + ((position.y - (size.height / 2) - bounds.minY) * scale))}`);
            rect.setAttribute("width", `${Math.max(4, round(size.width * scale))}`);
            rect.setAttribute("height", `${Math.max(4, round(size.height * scale))}`);
            rect.setAttribute("rx", node.family === "root" ? "5" : "3");
            rect.setAttribute("data-selected", state.selectedIds.has(node.id) ? "true" : "false");
            svg.appendChild(rect);
        }

        const hostRect = state.host.getBoundingClientRect();
        const viewport = createSvgElement(state.document, "rect", "cw-minimap__viewport");
        viewport.setAttribute("x", `${round(offsetX + ((((0 - state.ui.panX) / state.ui.zoom) - bounds.minX) * scale))}`);
        viewport.setAttribute("y", `${round(offsetY + ((((0 - state.ui.panY) / state.ui.zoom) - bounds.minY) * scale))}`);
        viewport.setAttribute("width", `${Math.max(12, round((hostRect.width / state.ui.zoom) * scale))}`);
        viewport.setAttribute("height", `${Math.max(12, round((hostRect.height / state.ui.zoom) * scale))}`);
        svg.appendChild(viewport);
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
        render(state);
        publishState(state);
    }

    async function writeClipboardText(payload) {
        if (!navigator?.clipboard?.writeText || !payload) {
            return;
        }

        try {
            await navigator.clipboard.writeText(payload);
        }
        catch {
        }
    }

    async function readClipboardText() {
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

    function buildClipboardPayload(state) {
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

        const payload = JSON.stringify(buildClipboardPayload(state));
        state.localClipboard = payload;
        void writeClipboardText(payload);
        state.dotNetRef.invokeMethodAsync("OnClipboardAction", "copy", payload);
        showStatusNotice(state, `Copied ${state.selectedIds.size} node(s)`, "accent");
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

        state.dotNetRef.invokeMethodAsync("OnClipboardAction", "duplicate", JSON.stringify(buildClipboardPayload(state)));
        showStatusNotice(state, "Duplicate request sent to the workspace", "accent");
    }

    function toggleMinimap(state) {
        state.ui.showMinimap = state.ui.showMinimap === false;
        render(state);
        publishState(state);
    }

    function toggleDiagnostics(state) {
        state.ui.showDiagnostics = !state.ui.showDiagnostics;
        hidePopover(state);
        render(state);
        publishState(state);
    }

    function invalidateMeasuredLayout(state) {
        state.layoutPositions = null;
        state.layoutKey = "";
    }

    function measureRenderedNodeSizes(state) {
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

    function scheduleNodeMeasurement(state) {
        if (state.measureLayoutFrame) {
            return;
        }

        state.measureLayoutFrame = window.requestAnimationFrame(() => {
            state.measureLayoutFrame = 0;
            if (measureRenderedNodeSizes(state)) {
                render(state);
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

    function hitTestNode(state, target) {
        const nodeElement = target?.closest?.(".cw-node");
        if (!nodeElement) {
            return null;
        }

        return state.lookups.byId.get(nodeElement.dataset.nodeId) || null;
    }

    function hitTestFrameHandle(target) {
        const handle = target?.closest?.(".cw-group-frame__handle, .cw-group-frame__label");
        if (!handle) {
            return null;
        }

        return handle.dataset.frameId || null;
    }

    function hitTestProgressBadge(target) {
        return target?.closest?.(".cw-node__progress") || null;
    }

    function isOverlayTarget(target) {
        return !!target?.closest?.(".cw-context-menu, .cw-canvas-composer, .cw-workbench__popover, .cw-minimap, .cw-status-notice");
    }

    function applyFullTextTooltip(element, text) {
        const fullText = typeof text === "string" ? text.trim() : "";
        if (element && fullText) {
            element.dataset.fullText = fullText;
            element.title = fullText;
        }

        return element;
    }

    function reconcileSelection(state) {
        const normalized = selectionModel.removeMissing(
            state.ui.selectedNodeIds,
            state.surface.nodes.map(node => node.id),
            state.ui.selectedNodeIds[0] || null);

        state.ui.selectedNodeIds = normalized.selectedNodeIds;
        state.selectedIds = toSelectionSet(normalized.selectedNodeIds);
        return normalized;
    }

    function applySelection(state, selectedNodeIds, primaryNodeId, options) {
        const normalized = selectionModel.replace(selectedNodeIds, primaryNodeId);
        state.ui.selectedNodeIds = normalized.selectedNodeIds;
        state.selectedIds = toSelectionSet(normalized.selectedNodeIds);

        if (options?.render !== false) {
            render(state);
        }

        if (options?.publish !== false) {
            publishSelection(state);
            publishState(state);
        }

        return normalized;
    }

    function selectSingleNode(state, nodeId, options) {
        const normalized = selectionModel.selectOne(nodeId);
        return applySelection(state, normalized.selectedNodeIds, normalized.primaryNodeId, options);
    }

    function publishSelection(state) {
        const normalized = selectionModel.normalize(state.ui.selectedNodeIds, state.ui.selectedNodeIds[0] || null);
        state.ui.selectedNodeIds = normalized.selectedNodeIds;
        state.selectedIds = toSelectionSet(normalized.selectedNodeIds);
        state.dotNetRef.invokeMethodAsync("OnSelectionChanged", normalized.primaryNodeId, JSON.stringify(normalized.selectedNodeIds));
    }

    function publishState(state) {
        state.publishStateDebounced(serializeState(state));
    }

    function publishNodesMoved(state, movedIds) {
        const payload = movedIds.map(nodeId => {
            const position = state.ui.manualPositions[nodeId] || { x: 0, y: 0 };
            return {
                nodeId,
                x: round(position.x),
                y: round(position.y)
            };
        });

        state.dotNetRef.invokeMethodAsync("OnNodesMoved", JSON.stringify(payload));
    }

    function setSelection(state, nodeIds, keepOrderPrimary) {
        const primaryNodeId = keepOrderPrimary && Array.isArray(nodeIds) ? nodeIds[0] || null : null;
        applySelection(state, nodeIds, primaryNodeId);
    }

    function toggleSelection(state, nodeId) {
        const normalized = selectionModel.toggle(state.ui.selectedNodeIds, nodeId, state.ui.selectedNodeIds[0] || null);
        applySelection(state, normalized.selectedNodeIds, normalized.primaryNodeId);
    }

    function toggleCollapse(state, nodeId) {
        if (state.collapsedIds.has(nodeId)) {
            state.collapsedIds.delete(nodeId);
        }
        else {
            state.collapsedIds.add(nodeId);
        }

        state.ui.collapsedNodeIds = [...state.collapsedIds];
        render(state);
        publishState(state);
    }

    function clearContextMenu(state) {
        state.contextMenu.innerHTML = "";
        state.contextMenu.style.display = "none";
        state.contextMenuState = null;
    }

    function closeComposer(state, options) {
        const focusHost = options?.focusHost !== false;
        if (!state.composer?.element) {
            return;
        }

        state.composer.element.remove();
        state.composer = null;
        if (focusHost) {
            deferHostFocus(state);
        }
    }

    function ensureHostFocus(state) {
        try {
            state.host.focus({ preventScroll: true });
        }
        catch {
            state.host.focus();
        }
    }

    function deferHostFocus(state) {
        window.requestAnimationFrame(() => ensureHostFocus(state));
    }

    function resolveComposerAnchor(state) {
        if (!state.composer) {
            return null;
        }

        if (state.composer.nodeId) {
            const node = state.lookups.byId.get(state.composer.nodeId);
            if (node) {
                return worldToHostPoint(state, getNodePosition(state, node));
            }
        }

        if (state.composer.anchorWorld) {
            return worldToHostPoint(state, state.composer.anchorWorld);
        }

        return state.composer.anchorHost || null;
    }

    function layoutComposer(state) {
        if (!state.composer?.element) {
            return;
        }

        const anchor = resolveComposerAnchor(state);
        if (!anchor) {
            return;
        }

        const element = state.composer.element;
        const hostRect = state.host.getBoundingClientRect();
        const composerRect = element.getBoundingClientRect();
        const margin = 18;
        let left = anchor.x - (composerRect.width / 2);
        let top = anchor.y + 24;

        if (state.composer.kind === "note-create" || state.composer.kind === "note-edit") {
            top = anchor.y - (composerRect.height / 2);
        }

        left = clamp(left, margin, Math.max(margin, hostRect.width - composerRect.width - margin));
        top = clamp(top, margin, Math.max(margin, hostRect.height - composerRect.height - margin));
        element.style.left = `${round(left)}px`;
        element.style.top = `${round(top)}px`;
    }

    function render(state) {
        const visibleNodes = getVisibleNodes(state);
        ensureLayoutPositions(state, visibleNodes);
        applySceneTransform(state);
        renderGroupFrames(state, visibleNodes);
        renderLinks(state, visibleNodes);
        renderSnapGuides(state);
        renderNodes(state, visibleNodes);
        renderConnectorAnchorOverlay(state, visibleNodes);
        renderTransformHandlesOverlay(state, visibleNodes);
        renderEmptyStateOverlay(state, visibleNodes);
        renderDebugDecorations(state, visibleNodes);
        renderDiagnosticsOverlay(state, visibleNodes);
        renderMinimap(state, visibleNodes);
        layoutComposer(state);
        scheduleNodeMeasurement(state);
    }

    function getContextActions(state, node) {
        if (node && state.selectedIds.size > 1 && state.selectedIds.has(node.id)) {
            return state.surface.chrome.groupContextActions || [];
        }

        if (node) {
            return node.contextActions || [];
        }

        return state.surface.chrome.quickCreateActions || [];
    }

    function isCreateAction(action) {
        if (action?.children?.length) {
            return false;
        }

        return !!action?.requiresInput ||
            (action?.createMode && action.createMode !== "command") ||
            (action?.actionId || "").startsWith("add-");
    }

    function buildCreateRequest(state, action, sourceNode, worldPoint, placementKind) {
        const point = worldPoint || (sourceNode ? getNodePosition(state, sourceNode) : { x: 0, y: 0 });
        return {
            actionId: action.actionId,
            sourceNodeId: sourceNode?.id || null,
            parentNodeId: sourceNode?.id || null,
            x: round(point.x),
            y: round(point.y),
            title: "",
            subtitle: "",
            notes: "",
            objectSubtype: action.objectSubtype || "",
            uploadedFile: null,
            inputValues: Array.isArray(action?.defaultInputValues)
                ? action.defaultInputValues.map(item => ({ key: item.key || "", value: item.value || "" }))
                : [],
            placementKind: placementKind || (sourceNode ? "child" : "canvas"),
            createMode: action.createMode || (action.requiresInput ? "dialog" : "command")
        };
    }

    function resolveMenuLabel(action) {
        if (action?.menuLabel) {
            return action.menuLabel;
        }

        const label = action?.label || action?.actionId || "Item";
        const parts = label.split(/\s+/).filter(Boolean);
        return parts[0] || label;
    }

    function getMenuScale(state) {
        return normalizeMenuActionScale(state?.ui?.menuActionScale);
    }

    function resolveMenuActionVariant(action) {
        const actionId = (action?.actionId || "").toLowerCase();
        if (actionId.startsWith("progress:")) {
            return "progress-preset";
        }

        if (actionId.startsWith("marker:")) {
            return "marker-preset";
        }

        if (actionId.startsWith("priority:")) {
            return "priority-preset";
        }

        return (action?.menuSize || "").toLowerCase() === "compact"
            ? "compact"
            : "normal";
    }

    function getActionMetrics(state, action) {
        const scale = getMenuScale(state);
        switch (resolveMenuActionVariant(action)) {
            case "progress-preset":
            case "marker-preset":
                return { halfWidth: round(34 * scale), halfHeight: round(29 * scale) };
            case "priority-preset":
            case "compact":
                return { halfWidth: round(29 * scale), halfHeight: round(25 * scale) };
            default:
                return { halfWidth: round(46 * scale), halfHeight: round(40 * scale) };
        }
    }

    function applyProgressPresetTone(button, action) {
        const token = (action?.actionId || "").substring("progress:".length).toLowerCase();
        const percent = Number.parseInt(token, 10);
        let depth = 236;
        if (token === "na") {
            depth = 244;
        }
        else if (token === "started") {
            depth = 222;
        }
        else if (Number.isFinite(percent)) {
            depth = clamp(242 - Math.round(percent * 1.08), 126, 242);
        }

        const nextDepth = clamp(depth - 18, 96, 224);
        button.style.background = `linear-gradient(180deg, rgb(${depth} ${depth} ${depth}), rgb(${nextDepth} ${nextDepth} ${nextDepth}))`;
        button.style.color = nextDepth <= 142 ? "#f8fafc" : "#0f172a";
    }

    function fitContextMenuLabel(button, label, variant) {
        if (!button || !label) {
            return;
        }

        window.requestAnimationFrame(() => {
            const maxWidthRatio = variant === "normal" ? 0.82 : variant === "compact" ? 0.74 : 0.78;
            const maxHeightRatio = variant === "normal" ? 0.33 : variant === "compact" ? 0.26 : 0.29;
            const maxWidth = Math.max(20, button.clientWidth * maxWidthRatio);
            const minFontSize = variant === "normal" ? 9.25 : variant === "compact" ? 7.4 : 7.7;
            const maxHeight = Math.max(14, button.clientHeight * maxHeightRatio);
            label.style.maxWidth = `${round(maxWidth)}px`;

            const measureService = getTextMeasureService();
            const initialFontSize = parseFloat(window.getComputedStyle(label).fontSize) || (variant === "normal" ? 11.5 : 8.4);
            if (measureService && typeof measureService.fitElementText === "function") {
                measureService.fitElementText(label, {
                    text: label.dataset.fullText || label.textContent || "",
                    maxWidth,
                    maxHeight,
                    maxLines: 2,
                    minFontSize,
                    initialFontSize,
                    truncationMode: "ellipsis"
                });
                return;
            }

            let fontSize = initialFontSize;
            while (fontSize > minFontSize &&
                (label.scrollWidth > (maxWidth + 0.5) || label.scrollHeight > (maxHeight + 0.5))) {
                fontSize -= 0.25;
                label.style.fontSize = `${fontSize}px`;
            }
        });
    }

    function resolveActionGlyph(icon) {
        switch ((icon || "").toLowerCase()) {
            case "open":
                return "\u2197";
            case "link":
            case "plug":
                return "\u21C4";
            case "qa":
            case "use":
                return "\u2713";
            case "test":
                return "\u2697";
            case "fork":
                return "\u2442";
            case "skip":
                return "\u00BB";
            case "note":
                return "\u270E";
            case "choice":
                return "\u25C6";
            case "phase":
                return "\u25ED";
            case "date":
                return "\u25F7";
            case "feature":
                return "\u25C8";
            case "arch":
                return "\u25A3";
            case "build":
                return "\u2B22";
            case "rev":
                return "\u21BA";
            case "prompt":
                return "\u2736";
            case "research":
                return "\u2315";
            case "money":
                return "$";
            case "market":
                return "\u25CE";
            case "ops":
                return "\u2699";
            case "ship":
                return "\u21E2";
            case "risk":
                return "\u26A0";
            case "audit":
                return "\u2714";
            case "support":
                return "\u2630";
            case "flow":
                return "\u27F6";
            case "session":
                return "\u25C9";
            case "step":
                return "\u2192";
            case "repo":
                return "\u2318";
            case "file":
                return "\u25A4";
            case "image":
                return "\u25A7";
            case "video":
                return "\u25B6";
            case "shield":
                return "\u26E8";
            case "evidence":
                return "\u25C9";
            case "frame":
                return "\u25AD";
            case "clear":
                return "\u00D7";
            case "progress":
                return "\u25D4";
            case "marker":
                return "\u2736";
            case "priority":
                return "#";
            default:
                return (icon || "").slice(0, 1).toUpperCase() || "\u25CF";
        }
    }

    function createMenuActionIcon(state, action) {
        const iconKey = (action?.icon || "").toLowerCase();
        const iconContainer = createElement(state.document, "span", "cw-context-menu__icon");

        if (iconKey.startsWith("progress-")) {
            const token = iconKey.substring("progress-".length);
            const progressMode = token === "na" ? "na" : token === "started" ? "started" : (Number(token) >= 100 ? "complete" : "progress");
            const progressPercent = Number.isFinite(Number(token)) ? Number(token) : 0;
            iconContainer.appendChild(createProgressBadge(state.document, progressMode, progressPercent, "cw-node__progress--menu"));
            return iconContainer;
        }

        if (iconKey.startsWith("marker-")) {
            const markerIcon = iconKey.substring("marker-".length);
            const markerBadge = createElement(state.document, "span", `cw-node__badge cw-node__marker tone-${(action?.tone || "accent").toLowerCase()} cw-node__badge--menu`, resolveMarkerGlyph(markerIcon) || "\u2736");
            iconContainer.appendChild(markerBadge);
            return iconContainer;
        }

        if (iconKey.startsWith("priority-")) {
            const priority = clamp(Math.round(Number(iconKey.substring("priority-".length)) || 0), 0, 6);
            const priorityBadge = createElement(state.document, "span", `cw-node__badge cw-node__priority is-level-${priority} cw-node__badge--menu`, `${priority}`);
            iconContainer.appendChild(priorityBadge);
            return iconContainer;
        }

        iconContainer.appendChild(createElement(state.document, "span", "cw-context-menu__glyph", resolveActionGlyph(iconKey)));
        return iconContainer;
    }

    function resolveMenuActionAriaLabel(action) {
        return action?.label || action?.menuLabel || action?.actionId || "Canvas action";
    }

    function getRadialOffsets(count, baseRadius, ringStep) {
        if (count <= 0) {
            return [];
        }

        if (count === 1) {
            return [{ x: 0, y: 0 }];
        }

        const offsets = [];
        let remaining = count;
        let ringIndex = 0;
        const radiusStart = typeof baseRadius === "number" ? baseRadius : 84;
        const radiusStep = typeof ringStep === "number" ? ringStep : 62;

        while (remaining > 0) {
            const ringCapacity = ringIndex === 0
                ? Math.min(remaining, 6)
                : Math.min(remaining, 12 + ((ringIndex - 1) * 6));
            const radius = radiusStart + (ringIndex * radiusStep);
            const startAngle = ringIndex % 2 === 0 ? -90 : -75;

            for (let index = 0; index < ringCapacity; index++) {
                const angle = ((startAngle + ((360 / ringCapacity) * index)) * Math.PI) / 180;
                offsets.push({
                    x: Math.cos(angle) * radius,
                    y: Math.sin(angle) * radius
                });
            }

            remaining -= ringCapacity;
            ringIndex += 1;
        }

        return offsets;
    }

    function getCompactRingOffsets(count, radius, startAngle) {
        if (count <= 0) {
            return [];
        }

        const offsets = [];
        const resolvedRadius = typeof radius === "number" ? radius : 72;
        const resolvedStartAngle = typeof startAngle === "number" ? startAngle : -90;
        for (let index = 0; index < count; index++) {
            const angle = ((resolvedStartAngle + ((360 / count) * index)) * Math.PI) / 180;
            offsets.push({
                x: Math.cos(angle) * resolvedRadius,
                y: Math.sin(angle) * resolvedRadius
            });
        }

        return offsets;
    }

    function resolveContextMenuOffsets(actions, baseRadius, ringStep, layout) {
        if ((layout || "").toLowerCase() === "compact-ring") {
            return getCompactRingOffsets(actions.length, typeof baseRadius === "number" ? baseRadius : 72, -90);
        }

        return getRadialOffsets(actions.length, baseRadius, ringStep);
    }

    function getContextMenuLayerBounds(state, originOffset, offsets, radius, actions) {
        const coreHalf = 38;
        const coreLabelAllowance = 30;
        const padding = 16;
        const bounds = {
            minX: originOffset.x - radius,
            maxX: originOffset.x + radius,
            minY: originOffset.y - radius,
            maxY: originOffset.y + radius
        };

        bounds.minX = Math.min(bounds.minX, originOffset.x - coreHalf);
        bounds.maxX = Math.max(bounds.maxX, originOffset.x + coreHalf);
        bounds.minY = Math.min(bounds.minY, originOffset.y - coreHalf);
        bounds.maxY = Math.max(bounds.maxY, originOffset.y + coreHalf + coreLabelAllowance);

        for (let index = 0; index < (offsets || []).length; index++) {
            const offset = offsets[index];
            const metrics = getActionMetrics(state, actions?.[index]);
            const centerX = originOffset.x + offset.x;
            const centerY = originOffset.y + offset.y;
            bounds.minX = Math.min(bounds.minX, centerX - metrics.halfWidth);
            bounds.maxX = Math.max(bounds.maxX, centerX + metrics.halfWidth);
            bounds.minY = Math.min(bounds.minY, centerY - metrics.halfHeight);
            bounds.maxY = Math.max(bounds.maxY, centerY + metrics.halfHeight);
        }

        bounds.minX -= padding;
        bounds.maxX += padding;
        bounds.minY -= padding;
        bounds.maxY += padding;
        return bounds;
    }

    function clampLayerBoundsToHost(state, bounds) {
        const rootCenter = state.contextMenuState?.rootCenter;
        if (!rootCenter) {
            return { x: 0, y: 0 };
        }

        const hostRect = state.host.getBoundingClientRect();
        const visibleMinX = -rootCenter.x;
        const visibleMaxX = hostRect.width - rootCenter.x;
        const visibleMinY = -rootCenter.y;
        const visibleMaxY = hostRect.height - rootCenter.y;
        let shiftX = 0;
        let shiftY = 0;

        if (bounds.minX < visibleMinX) {
            shiftX += visibleMinX - bounds.minX;
        }

        if (bounds.maxX > visibleMaxX) {
            shiftX -= bounds.maxX - visibleMaxX;
        }

        if (bounds.minY < visibleMinY) {
            shiftY += visibleMinY - bounds.minY;
        }

        if (bounds.maxY > visibleMaxY) {
            shiftY -= bounds.maxY - visibleMaxY;
        }

        return {
            x: round(shiftX),
            y: round(shiftY)
        };
    }

    function positionContextMenu(state, center, offsets, actions) {
        const hostRect = state.host.getBoundingClientRect();
        const radius = getContextMenuOrbitRadius(state, offsets || [], actions || []);
        const bounds = getContextMenuLayerBounds(state, { x: 0, y: 0 }, offsets || [], radius, actions || []);
        const x = round(clamp(center.x, -bounds.minX, Math.max(-bounds.minX, hostRect.width - bounds.maxX)));
        const y = round(clamp(center.y, -bounds.minY, Math.max(-bounds.minY, hostRect.height - bounds.maxY)));
        state.contextMenu.style.left = `${x}px`;
        state.contextMenu.style.top = `${y}px`;
        return { x, y };
    }

    function getContextMenuOrbitRadius(state, offsets, actions) {
        let radius = 76 * getMenuScale(state);

        for (let index = 0; index < (offsets || []).length; index++) {
            const offset = offsets[index];
            const metrics = getActionMetrics(state, actions?.[index]);
            radius = Math.max(radius, Math.hypot(offset.x, offset.y) + Math.max(metrics.halfWidth, metrics.halfHeight) + 12);
        }

        return radius;
    }

    function getContextMenuLocalPoint(state, clientX, clientY) {
        const rootCenter = state.contextMenuState?.rootCenter;
        if (!rootCenter) {
            return null;
        }

        const hostPoint = getHostPoint(state, clientX, clientY);
        return {
            x: hostPoint.x - rootCenter.x,
            y: hostPoint.y - rootCenter.y
        };
    }

    function isPointInContextMenuLayer(layerState, localPoint) {
        if (!layerState || !localPoint) {
            return false;
        }

        if (layerState.mode === "panel" && layerState.bounds) {
            return localPoint.x >= (layerState.bounds.minX - 12) &&
                localPoint.x <= (layerState.bounds.maxX + 12) &&
                localPoint.y >= (layerState.bounds.minY - 12) &&
                localPoint.y <= (layerState.bounds.maxY + 12);
        }

        const dx = localPoint.x - layerState.originOffset.x;
        const dy = localPoint.y - layerState.originOffset.y;
        return Math.hypot(dx, dy) <= (layerState.radius + 18);
    }

    function closeContextMenuLayersFrom(state, depth) {
        const layers = state.contextMenuState?.layers;
        if (!layers?.length || depth >= layers.length) {
            return;
        }

        for (let index = layers.length - 1; index >= depth; index--) {
            layers[index].element.remove();
        }

        state.contextMenuState.layers = layers.slice(0, depth);
    }

    function syncContextMenuLayers(state, event) {
        const layers = state.contextMenuState?.layers;
        if (!layers?.length) {
            return;
        }

        const hoveredAction = event.target?.closest?.(".cw-context-menu__action");
        if (hoveredAction && state.contextMenu?.contains(hoveredAction)) {
            const depth = Number.parseInt(hoveredAction.dataset.layerDepth || "0", 10) || 0;
            const layer = layers[depth];
            const entry = layer?.actionEntries?.get(hoveredAction.dataset.actionId || "");
            if (entry?.action?.children?.length) {
                openContextSubmenu(state, layer, entry.options, entry.action, entry.offset);
                return;
            }

            closeContextMenuLayersFrom(state, depth + 1);
            return;
        }

        if (layers.length < 2) {
            return;
        }

        const localPoint = getContextMenuLocalPoint(state, event.clientX, event.clientY);
        if (!localPoint) {
            return;
        }

        let deepestContainingLayer = 0;
        for (let index = 0; index < layers.length; index++) {
            if (isPointInContextMenuLayer(layers[index], localPoint)) {
                deepestContainingLayer = index;
            }
        }

        closeContextMenuLayersFrom(state, deepestContainingLayer + 1);
    }

    function resolveSubmenuOrigin(parentLayer, offset, layout) {
        const length = Math.hypot(offset.x, offset.y) || 1;
        const outwardDistance = (layout || "").toLowerCase() === "compact-ring"
            ? Math.max(92, round(parentLayer.radius * 0.26))
            : Math.max(108, round(parentLayer.radius * 0.34));
        return {
            x: round(parentLayer.originOffset.x + offset.x + ((offset.x / length) * outwardDistance)),
            y: round(parentLayer.originOffset.y + offset.y + ((offset.y / length) * outwardDistance))
        };
    }

    function clampLayerOriginToHost(state, originOffset, offsets, radius, actions) {
        const bounds = getContextMenuLayerBounds(state, originOffset, offsets || [], radius, actions || []);
        const shift = clampLayerBoundsToHost(state, bounds);
        return {
            x: round(originOffset.x + shift.x),
            y: round(originOffset.y + shift.y)
        };
    }

    function getToolboxPanelSize() {
        return { width: 452, height: 492 };
    }

    function getToolboxPanelBounds(originOffset, panelSize) {
        return {
            minX: originOffset.x,
            maxX: originOffset.x + panelSize.width,
            minY: originOffset.y,
            maxY: originOffset.y + panelSize.height
        };
    }

    function clampToolboxPanelOriginToHost(state, originOffset, panelSize) {
        const bounds = getToolboxPanelBounds(originOffset, panelSize);
        const shift = clampLayerBoundsToHost(state, bounds);
        return {
            x: round(originOffset.x + shift.x),
            y: round(originOffset.y + shift.y)
        };
    }

    function resolveToolboxPanelOrigin(parentLayer, offset, panelSize) {
        const openRight = offset.x >= 0;
        const anchorX = parentLayer.mode === "panel"
            ? (openRight ? parentLayer.bounds.maxX : parentLayer.bounds.minX)
            : (parentLayer.originOffset.x + offset.x);
        const anchorY = parentLayer.mode === "panel"
            ? parentLayer.originOffset.y + 18
            : (parentLayer.originOffset.y + offset.y - (panelSize.height * 0.34));
        return {
            x: round(anchorX + (openRight ? 26 : -(panelSize.width + 26))),
            y: round(anchorY)
        };
    }

    function createContextMenuLayer(state, options) {
        if ((options.mode || "") === "panel") {
            const panelSize = options.panelSize || getToolboxPanelSize();
            const layer = createElement(state.document, "div", `cw-context-menu__layer cw-context-menu__layer--panel ${options.depth > 0 ? "is-submenu" : "is-root"}`);
            layer.style.zIndex = `${options.depth + 1}`;

            const panel = createElement(state.document, "div", "cw-context-toolbox");
            panel.style.left = `${options.originOffset.x}px`;
            panel.style.top = `${options.originOffset.y}px`;
            panel.style.setProperty("--cw-toolbox-width", `${panelSize.width}px`);
            panel.style.setProperty("--cw-toolbox-height", `${panelSize.height}px`);
            panel.addEventListener("pointerdown", event => event.stopPropagation());
            layer.appendChild(panel);

            return {
                depth: options.depth,
                element: layer,
                panel,
                mode: "panel",
                originOffset: options.originOffset,
                bounds: getToolboxPanelBounds(options.originOffset, panelSize),
                radius: 0,
                ownerActionId: options.ownerActionId || "",
                ownerDepth: typeof options.ownerDepth === "number" ? options.ownerDepth : -1,
                actionEntries: new Map()
            };
        }

        const layer = createElement(state.document, "div", `cw-context-menu__layer ${options.depth > 0 ? "is-submenu" : "is-root"}`);
        layer.style.zIndex = `${options.depth + 1}`;

        const backdrop = createElement(state.document, "div", `cw-context-menu__backdrop ${options.depth > 0 ? "is-submenu" : "is-root"}`);
        backdrop.style.setProperty("--cw-orbit-x", `${options.originOffset.x}px`);
        backdrop.style.setProperty("--cw-orbit-y", `${options.originOffset.y}px`);
        layer.appendChild(backdrop);

        const orbit = createElement(state.document, "div", `cw-context-menu__orbit ${options.depth > 0 ? "is-submenu" : "is-root"}`);
        orbit.style.setProperty("--cw-orbit-x", `${options.originOffset.x}px`);
        orbit.style.setProperty("--cw-orbit-y", `${options.originOffset.y}px`);
        orbit.addEventListener("pointerdown", event => event.stopPropagation());

        const core = createElement(state.document, "div", `cw-context-menu__core ${options.depth > 0 ? "is-submenu" : "is-root"}`);
        core.appendChild(createElement(state.document, "span", "cw-context-menu__core-dot"));
        core.appendChild(createElement(state.document, "span", "cw-context-menu__core-label", options.label || "Canvas"));
        orbit.appendChild(core);
        layer.appendChild(orbit);

        return {
            depth: options.depth,
            element: layer,
            backdrop,
            orbit,
            mode: "orbit",
            originOffset: options.originOffset,
            radius: 0,
            ownerActionId: options.ownerActionId || "",
            ownerDepth: typeof options.ownerDepth === "number" ? options.ownerDepth : -1,
            actionEntries: new Map()
        };
    }

    function resolveQuickCreateSourceNode(state) {
        const selectedId = state.ui.selectedNodeIds[0];
        if (selectedId && state.lookups.byId.has(selectedId)) {
            return state.lookups.byId.get(selectedId);
        }

        return state.surface.nodes.find(node => (node.family || "").toLowerCase() === "root")
            || state.surface.nodes.find(node => !node.parentId)
            || state.surface.nodes[0]
            || null;
    }

    function submitCreateRequest(state, payload, options) {
        state.pendingCreate = {
            actionId: payload?.actionId || "",
            sourceNodeId: payload?.sourceNodeId || null,
            placementKind: payload?.placementKind || "child",
            requestedAt: Date.now(),
            focusHost: options?.focusHost !== false
        };
        state.dotNetRef.invokeMethodAsync("OnCreateAction", JSON.stringify(payload));
    }

    function submitNodeEdit(state, payload) {
        state.dotNetRef.invokeMethodAsync("OnNodeEdited", JSON.stringify(payload));
    }

    function readFileAsUpload(file) {
        return new Promise((resolve, reject) => {
            if (!file) {
                resolve(null);
                return;
            }

            const reader = new FileReader();
            reader.onload = () => {
                const result = typeof reader.result === "string" ? reader.result : "";
                const separatorIndex = result.indexOf(",");
                resolve({
                    fileName: file.name || "upload.bin",
                    contentType: file.type || "application/octet-stream",
                    base64Data: separatorIndex >= 0 ? result.substring(separatorIndex + 1) : result
                });
            };
            reader.onerror = () => reject(reader.error || new Error("Failed to read file."));
            reader.readAsDataURL(file);
        });
    }

    function commitComposer(state) {
        if (!state.composer) {
            return;
        }

        if (state.composer.kind === "create") {
            if (state.composer.requiresFile && !state.composer.uploadedFile) {
                return;
            }

            const inputValues = Array.isArray(state.composer.inputFieldEntries)
                ? state.composer.inputFieldEntries.map(entry => ({
                    key: entry.key,
                    value: (entry.input.value || "").trim()
                }))
                : [];
            const hasMissingRequiredInputs = Array.isArray(state.composer.inputFieldEntries) &&
                state.composer.inputFieldEntries.some(entry => entry.isRequired && !(entry.input.value || "").trim());
            if (hasMissingRequiredInputs) {
                return;
            }

            submitCreateRequest(state, {
                ...state.composer.request,
                title: state.composer.titleInput ? state.composer.titleInput.value.trim() : "",
                subtitle: state.composer.subtitleInput ? state.composer.subtitleInput.value.trim() : "",
                notes: state.composer.notesInput ? state.composer.notesInput.value.trim() : "",
                objectSubtype: state.composer.request.objectSubtype || "",
                inputValues,
                uploadedFile: state.composer.uploadedFile || null
            }, { focusHost: true });
            closeComposer(state);
            return;
        }

        const text = state.composer.textInput.value.trim();
        if (!text) {
            return;
        }

        if (state.composer.kind === "note-create") {
            submitCreateRequest(state, {
                actionId: state.composer.actionId,
                sourceNodeId: state.composer.sourceNodeId,
                parentNodeId: state.composer.parentNodeId,
                x: round(state.composer.anchorWorld.x),
                y: round(state.composer.anchorWorld.y),
                title: text,
                subtitle: "",
                notes: text,
                placementKind: state.composer.placementKind,
                createMode: "quick-note"
            }, { focusHost: true });
        }
        else if (state.composer.kind === "note-edit") {
            submitNodeEdit(state, {
                nodeId: state.composer.nodeId,
                title: text,
                notes: text
            });
        }

        closeComposer(state);
    }

    function decorateComposerShell(state, title, kicker, variant) {
        const composer = createElement(state.document, "div", `cw-canvas-composer ${variant ? `is-${variant}` : ""}`);
        composer.addEventListener("pointerdown", event => event.stopPropagation());
        composer.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                event.preventDefault();
                closeComposer(state);
                return;
            }

            if (event.key === "Enter" && !event.shiftKey && !event.ctrlKey && !event.metaKey) {
                const tagName = event.target?.tagName?.toLowerCase?.() || "";
                if (variant === "note" || tagName !== "textarea") {
                    event.preventDefault();
                    commitComposer(state);
                }
            }
        });

        const card = createElement(state.document, "div", "cw-canvas-composer__card");
        if (kicker) {
            card.appendChild(createElement(state.document, "p", "cw-canvas-composer__kicker", kicker));
        }

        if (title) {
            card.appendChild(createElement(state.document, "h3", "cw-canvas-composer__title", title));
        }

        composer.appendChild(card);
        state.host.appendChild(composer);
        return { composer, card };
    }

    function updateComposerFileState(composer) {
        if (!composer) {
            return;
        }

        if (composer.fileSummary) {
            composer.fileSummary.textContent = composer.uploadedFile
                ? `${composer.uploadedFile.fileName} ready`
                : (composer.filePrompt || "Drop a file here or choose one.");
        }

        if (composer.createButton) {
            const hasMissingRequiredInputs = Array.isArray(composer.inputFieldEntries) &&
                composer.inputFieldEntries.some(entry => entry.isRequired && !(entry.input.value || "").trim());
            composer.createButton.disabled = (!!composer.requiresFile && !composer.uploadedFile) || hasMissingRequiredInputs;
        }
    }

    function openCreateComposer(state, action, request) {
        clearContextMenu(state);
        closeComposer(state, { focusHost: false });

        const shell = decorateComposerShell(state, `Create ${action.label || "item"}`, action.label || "Create", "dialog");
        const fields = createElement(state.document, "div", "cw-canvas-composer__fields");
        const showDefaultTextFields = action.showDefaultTextFields !== false;
        let titleInput = null;
        let subtitleInput = null;
        let notesInput = null;
        let deferredNotesField = null;

        if (showDefaultTextFields) {
            const titleField = createElement(state.document, "label", "cw-canvas-composer__field");
            titleField.appendChild(createElement(state.document, "span", null, action.titleLabel || "Title"));
            titleInput = createElement(state.document, "input", "cw-canvas-composer__input");
            titleInput.type = "text";
            titleInput.value = request?.title || "";
            titleInput.placeholder = action.titlePlaceholder || "";
            titleField.appendChild(titleInput);
            fields.appendChild(titleField);

            const subtitleField = createElement(state.document, "label", "cw-canvas-composer__field");
            subtitleField.appendChild(createElement(state.document, "span", null, action.subtitleLabel || "Subtitle"));
            subtitleInput = createElement(state.document, "input", "cw-canvas-composer__input");
            subtitleInput.type = "text";
            subtitleInput.value = request?.subtitle || "";
            subtitleInput.placeholder = action.subtitlePlaceholder || "";
            subtitleField.appendChild(subtitleInput);
            fields.appendChild(subtitleField);

            const notesField = createElement(state.document, "label", "cw-canvas-composer__field");
            notesField.appendChild(createElement(state.document, "span", null, action.notesLabel || "Notes"));
            notesInput = createElement(state.document, "textarea", "cw-canvas-composer__textarea");
            notesInput.value = request?.notes || "";
            notesInput.placeholder = action.notesPlaceholder || "";
            notesField.appendChild(notesInput);
            if (action.requiresFile) {
                deferredNotesField = notesField;
            }
            else {
                fields.appendChild(notesField);
            }
        }

        const inputValueLookup = new Map();
        const initialInputValues = Array.isArray(request?.inputValues) && request.inputValues.length
            ? request.inputValues
            : (Array.isArray(action?.defaultInputValues) ? action.defaultInputValues : []);
        for (const item of initialInputValues) {
            if (!item?.key) {
                continue;
            }

            inputValueLookup.set(item.key, item.value || "");
        }

        const inputFieldEntries = [];
        for (const field of action.inputFields || []) {
            const fieldWrapper = createElement(state.document, "label", "cw-canvas-composer__field");
            fieldWrapper.appendChild(createElement(
                state.document,
                "span",
                null,
                `${field.label || field.key || "Value"}${field.isRequired ? " *" : ""}`));

            const inputMode = (field.inputMode || "text").toLowerCase();
            const isMultiline = inputMode === "textarea" || inputMode === "multiline";
            const input = createElement(
                state.document,
                isMultiline ? "textarea" : "input",
                isMultiline ? "cw-canvas-composer__textarea" : "cw-canvas-composer__input");
            if (!isMultiline) {
                input.type = inputMode === "url" ? "url" : "text";
            }

            input.value = inputValueLookup.get(field.key) || "";
            input.placeholder = field.placeholder || "";
            fieldWrapper.appendChild(input);
            fields.appendChild(fieldWrapper);
            input.addEventListener("input", () => {
                if (state.composer) {
                    updateComposerFileState(state.composer);
                }
            });
            inputFieldEntries.push({
                key: field.key,
                input,
                isRequired: !!field.isRequired
            });
        }

        let uploadedFile = request?.uploadedFile || null;
        let fileSummary = null;
        let fileInput = null;
        if (action.requiresFile) {
            const uploadField = createElement(state.document, "div", "cw-canvas-composer__upload");
            const uploadTitle = createElement(state.document, "span", "cw-canvas-composer__upload-title", action.filePrompt || "Drop a file here or choose one.");
            uploadField.appendChild(uploadTitle);

            const dropZone = createElement(state.document, "div", "cw-canvas-composer__dropzone");
            dropZone.tabIndex = 0;
            fileInput = createElement(state.document, "input", "cw-canvas-composer__file-input");
            fileInput.type = "file";
            fileInput.tabIndex = -1;
            fileInput.setAttribute("aria-hidden", "true");
            if (action.acceptedFileTypes) {
                fileInput.accept = action.acceptedFileTypes;
            }

            const fileTrigger = createElement(state.document, "button", "cw-button cw-canvas-composer__file-trigger", "Choose file");
            fileTrigger.type = "button";
            fileTrigger.dataset.tone = "ghost";
            const dropCopy = createElement(state.document, "span", null, action.supportsDragDrop ? "Click or drop a file here." : "Click to choose a file.");
            fileSummary = createElement(state.document, "small", "cw-canvas-composer__upload-summary", "");
            dropZone.appendChild(fileInput);
            dropZone.appendChild(fileTrigger);
            dropZone.appendChild(dropCopy);
            dropZone.appendChild(fileSummary);
            uploadField.appendChild(dropZone);
            fields.appendChild(uploadField);

            const assignUpload = async file => {
                uploadedFile = await readFileAsUpload(file);
                if (state.composer) {
                    state.composer.uploadedFile = uploadedFile;
                    updateComposerFileState(state.composer);
                }
            };

            const openFilePicker = event => {
                event?.preventDefault?.();
                event?.stopPropagation?.();
                if (!fileInput) {
                    return;
                }

                try {
                    if (typeof fileInput.showPicker === "function") {
                        fileInput.showPicker();
                        return;
                    }
                }
                catch {
                }

                fileInput.click();
            };

            dropZone.addEventListener("pointerdown", event => event.stopPropagation());
            dropZone.addEventListener("click", event => {
                if (event.target === fileInput || event.target?.closest?.(".cw-canvas-composer__file-trigger")) {
                    return;
                }

                openFilePicker(event);
            });
            dropZone.addEventListener("keydown", event => {
                if (event.key !== "Enter" && event.key !== " ") {
                    return;
                }

                openFilePicker(event);
            });
            fileTrigger.addEventListener("pointerdown", event => event.stopPropagation());
            fileTrigger.addEventListener("click", openFilePicker);
            fileInput.addEventListener("click", event => event.stopPropagation());
            fileInput.addEventListener("change", async event => {
                const file = event.target?.files?.[0];
                if (!file) {
                    return;
                }

                await assignUpload(file);
            });

            if (action.supportsDragDrop) {
                ["dragenter", "dragover"].forEach(eventName => dropZone.addEventListener(eventName, event => {
                    event.preventDefault();
                    dropZone.classList.add("is-dragover");
                }));
                ["dragleave", "dragend"].forEach(eventName => dropZone.addEventListener(eventName, () => {
                    dropZone.classList.remove("is-dragover");
                }));
                dropZone.addEventListener("drop", async event => {
                    event.preventDefault();
                    dropZone.classList.remove("is-dragover");
                    const file = event.dataTransfer?.files?.[0];
                    if (!file) {
                        return;
                    }

                    await assignUpload(file);
                });
            }
        }

        if (deferredNotesField) {
            fields.appendChild(deferredNotesField);
        }

        const actions = createElement(state.document, "div", "cw-canvas-composer__actions");
        const cancel = createElement(state.document, "button", "cw-button");
        cancel.type = "button";
        cancel.textContent = "Cancel";
        cancel.addEventListener("click", () => closeComposer(state));
        actions.appendChild(cancel);

        const create = createElement(state.document, "button", "cw-button");
        create.type = "button";
        create.dataset.tone = "accent";
        create.textContent = action.submitLabel || action.label || "Create";
        create.addEventListener("click", () => commitComposer(state));
        actions.appendChild(create);

        shell.card.appendChild(fields);
        if (action.description) {
            shell.card.appendChild(createElement(state.document, "p", "cw-canvas-composer__copy", action.description));
        }
        shell.card.appendChild(actions);

        state.composer = {
            kind: "create",
            element: shell.composer,
            request: request || {},
            anchorWorld: request ? { x: request.x || 0, y: request.y || 0 } : { x: 0, y: 0 },
            titleInput,
            subtitleInput,
            notesInput,
            inputFieldEntries,
            createButton: create,
            requiresFile: !!action.requiresFile,
            uploadedFile,
            fileSummary,
            fileInput,
            filePrompt: action.filePrompt || "Drop a file here or choose one."
        };

        window.requestAnimationFrame(() => {
            layoutComposer(state);
            updateComposerFileState(state.composer);
            const firstInput = inputFieldEntries[0]?.input || titleInput || subtitleInput || notesInput;
            if (firstInput) {
                firstInput.focus();
                if (typeof firstInput.select === "function") {
                    firstInput.select();
                }
            }
        });
    }

    function openInlineNoteComposer(state, options) {
        closeComposer(state, { focusHost: false });
        clearContextMenu(state);

        const shell = decorateComposerShell(state, "", "", "note");
        const noteEditor = createElement(state.document, "div", "cw-note-editor");
        const textInput = createElement(state.document, "input", "cw-note-editor__input");
        textInput.type = "text";
        textInput.value = options.value || "";
        textInput.placeholder = options.placeholder || state.surface.chrome.inlineNotePlaceholder || "Write note";
        noteEditor.appendChild(textInput);
        noteEditor.appendChild(createElement(state.document, "p", "cw-note-editor__hint", "Enter saves. Escape cancels."));
        shell.card.appendChild(noteEditor);

        state.composer = {
            kind: options.kind,
            element: shell.composer,
            actionId: options.actionId || "",
            sourceNodeId: options.sourceNodeId || null,
            parentNodeId: options.parentNodeId || null,
            placementKind: options.placementKind || "child",
            nodeId: options.nodeId || null,
            anchorWorld: options.anchorWorld,
            textInput
        };

        window.requestAnimationFrame(() => {
            layoutComposer(state);
            textInput.focus();
            textInput.select();
        });
    }

    function buildChildNotePlacement(position, childCount) {
        const column = childCount % 3;
        const row = Math.floor(childCount / 3);
        return {
            x: round(position.x + 240 + (column * 46)),
            y: round(position.y - 70 + (row * 118))
        };
    }

    function buildSiblingNotePlacement(position, siblingCount) {
        return {
            x: round(position.x + ((siblingCount % 2) * 24)),
            y: round(position.y + 132)
        };
    }

    function openKeyboardNoteComposer(state, placementKind) {
        const selectedId = state.ui.selectedNodeIds[0];
        if (!selectedId) {
            return;
        }

        const node = state.lookups.byId.get(selectedId);
        if (!node) {
            return;
        }

        const isSibling = placementKind === "sibling";
        const actionId = isSibling ? state.surface.chrome.siblingNoteActionId : state.surface.chrome.childNoteActionId;
        if (!actionId) {
            return;
        }

        const position = getNodePosition(state, node);
        const anchorWorld = isSibling
            ? buildSiblingNotePlacement(position, state.surface.nodes.filter(candidate => candidate.parentId === node.parentId && candidate.id !== node.id).length)
            : buildChildNotePlacement(position, state.surface.nodes.filter(candidate => candidate.parentId === node.id).length);

        openInlineNoteComposer(state, {
            kind: "note-create",
            actionId,
            sourceNodeId: node.id,
            parentNodeId: isSibling ? (node.parentId || node.id) : node.id,
            placementKind,
            anchorWorld,
            value: "",
            placeholder: node.inlineTextPlaceholder || state.surface.chrome.inlineNotePlaceholder || "Write note"
        });
    }

    function openExistingNoteEditor(state, node) {
        openInlineNoteComposer(state, {
            kind: "note-edit",
            nodeId: node.id,
            anchorWorld: getNodePosition(state, node),
            value: node.inlineText || node.title || "",
            placeholder: node.inlineTextPlaceholder || state.surface.chrome.inlineNotePlaceholder || "Write note"
        });
    }

    function executeContextAction(state, node, action, clientX, clientY, placementKind) {
        if (action?.children?.length) {
            return;
        }

        if (isCreateAction(action) || !node) {
            const request = buildCreateRequest(
                state,
                action,
                node,
                node ? getNodePosition(state, node) : getWorldPoint(state, clientX, clientY),
                placementKind || (node ? "child" : "canvas"));

            if (action.requiresInput) {
                openCreateComposer(state, action, request);
                return;
            }

            submitCreateRequest(state, request, { focusHost: true });
            clearContextMenu(state);
            return;
        }

        clearContextMenu(state);
        const position = getNodePosition(state, node);
        state.dotNetRef.invokeMethodAsync("OnContextAction", node.id, action.actionId, round(position.x), round(position.y));
    }

    function openContextSubmenu(state, parentLayer, options, action, offset) {
        const nextDepth = parentLayer.depth + 1;
        const existingLayer = state.contextMenuState?.layers?.[nextDepth];
        if (existingLayer &&
            existingLayer.ownerActionId === action.actionId &&
            existingLayer.ownerDepth === parentLayer.depth) {
            return;
        }

        closeContextMenuLayersFrom(state, nextDepth);

        const submenuLayout = action.submenuLayout || "";
        if (submenuLayout === "toolbox-panel") {
            const panelSize = getToolboxPanelSize();
            const submenuOrigin = clampToolboxPanelOriginToHost(
                state,
                resolveToolboxPanelOrigin(parentLayer, offset, panelSize),
                panelSize);
            const submenuLayer = createContextMenuLayer(state, {
                depth: nextDepth,
                label: action.label || "Components",
                originOffset: submenuOrigin,
                ownerActionId: action.actionId,
                ownerDepth: parentLayer.depth,
                mode: "panel",
                panelSize
            });

            renderToolboxPanelLayer(state, submenuLayer, {
                actions: action.children || [],
                node: options.node,
                clientX: options.clientX,
                clientY: options.clientY,
                placementKind: options.placementKind,
                label: action.label || "Components",
                description: action.description || ""
            });

            state.contextMenu.appendChild(submenuLayer.element);
            state.contextMenuState.layers.push(submenuLayer);
            return;
        }

        const menuScale = getMenuScale(state);
        const submenuBaseRadius = (submenuLayout === "compact-ring" ? 74 : 80) * menuScale;
        const submenuRingStep = (submenuLayout === "compact-ring" ? 0 : 64) * menuScale;
        const submenuOffsets = resolveContextMenuOffsets(action.children || [], submenuBaseRadius, submenuRingStep, submenuLayout);
        const submenuRadius = getContextMenuOrbitRadius(state, submenuOffsets, action.children || []);
        const submenuOrigin = clampLayerOriginToHost(
            state,
            resolveSubmenuOrigin(parentLayer, offset, submenuLayout),
            submenuOffsets,
            submenuRadius,
            action.children || []);

        const submenuLayer = createContextMenuLayer(state, {
            depth: nextDepth,
            label: action.label || "More",
            originOffset: submenuOrigin,
            ownerActionId: action.actionId,
            ownerDepth: parentLayer.depth
        });

        renderContextMenuLayer(state, submenuLayer, {
            actions: action.children || [],
            offsets: submenuOffsets,
            node: options.node,
            clientX: options.clientX,
            clientY: options.clientY,
            placementKind: options.placementKind,
            depth: nextDepth,
            baseRadius: submenuBaseRadius,
            ringStep: submenuRingStep,
            submenuLayout
        });

        state.contextMenu.appendChild(submenuLayer.element);
        state.contextMenuState.layers.push(submenuLayer);
    }

    function openContextSubmenuByActionId(state, actionId) {
        const rootLayer = state.contextMenuState?.layers?.[0];
        const entry = rootLayer?.actionEntries?.get(actionId || "");
        if (!entry || !entry.action?.children?.length) {
            return false;
        }

        openContextSubmenu(state, rootLayer, entry.options, entry.action, entry.offset);
        return true;
    }

    function renderContextMenuLayer(state, layerState, options) {
        const offsets = options.offsets || resolveContextMenuOffsets(options.actions, options.baseRadius, options.ringStep, options.submenuLayout);
        layerState.actionEntries = new Map();
        layerState.radius = getContextMenuOrbitRadius(state, offsets, options.actions);
        layerState.orbit.style.setProperty("--cw-orbit-size", `${round(layerState.radius * 2)}px`);
        if (layerState.backdrop) {
            layerState.backdrop.style.setProperty("--cw-orbit-size", `${round(layerState.radius * 2)}px`);
        }
        options.actions.forEach((action, index) => {
            const offset = offsets[index];
            const variant = resolveMenuActionVariant(action);
            const button = createElement(
                state.document,
                "button",
                `cw-context-menu__action tone-${action.tone || "neutral"}${(action.menuSize || "").toLowerCase() === "compact" ? " is-compact" : ""}`);
            button.type = "button";
            button.dataset.actionId = action.actionId || "";
            button.dataset.layerDepth = `${options.depth || 0}`;
            button.dataset.menuSize = action.menuSize || "normal";
            button.title = action.description || resolveMenuActionAriaLabel(action);
            button.setAttribute("aria-label", resolveMenuActionAriaLabel(action));
            button.style.setProperty("--cw-menu-x", `${round(offset.x)}px`);
            button.style.setProperty("--cw-menu-y", `${round(offset.y)}px`);
            if (variant === "progress-preset") {
                button.classList.add("is-progress-preset");
                applyProgressPresetTone(button, action);
            }
            else if (variant === "marker-preset") {
                button.classList.add("is-marker-preset");
            }
            else if (variant === "priority-preset") {
                button.classList.add("is-priority-preset");
            }
            button.addEventListener("pointerdown", event => event.stopPropagation());
            button.addEventListener("pointermove", event => {
                event.stopPropagation();
                if (action.children?.length) {
                    openContextSubmenu(state, layerState, options, action, offset);
                }
            });
            button.addEventListener("pointerenter", () => {
                if (!action.children?.length) {
                    closeContextMenuLayersFrom(state, (options.depth || 0) + 1);
                    return;
                }

                openContextSubmenu(state, layerState, options, action, offset);
            });
            button.addEventListener("click", event => {
                event.stopPropagation();
                executeContextAction(state, options.node, action, options.clientX, options.clientY, options.placementKind);
            });

            button.appendChild(createMenuActionIcon(state, action));
            let label = null;
            if (variant !== "priority-preset") {
                const labelText = resolveMenuLabel(action);
                label = createElement(state.document, "strong", "cw-context-menu__label", labelText);
                label.dataset.fullText = labelText;
                button.appendChild(label);
            }
            if (action.children?.length) {
                button.appendChild(createElement(state.document, "span", "cw-context-menu__caret", "\u203A"));
                button.classList.add("has-children");
            }

            layerState.orbit.appendChild(button);
            if (label) {
                fitContextMenuLabel(button, label, variant);
            }
            layerState.actionEntries.set(action.actionId || `index-${index}`, {
                action,
                offset,
                options: {
                    node: options.node,
                    clientX: options.clientX,
                    clientY: options.clientY,
                    placementKind: options.placementKind
                }
            });
        });

        return offsets;
    }

    function renderToolboxPreview(state, layerState, previewHost, action, sectionLabel, groupLabel) {
        if (!previewHost) {
            return;
        }

        clear(previewHost);

        const kicker = createElement(state.document, "span", "cw-context-toolbox__preview-kicker", `${sectionLabel} / ${groupLabel}`);
        const title = createElement(state.document, "strong", "cw-context-toolbox__preview-title", action?.label || action?.menuLabel || "Component");
        const copy = createElement(
            state.document,
            "p",
            "cw-context-toolbox__preview-copy",
            action?.description || "Hover a component to preview its prompt text and usage.");
        const meta = createElement(state.document, "div", "cw-context-toolbox__preview-meta");
        meta.appendChild(createElement(
            state.document,
            "span",
            "cw-context-toolbox__preview-pill",
            action?.requiresInput ? `${action.inputFields?.length || 0} inputs required` : "Ready to add"));
        meta.appendChild(createElement(
            state.document,
            "span",
            "cw-context-toolbox__preview-pill",
            action?.tone || "neutral"));

        previewHost.appendChild(kicker);
        previewHost.appendChild(title);
        previewHost.appendChild(copy);
        previewHost.appendChild(meta);
        layerState.previewActionId = action?.actionId || "";
    }

    function renderToolboxPanelLayer(state, layerState, options) {
        const panel = layerState.panel;
        if (!panel) {
            return;
        }

        clear(panel);
        layerState.actionEntries = new Map();

        const header = createElement(state.document, "div", "cw-context-toolbox__header");
        const headerCopy = createElement(state.document, "div", "cw-context-toolbox__header-copy");
        headerCopy.appendChild(createElement(state.document, "span", "cw-context-toolbox__eyebrow", options.label || "Components"));
        headerCopy.appendChild(createElement(state.document, "strong", "cw-context-toolbox__title", options.label || "Prompt components"));
        if (options.description) {
            headerCopy.appendChild(createElement(state.document, "p", "cw-context-toolbox__copy", options.description));
        }
        header.appendChild(headerCopy);
        header.appendChild(createElement(state.document, "span", "cw-context-toolbox__count", `${options.actions?.length || 0} sections`));
        panel.appendChild(header);

        const search = createElement(state.document, "input", "cw-context-toolbox__search");
        search.type = "search";
        search.placeholder = "Search components";
        search.setAttribute("aria-label", "Search prompt components");
        panel.appendChild(search);

        const body = createElement(state.document, "div", "cw-context-toolbox__body");
        const sectionsHost = createElement(state.document, "div", "cw-context-toolbox__sections");
        const previewHost = createElement(state.document, "aside", "cw-context-toolbox__preview");
        body.appendChild(sectionsHost);
        body.appendChild(previewHost);
        panel.appendChild(body);

        const renderSections = () => {
            clear(sectionsHost);
            const query = (search.value || "").trim().toLowerCase();
            let firstPreview = null;

            for (let sectionIndex = 0; sectionIndex < (options.actions || []).length; sectionIndex++) {
                const sectionAction = options.actions[sectionIndex];
                const matchingGroups = [];

                for (const groupAction of (sectionAction.children || [])) {
                    const matchingItems = (groupAction.children || []).filter(item => {
                        if (!query) {
                            return true;
                        }

                        const haystack = [
                            item.label,
                            item.menuLabel,
                            item.description,
                            groupAction.label,
                            sectionAction.label
                        ].join(" ").toLowerCase();
                        return haystack.includes(query);
                    });

                    if (matchingItems.length > 0) {
                        matchingGroups.push({ groupAction, matchingItems });
                    }
                }

                const sectionMatches = !query ||
                    [sectionAction.label, sectionAction.description].join(" ").toLowerCase().includes(query);
                if (!sectionMatches && matchingGroups.length === 0) {
                    continue;
                }

                const section = createElement(state.document, "details", "cw-context-toolbox__section");
                section.open = !!query || sectionIndex === 0;

                const sectionSummary = createElement(state.document, "summary", "cw-context-toolbox__section-summary");
                const sectionBadge = createElement(state.document, `span`, `cw-context-toolbox__section-badge tone-${sectionAction.tone || "neutral"}`);
                sectionBadge.textContent = resolveActionGlyph(sectionAction.icon || "");
                const sectionSummaryCopy = createElement(state.document, "span", "cw-context-toolbox__section-copy");
                sectionSummaryCopy.appendChild(createElement(state.document, "strong", null, sectionAction.label || "Section"));
                sectionSummaryCopy.appendChild(createElement(state.document, "small", null, sectionAction.description || ""));
                sectionSummary.appendChild(sectionBadge);
                sectionSummary.appendChild(sectionSummaryCopy);
                section.appendChild(sectionSummary);

                const sectionBody = createElement(state.document, "div", "cw-context-toolbox__section-body");

                for (let groupIndex = 0; groupIndex < matchingGroups.length; groupIndex++) {
                    const groupEntry = matchingGroups[groupIndex];
                    const group = createElement(state.document, "details", "cw-context-toolbox__group");
                    group.open = !!query || groupIndex === 0;

                    const groupSummary = createElement(state.document, "summary", "cw-context-toolbox__group-summary");
                    groupSummary.appendChild(createElement(state.document, "strong", null, groupEntry.groupAction.label || "Group"));
                    groupSummary.appendChild(createElement(state.document, "small", null, groupEntry.groupAction.description || ""));
                    group.appendChild(groupSummary);

                    const itemList = createElement(state.document, "div", "cw-context-toolbox__item-list");
                    for (const itemAction of groupEntry.matchingItems) {
                        if (!firstPreview) {
                            firstPreview = {
                                action: itemAction,
                                sectionLabel: sectionAction.label || "Section",
                                groupLabel: groupEntry.groupAction.label || "Group"
                            };
                        }

                        const item = createElement(state.document, "button", "cw-context-toolbox__item");
                        item.type = "button";
                        item.dataset.tone = itemAction.tone || "neutral";
                        item.addEventListener("pointerdown", event => event.stopPropagation());
                        item.addEventListener("pointerenter", () => {
                            renderToolboxPreview(state, layerState, previewHost, itemAction, sectionAction.label || "Section", groupEntry.groupAction.label || "Group");
                        });
                        item.addEventListener("focus", () => {
                            renderToolboxPreview(state, layerState, previewHost, itemAction, sectionAction.label || "Section", groupEntry.groupAction.label || "Group");
                        });
                        item.addEventListener("click", event => {
                            event.stopPropagation();
                            executeContextAction(state, options.node, itemAction, options.clientX, options.clientY, options.placementKind);
                        });

                        const itemIcon = createElement(state.document, "span", "cw-context-toolbox__item-icon");
                        itemIcon.appendChild(createMenuActionIcon(state, itemAction));
                        const itemBody = createElement(state.document, "span", "cw-context-toolbox__item-body");
                        itemBody.appendChild(createElement(state.document, "strong", null, itemAction.label || itemAction.menuLabel || "Item"));
                        itemBody.appendChild(createElement(state.document, "small", null, itemAction.requiresInput ? "Specify inputs before inserting" : "Add directly to the prompt flow"));
                        item.appendChild(itemIcon);
                        item.appendChild(itemBody);
                        itemList.appendChild(item);
                    }

                    group.appendChild(itemList);
                    sectionBody.appendChild(group);
                }

                section.appendChild(sectionBody);
                sectionsHost.appendChild(section);
            }

            if (!sectionsHost.childElementCount) {
                sectionsHost.appendChild(createElement(state.document, "div", "cw-context-toolbox__empty", "No prompt components match the current search."));
                renderToolboxPreview(state, layerState, previewHost, null, "Search", "No results");
                return;
            }

            if (firstPreview && layerState.previewActionId !== firstPreview.action.actionId) {
                renderToolboxPreview(state, layerState, previewHost, firstPreview.action, firstPreview.sectionLabel, firstPreview.groupLabel);
            }
        };

        search.addEventListener("input", renderSections);
        renderSections();
        window.requestAnimationFrame(() => search.focus({ preventScroll: true }));
    }

    function showContextMenu(state, options) {
        clearContextMenu(state);
        const actions = options.actions || getContextActions(state, options.node);
        if (!actions.length) {
            return;
        }

        ensureHostFocus(state);
        deferHostFocus(state);
        const hostPoint = getHostPoint(state, options.clientX, options.clientY);
        state.contextMenu.style.display = "block";
        const menuScale = getMenuScale(state);
        const rootOffsets = resolveContextMenuOffsets(actions, 84 * menuScale, 62 * menuScale, "");
        const rootCenter = positionContextMenu(state, hostPoint, rootOffsets, actions);
        state.contextMenuState = {
            node: options.node || null,
            actions,
            clientX: options.clientX,
            clientY: options.clientY,
            placementKind: options.placementKind || (options.node ? "child" : "canvas"),
            rootCenter,
            layers: []
        };

        const rootLayer = createContextMenuLayer(state, {
            depth: 0,
            label: options.label || options.node?.title || "Canvas",
            originOffset: { x: 0, y: 0 }
        });
        renderContextMenuLayer(state, rootLayer, {
            actions,
            node: options.node || null,
            clientX: options.clientX,
            clientY: options.clientY,
            placementKind: options.placementKind || (options.node ? "child" : "canvas"),
            depth: 0,
            baseRadius: 84 * menuScale,
            ringStep: 62 * menuScale,
            submenuLayout: ""
        });
        state.contextMenu.appendChild(rootLayer.element);
        state.contextMenuState.layers.push(rootLayer);
    }

    function openNodeMetadataMenu(state, node, actionId, anchorElement) {
        if (!node || !anchorElement) {
            return;
        }

        if (state.selectedIds.size !== 1 || !state.selectedIds.has(node.id)) {
            setSelection(state, [node.id], true);
        }

        const rect = anchorElement.getBoundingClientRect();
        showContextMenu(state, {
            node,
            clientX: rect.left + (rect.width / 2),
            clientY: rect.top + (rect.height / 2),
            placementKind: "child",
            label: node.title || "Canvas"
        });
        openContextSubmenuByActionId(state, actionId);
    }

    function startPan(state, event) {
        clearSnapGuides(state);
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
            frameId: options?.frameId || null
        };
        clearSnapGuides(state);
        render(state);
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

        startDragForNodeIds(state, event, renderedFrame.nodeIds, { kind: "frame-drag", frameId });
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

    function applyMarqueeSelection(state) {
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
        state.interaction.moved = state.interaction.moved || Math.abs(deltaX) > 0.5 || Math.abs(deltaY) > 0.5;

        for (const nodeId of state.interaction.nodeIds) {
            const startPosition = state.interaction.startPositions[nodeId];
            state.ui.manualPositions[nodeId] = {
                x: round(startPosition.x + deltaX),
                y: round(startPosition.y + deltaY)
            };
        }

        render(state);
    }

    function updatePan(state, event) {
        const deltaX = event.clientX - state.interaction.startClientX;
        const deltaY = event.clientY - state.interaction.startClientY;
        state.interaction.moved = state.interaction.moved || Math.abs(deltaX) > 1 || Math.abs(deltaY) > 1;
        setPan(state, state.interaction.panX + deltaX, state.interaction.panY + deltaY);
        render(state);
    }

    function finishInteraction(state) {
        if (!state.interaction) {
            return;
        }

        const interaction = state.interaction;
        state.interaction = null;
        clearSnapGuides(state);

        switch (interaction.kind) {
            case "drag":
            case "frame-drag":
                if (interaction.moved) {
                    publishNodesMoved(state, interaction.nodeIds);
                    publishState(state);
                }
                break;
            case "pan":
                if (interaction.moved) {
                    publishState(state);
                }
                break;
            case "marquee":
                applyMarqueeSelection(state);
                break;
        }

        render(state);
    }

    function isNodeVisibleInViewport(state, node, margin) {
        const rect = state.host.getBoundingClientRect();
        const position = worldToHostPoint(state, getNodePosition(state, node));
        const size = getNodeSize(state, node);
        const halfWidth = (size.width * state.ui.zoom) / 2;
        const halfHeight = (size.height * state.ui.zoom) / 2;
        const safeMargin = typeof margin === "number" ? margin : 92;

        return position.x - halfWidth >= safeMargin &&
            position.x + halfWidth <= rect.width - safeMargin &&
            position.y - halfHeight >= safeMargin &&
            position.y + halfHeight <= rect.height - safeMargin;
    }

    function centerNodeElementInViewport(state, nodeId) {
        const element = state.nodeLayer?.querySelector?.(`.cw-node[data-node-id="${nodeId}"]`);
        if (!element) {
            return false;
        }

        const hostRect = state.host.getBoundingClientRect();
        const nodeRect = element.getBoundingClientRect();
        const deltaX = (hostRect.left + (hostRect.width / 2)) - (nodeRect.left + (nodeRect.width / 2));
        const deltaY = (hostRect.top + (hostRect.height / 2)) - (nodeRect.top + (nodeRect.height / 2));
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

        const rect = state.host.getBoundingClientRect();
        const position = getNodePosition(state, node);
        setPan(
            state,
            (rect.width / 2) - (position.x * state.ui.zoom),
            (rect.height / 2) - (position.y * state.ui.zoom));
        return true;
    }

    function resize(state) {
        cancelViewportAnimation(state);
        const rect = state.host.getBoundingClientRect();
        state.links.setAttribute("width", `${Math.max(rect.width, 1)}`);
        state.links.setAttribute("height", `${Math.max(rect.height, 1)}`);
        setPan(state, state.ui.panX, state.ui.panY);
        layoutComposer(state);
    }

    function findContainingBlockOverride(state) {
        let current = state.shell?.parentElement || null;
        while (current) {
            const style = window.getComputedStyle(current);
            if (style.transform !== "none" ||
                style.perspective !== "none" ||
                style.filter !== "none" ||
                style.backdropFilter !== "none" ||
                style.webkitBackdropFilter !== "none") {
                return current;
            }

            current = current.parentElement;
        }

        return null;
    }

    function suspendContainingBlock(state) {
        if (state.containingBlockOverride) {
            return;
        }

        const element = findContainingBlockOverride(state);
        if (!element) {
            return;
        }

        state.containingBlockOverride = {
            element,
            filter: element.style.filter,
            backdropFilter: element.style.backdropFilter,
            webkitBackdropFilter: element.style.webkitBackdropFilter
        };
        element.style.filter = "none";
        element.style.backdropFilter = "none";
        element.style.webkitBackdropFilter = "none";
    }

    function restoreContainingBlock(state) {
        if (!state.containingBlockOverride) {
            return;
        }

        const override = state.containingBlockOverride;
        override.element.style.filter = override.filter || "";
        override.element.style.backdropFilter = override.backdropFilter || "";
        override.element.style.webkitBackdropFilter = override.webkitBackdropFilter || "";
        state.containingBlockOverride = null;
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

        resize(state);
        render(state);
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
        const rect = state.host.getBoundingClientRect();
        const position = getNodePosition(state, node);
        const viewportController = getViewportControllerService();
        const target = viewportController?.createFocusTarget
            ? viewportController.createFocusTarget({
                bounds: getSceneBounds(state),
                hostWidth: rect.width,
                hostHeight: rect.height,
                pointX: position.x,
                pointY: position.y,
                zoom: state.ui.zoom
            })
            : {
                zoom: state.ui.zoom,
                panX: (rect.width / 2) - (position.x * state.ui.zoom),
                panY: (rect.height / 2) - (position.y * state.ui.zoom)
            };

        animateViewportTransition(state, target, {
            key: "viewport",
            durationMs: 280,
            easing: "softInOut"
        });
        ensureHostFocus(state);
        deferHostFocus(state);
        publishSelection(state);
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
        setZoomPercent(state, (state.ui.zoom * 100) + (-direction * stepCount * 4), hostPoint);
    }

    function setZoomPercent(state, percent, anchorPoint) {
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
        render(state);
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

        if (node.isCollapsible) {
            toggleCollapse(state, node.id);
            return;
        }

        state.dotNetRef.invokeMethodAsync("OnNodeOpened", node.id);
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

                if (targetNode.isInlineTextNode) {
                    openExistingNoteEditor(state, targetNode);
                    return;
                }

                if (targetNode.isCollapsible) {
                    toggleCollapse(state, targetNode.id);
                    return;
                }

                state.dotNetRef.invokeMethodAsync("OnNodeOpened", targetNode.id);
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

    function buildWorkbench(state) {
        clear(state.host);
        state.host.classList.add("cw-workbench");
        syncMenuScaleCss(state);

        const backdrop = createElement(state.document, "div", "cw-workbench__backdrop");
        const scene = createElement(state.document, "div", "cw-workbench__scene");
        const frameLayer = createElement(state.document, "div", "cw-workbench__frame-layer");
        const links = createSvgElement(state.document, "svg", "cw-workbench__links");
        const debugLayer = createElement(state.document, "div", "cw-workbench__debug-layer");
        const guideLayer = createElement(state.document, "div", "cw-workbench__guide-layer");
        const nodeLayer = createElement(state.document, "div", "cw-workbench__node-layer");
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
        const minimapCanvas = createSvgElement(state.document, "svg", "cw-minimap__canvas");
        const popover = createElement(state.document, "div", "cw-workbench__popover");
        const popoverTitle = createElement(state.document, "strong", "cw-workbench__popover-title");
        const popoverBody = createElement(state.document, "span", "cw-workbench__popover-body");
        const statusNotice = createElement(state.document, "div", "cw-status-notice");
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
        minimapCanvas.setAttribute("viewBox", "0 0 200 128");
        minimapCanvas.addEventListener("pointerdown", event => {
            event.preventDefault();
            event.stopPropagation();
            navigateViaMinimap(state, event);
        });
        minimapShell.appendChild(minimapCanvas);
        popover.appendChild(popoverTitle);
        popover.appendChild(popoverBody);

        scene.appendChild(frameLayer);
        scene.appendChild(links);
        scene.appendChild(debugLayer);
        scene.appendChild(guideLayer);
        scene.appendChild(nodeLayer);
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
        state.frameLayer = frameLayer;
        state.links = links;
        state.debugLayer = debugLayer;
        state.guideLayer = guideLayer;
        state.nodeLayer = nodeLayer;
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
        resize(state);
    }

    function hydrateState(host, dotNetRef, surface) {
        const normalizedSurface = normalizeSurface(surface);
        const lookups = buildNodeLookup(normalizedSurface.nodes);
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
            containingBlockOverride: null,
            resizeObserver: null,
            lastPointerTarget: null,
            recentDoubleActivationAt: 0,
            wheelZoom: null,
            measuredNodeSizes: new Map(),
            renderedFrames: new Map(),
            layoutPositions: null,
            layoutKey: "",
            measureLayoutFrame: 0,
            snapGuides: [],
            hoveredNodeId: null,
            popover: null,
            popoverTitle: null,
            popoverBody: null,
            popoverAnchor: null,
            statusNotice: null,
            statusNoticeTimer: 0,
            localClipboard: "",
            minimapMetrics: null,
            publishStateDebounced: debounce(stateJson => dotNetRef.invokeMethodAsync("OnStateChanged", stateJson), 140)
        };

        state.animationTimeline?.setReducedMotionAttribute?.(host);
        reconcileSelection(state);
        return state;
    }

    function refresh(state, surface) {
        cancelViewportAnimation(state);
        const previousNodeIds = new Set((state.surface?.nodes || []).map(node => node.id));
        const previousSelectedId = state.ui?.selectedNodeIds?.[0] || null;
        const pendingCreate = state.pendingCreate;
        if (state.measureLayoutFrame) {
            window.cancelAnimationFrame(state.measureLayoutFrame);
            state.measureLayoutFrame = 0;
        }

        state.surface = normalizeSurface(surface);
        state.lookups = buildNodeLookup(state.surface.nodes);
        state.ui = state.surface.uiState;
        reconcileSelection(state);
        state.collapsedIds = toCollapsedSet(state.ui.collapsedNodeIds);
        syncMenuScaleCss(state);
        invalidateMeasuredLayout(state);
        clearContextMenu(state);
        hidePopover(state);
        if (state.composer?.nodeId && !state.lookups.byId.has(state.composer.nodeId)) {
            closeComposer(state, { focusHost: false });
        }

        setMaximized(state, !!state.ui.isMaximized);
        resize(state);
        const selectedNodeId = state.ui.selectedNodeIds[0] || null;
        const selectedNode = selectedNodeId ? state.lookups.byId.get(selectedNodeId) : null;
        const shouldRevealSelection = !!selectedNodeId &&
            (!!pendingCreate || (!!selectedNodeId && selectedNodeId !== previousSelectedId));
        const shouldRestoreVisibleSelection = !!selectedNodeId &&
            !!selectedNode &&
            !isNodeVisibleInViewport(state, selectedNode, 72);
        if (shouldRevealSelection || shouldRestoreVisibleSelection) {
            const isNewNode = !previousNodeIds.has(selectedNodeId);
            ensureNodeVisible(state, selectedNodeId, { forceCenter: isNewNode || shouldRestoreVisibleSelection });
        }

        render(state);
        if (selectedNodeId &&
            state.lookups.byId.has(selectedNodeId) &&
            !isNodeVisibleInViewport(state, state.lookups.byId.get(selectedNodeId), 72)) {
            ensureNodeVisible(state, selectedNodeId, { forceCenter: true });
            render(state);
        }

        if (pendingCreate) {
            if (pendingCreate.focusHost) {
                deferHostFocus(state);
            }

            state.pendingCreate = null;
        }
    }

    root.canvasWorkbench = {
        create(host, dotNetRef, surface) {
            const state = hydrateState(host, dotNetRef, surface);
            buildWorkbench(state);
            attachEvents(state);
            setMaximized(state, !!state.ui.isMaximized);
            if (typeof window.ResizeObserver === "function") {
                state.resizeObserver = new window.ResizeObserver(() => {
                    resize(state);
                    render(state);
                });
                state.resizeObserver.observe(host);
            }

            host.__canvasWorkbenchState = state;
            render(state);
        },
        update(host, surface) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            refresh(state, surface);
        },
        fitView(host) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            fitView(state);
        },
        focusNode(host, nodeId) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            focusNode(state, nodeId);
        },
        setZoomPercent(host, zoomPercent) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            setZoomPercent(state, zoomPercent);
        },
        setMenuScalePercent(host, menuScalePercent) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            setMenuScalePercent(state, menuScalePercent);
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
            return state ? serializeState(state) : JSON.stringify({});
        },
        selectNodes(host, nodeIds, primaryNodeId) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            const normalized = selectionModel.replace(nodeIds, primaryNodeId || (Array.isArray(nodeIds) ? nodeIds[0] : null));
            applySelection(state, normalized.selectedNodeIds, normalized.primaryNodeId);
        },
        setMaximized(host, isMaximized) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            setMaximized(state, isMaximized);
        },
        resize(host) {
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            resize(state);
            render(state);
        },
        dispose(host) {
            if (!host) {
                return;
            }

            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
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

            if (state.animationTimeline) {
                state.animationTimeline.dispose();
            }

            if (state.handlers) {
                host.removeEventListener("pointerdown", state.handlers.pointerDown);
                window.removeEventListener("pointermove", state.handlers.pointerMove);
                window.removeEventListener("pointerup", state.handlers.pointerUp);
                window.removeEventListener("blur", state.handlers.blur);
                host.removeEventListener("dblclick", state.handlers.doubleClick);
                host.removeEventListener("wheel", state.handlers.wheel);
                host.removeEventListener("contextmenu", state.handlers.contextMenu);
                state.document.removeEventListener("keydown", state.handlers.keyDown);
            }

            setMaximized(state, false);
            clear(host);
            delete host.__canvasWorkbenchState;
        }
    };
})();
