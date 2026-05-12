(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const contextSubmenuHoverDelayMs = 500;
    const MIN_ZOOM = 0.15;
    const MAX_ZOOM = 1.75;
    function getRequiredRootService(name) {
        const service = root[name];
        if (!service) {
            throw new Error(`CanDoItAll.${name} must be loaded before the CanvasLib workbench runtime.`);
        }

        return service;
    }
    function getTextMeasureService() {
        return root.textMeasureService || null;
    }
    function getViewportControllerService() {
        return root.viewportController || null;
    }
    function getAnimationTimelineService() {
        return root.animationTimeline || null;
    }
    const selectionModel = getRequiredRootService("selectionModel");
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
            sectionKey: field?.sectionKey || "",
            sectionTitle: field?.sectionTitle || "",
            sectionDescription: field?.sectionDescription || "",
            label: field?.label || field?.key || "Value",
            placeholder: field?.placeholder || "",
            inputMode: field?.inputMode || "text",
            isRequired: !!field?.isRequired,
            options: Array.isArray(field?.options)
                ? field.options
                    .filter(option => option && (option.value || option.label))
                    .map(option => ({
                        value: option.value || "",
                        label: option.label || option.value || ""
                    }))
                : []
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
        let handle = 0;
        const debounced = (...args) => {
            window.clearTimeout(handle);
            handle = window.setTimeout(() => {
                handle = 0;
                callback(...args);
            }, delayMs);
        };

        debounced.cancel = () => {
            window.clearTimeout(handle);
            handle = 0;
        };

        return debounced;
    }

    function now() {
        return typeof performance !== "undefined" && typeof performance.now === "function"
            ? performance.now()
            : Date.now();
    }

    function createWorkbenchMetrics() {
        return {
            renderCount: 0,
            totalRenderDurationMs: 0,
            lastRenderDurationMs: 0,
            maxRenderDurationMs: 0,
            frameLayerRebuildCount: 0,
            linkLayerRebuildCount: 0,
            nodeLayerRebuildCount: 0,
            lastRenderedFrameCount: 0,
            lastRenderedLinkCount: 0,
            lastRenderedNodeCount: 0,
            lastVisibleNodeCount: 0,
            statePublishRequestCount: 0,
            statePublishImmediateCount: 0,
            statePublishCommitCount: 0,
            viewportCommitScheduleCount: 0,
            viewportCommitCount: 0,
            lastStatePublishMode: "",
            lastCommittedStateSize: 0,
            movePublishRequestCount: 0,
            movePublishSuccessCount: 0,
            movePublishFailureCount: 0,
            lastMovePublishStatus: "",
            lastResolvedDragDeltaX: 0,
            lastResolvedDragDeltaY: 0,
            lastReleasedInteractionKind: "",
            lastReleasedInteractionMoved: false,
            dragPatchCount: 0,
            totalDragPatchedNodeCount: 0,
            totalDragPatchedLinkCount: 0,
            totalDragPatchedFrameCount: 0,
            lastDragPatchedNodeCount: 0,
            lastDragPatchedLinkCount: 0,
            lastDragPatchedFrameCount: 0
        };
    }

    function formatMetricDuration(value) {
        if (typeof value !== "number" || !Number.isFinite(value)) {
            return "0 ms";
        }

        return `${round(value)} ms`;
    }

    function cloneWorkbenchMetrics(metrics) {
        return {
            renderCount: metrics?.renderCount || 0,
            totalRenderDurationMs: round(metrics?.totalRenderDurationMs || 0),
            lastRenderDurationMs: round(metrics?.lastRenderDurationMs || 0),
            maxRenderDurationMs: round(metrics?.maxRenderDurationMs || 0),
            frameLayerRebuildCount: metrics?.frameLayerRebuildCount || 0,
            linkLayerRebuildCount: metrics?.linkLayerRebuildCount || 0,
            nodeLayerRebuildCount: metrics?.nodeLayerRebuildCount || 0,
            lastRenderedFrameCount: metrics?.lastRenderedFrameCount || 0,
            lastRenderedLinkCount: metrics?.lastRenderedLinkCount || 0,
            lastRenderedNodeCount: metrics?.lastRenderedNodeCount || 0,
            lastVisibleNodeCount: metrics?.lastVisibleNodeCount || 0,
            statePublishRequestCount: metrics?.statePublishRequestCount || 0,
            statePublishImmediateCount: metrics?.statePublishImmediateCount || 0,
            statePublishCommitCount: metrics?.statePublishCommitCount || 0,
            viewportCommitScheduleCount: metrics?.viewportCommitScheduleCount || 0,
            viewportCommitCount: metrics?.viewportCommitCount || 0,
            lastStatePublishMode: metrics?.lastStatePublishMode || "",
            lastCommittedStateSize: metrics?.lastCommittedStateSize || 0,
            movePublishRequestCount: metrics?.movePublishRequestCount || 0,
            movePublishSuccessCount: metrics?.movePublishSuccessCount || 0,
            movePublishFailureCount: metrics?.movePublishFailureCount || 0,
            lastMovePublishStatus: metrics?.lastMovePublishStatus || "",
            lastResolvedDragDeltaX: round(metrics?.lastResolvedDragDeltaX || 0),
            lastResolvedDragDeltaY: round(metrics?.lastResolvedDragDeltaY || 0),
            lastReleasedInteractionKind: metrics?.lastReleasedInteractionKind || "",
            lastReleasedInteractionMoved: !!metrics?.lastReleasedInteractionMoved,
            dragPatchCount: metrics?.dragPatchCount || 0,
            totalDragPatchedNodeCount: metrics?.totalDragPatchedNodeCount || 0,
            totalDragPatchedLinkCount: metrics?.totalDragPatchedLinkCount || 0,
            totalDragPatchedFrameCount: metrics?.totalDragPatchedFrameCount || 0,
            lastDragPatchedNodeCount: metrics?.lastDragPatchedNodeCount || 0,
            lastDragPatchedLinkCount: metrics?.lastDragPatchedLinkCount || 0,
            lastDragPatchedFrameCount: metrics?.lastDragPatchedFrameCount || 0
        };
    }

    function incrementMetric(metrics, key) {
        if (!metrics || typeof metrics[key] !== "number") {
            return;
        }

        metrics[key] += 1;
    }

    function resetLastDragPatchMetrics(metrics) {
        if (!metrics) {
            return;
        }

        metrics.lastDragPatchedNodeCount = 0;
        metrics.lastDragPatchedLinkCount = 0;
        metrics.lastDragPatchedFrameCount = 0;
    }

    function recordDragPatchMetrics(metrics, nodeCount, linkCount, frameCount) {
        if (!metrics) {
            return;
        }

        metrics.dragPatchCount += 1;
        metrics.totalDragPatchedNodeCount += nodeCount;
        metrics.totalDragPatchedLinkCount += linkCount;
        metrics.totalDragPatchedFrameCount += frameCount;
        metrics.lastDragPatchedNodeCount = nodeCount;
        metrics.lastDragPatchedLinkCount = linkCount;
        metrics.lastDragPatchedFrameCount = frameCount;
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
            setupRendererKey: action?.setupRendererKey || "",
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

    function normalizeCompactPath(path) {
        const fullPath = typeof path?.fullPath === "string" ? path.fullPath.trim() : "";
        if (!fullPath) {
            return null;
        }

        return {
            label: path?.label || "Path",
            displayText: path?.displayText || fullPath,
            fullPath,
            promotedText: path?.promotedText || ""
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

    function normalizePortCollection(ports, defaultSide) {
        if (!Array.isArray(ports)) {
            return [];
        }

        return ports
            .filter(Boolean)
            .map(port => ({
                id: (port?.id || port?.label || defaultSide || "port").toString(),
                label: (port?.label || port?.id || "Port").toString(),
                side: (port?.side || defaultSide || "").toString().trim().toLowerCase() || defaultSide,
                tone: (port?.tone || "neutral").toString(),
                categoryKey: (port?.categoryKey || "").toString(),
                accentColor: (port?.accentColor || "").toString(),
                kind: (port?.kind || "").toString(),
                isRequired: !!port?.isRequired
            }));
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
            dependencySourceId: surface?.dependencySourceId || "",
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
                compactPath: normalizeCompactPath(node?.compactPath),
                progressMode: node?.progressMode || "na",
                progressPercent: normalizeProgressPercent(node?.progressPercent),
                markerIcon: node?.markerIcon || "",
                markerTone: node?.markerTone || "",
                markerLabel: node?.markerLabel || "",
                priority: typeof node?.priority === "number" ? clamp(Math.round(node.priority), 0, 6) : 0,
                annotations: Array.isArray(node?.annotations) ? node.annotations.map(normalizeAnnotation) : [],
                inputPorts: normalizePortCollection(node?.inputPorts, "left"),
                outputPorts: normalizePortCollection(node?.outputPorts, "right")
            })) : [],
            links: Array.isArray(surface?.links) ? surface.links.map(link => ({
                ...link,
                sourcePortId: link?.sourcePortId || "",
                targetPortId: link?.targetPortId || "",
                kind: link?.kind || "",
                label: link?.label || "",
                summary: link?.summary || "",
                tone: link?.tone || "neutral",
                isUserAuthored: !!link?.isUserAuthored
            })) : [],
            uiState: {
                version: surface?.uiState?.version || "canvas-workbench.v1",
                selectedNodeIds: normalizedSelection.selectedNodeIds,
                highlightedNodeIds: Array.isArray(surface?.uiState?.highlightedNodeIds)
                    ? [...new Set(surface.uiState.highlightedNodeIds.filter(Boolean))]
                    : [],
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
                collapseOnDoubleClick: surface?.chrome?.collapseOnDoubleClick !== false,
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

    function normalizeInlineTextParagraphs(text) {
        const normalized = typeof text === "string"
            ? text.replace(/\r\n/g, "\n").replace(/\r/g, "\n")
            : "";
        const paragraphs = normalized.split("\n");
        return paragraphs.length > 0
            ? paragraphs
            : [""];
    }

    function countInlineTextNodeMetaItems(node) {
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

    function measureInlineTextNodeParagraphs(measureService, text, maxWidth, font, maxLines) {
        const paragraphs = normalizeInlineTextParagraphs(text);
        const lineHeightPx = Math.max(16, font?.lineHeightPx || 20);
        const lineLimit = Math.max(1, maxLines || 1);
        let remainingLines = lineLimit;
        let lineCount = 0;
        let estimatedWidth = 0;
        let isTruncated = false;

        for (let index = 0; index < paragraphs.length; index += 1) {
            if (remainingLines <= 0) {
                isTruncated = true;
                break;
            }

            const paragraph = paragraphs[index];
            if (!paragraph.trim()) {
                lineCount += 1;
                remainingLines -= 1;
                continue;
            }

            const measure = measureService.measure({
                text: paragraph,
                maxWidth,
                maxLines: remainingLines,
                font
            });
            const measuredLineCount = Math.max(1, measure?.lineCount || 1);
            lineCount += measuredLineCount;
            remainingLines -= measuredLineCount;
            estimatedWidth = Math.max(estimatedWidth, measure?.estimatedWidth || 0);

            if (measure?.isTruncated) {
                isTruncated = true;
                break;
            }
        }

        return {
            estimatedWidth,
            estimatedHeight: Math.ceil(Math.max(1, lineCount) * lineHeightPx),
            lineCount: Math.max(1, lineCount),
            isTruncated
        };
    }

    function estimateInlineTextNodeSize(node, baseSize, measureService) {
        const noteText = node.inlineText || node.title || node.subtitle || "Write note";
        const tokens = noteText.split(/\s+/).filter(Boolean);
        const longestTokenLength = tokens.reduce((longest, token) => Math.max(longest, token.length), 0);
        const noteFont = {
            family: "\"DM Sans\", sans-serif",
            sizePx: 14,
            weight: 700,
            lineHeightPx: 20
        };
        const minWidth = 148;
        const maxWidth = 348;
        const maxLines = 12;
        const widthBias = Math.max(0, Math.min(132, (noteText.length - 18) * 1.45));
        const longWordBias = Math.max(0, longestTokenLength - 12) * 4.5;
        let estimatedWidth = clamp(
            Math.ceil(baseSize.width + widthBias + longWordBias),
            minWidth,
            maxWidth);
        let textMeasure = measureInlineTextNodeParagraphs(
            measureService,
            noteText,
            Math.max(108, estimatedWidth - 40),
            noteFont,
            maxLines);
        estimatedWidth = clamp(
            Math.ceil(Math.max(minWidth, textMeasure.estimatedWidth + 40)),
            minWidth,
            maxWidth);
        textMeasure = measureInlineTextNodeParagraphs(
            measureService,
            noteText,
            Math.max(108, estimatedWidth - 40),
            noteFont,
            maxLines);

        const metaRows = Math.max(1, Math.ceil(countInlineTextNodeMetaItems(node) / 4));
        const metaHeight = 26 + ((metaRows - 1) * 18);
        const estimatedHeight = clamp(
            Math.ceil(30 + textMeasure.estimatedHeight + metaHeight),
            76,
            304);

        return {
            width: estimatedWidth,
            height: estimatedHeight
        };
    }

    function resolveBaseNodeSize(node) {
        if (node.isInlineTextNode) {
            return { width: 164, height: 76 };
        }

        if ((node?.family || "").toLowerCase() === "workflow-decision" ||
            (node?.paletteKey || "").toLowerCase() === "workflow-decision") {
            return { width: 178, height: 178 };
        }

        const inputPorts = Array.isArray(node?.inputPorts) ? node.inputPorts : [];
        const outputPorts = Array.isArray(node?.outputPorts) ? node.outputPorts : [];
        const portRows = Math.max(inputPorts.length, outputPorts.length);
        if (portRows > 0) {
            return {
                width: 336,
                height: Math.max(212, 188 + (Math.max(0, portRows - 1) * 28))
            };
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

        if (node.isInlineTextNode) {
            return estimateInlineTextNodeSize(node, baseSize, measureService);
        }

        const family = (node.family || "item").toLowerCase();
        if (family === "workflow-decision" || (node.paletteKey || "").toLowerCase() === "workflow-decision") {
            return baseSize;
        }

        const titleText = node.title || "Untitled";
        const subtitleText = node.subtitle || node.leadText || "";
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
            maxLines: 2,
            font: {
                family: "\"DM Sans\", sans-serif",
                sizePx: family === "root" ? 18 : 15,
                weight: 700,
                lineHeightPx: family === "root" ? 22 : 18
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
        const inputPorts = Array.isArray(node?.inputPorts) ? node.inputPorts : [];
        const outputPorts = Array.isArray(node?.outputPorts) ? node.outputPorts : [];
        const longestInputPort = inputPorts.reduce((longest, port) => Math.max(longest, (port?.label || port?.id || "").length), 0);
        const longestOutputPort = outputPorts.reduce((longest, port) => Math.max(longest, (port?.label || port?.id || "").length), 0);
        const portRows = Math.max(inputPorts.length, outputPorts.length);
        const portWidthBudget = portRows > 0
            ? Math.min(420, Math.max(baseSize.width, 224 + (longestInputPort + longestOutputPort) * 5.8))
            : 0;
        const portHeight = portRows > 0
            ? Math.max(52, 28 + (portRows * 26))
            : 0;
        const annotationHeight = Array.isArray(node.annotations) ? Math.ceil(node.annotations.length / 2) * 14 : 0;
        const footerHeight = (Array.isArray(node.chips) && node.chips.length > 0 ? 18 : 0) +
            (Array.isArray(node.footerChips) && node.footerChips.length > 0 ? 18 : 0);
        const mediaHeight = node.mediaPreviewUrl ? 62 : 0;
        const bodyHeight = 92 +
            titleMeasure.estimatedHeight +
            (subtitleMeasure ? subtitleMeasure.estimatedHeight + 8 : 0) +
            annotationHeight +
            footerHeight +
            mediaHeight +
            portHeight;
        const estimatedWidth = Math.ceil(Math.max(
            baseSize.width,
            portWidthBudget,
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

    function getProjectionOverscanPx(state, hostRect) {
        const rect = hostRect || state.host?.getBoundingClientRect?.() || { width: 0, height: 0 };
        const baseOverscan = Math.max(180, Math.min(rect.width || 0, rect.height || 0) * 0.24);
        return state?.interaction
            ? Math.max(baseOverscan, 320)
            : baseOverscan;
    }

    function collectProjectedContextNodeIds(state) {
        const contextNodeIds = new Set(state.ui?.selectedNodeIds || []);
        if (Array.isArray(state.interaction?.nodeIds)) {
            for (const nodeId of state.interaction.nodeIds) {
                contextNodeIds.add(nodeId);
            }
        }

        if (state.hoveredNodeId) {
            contextNodeIds.add(state.hoveredNodeId);
        }

        for (const nodeId of [...contextNodeIds]) {
            let current = state.lookups.byId.get(nodeId) || null;
            while (current?.parentId) {
                contextNodeIds.add(current.parentId);
                current = state.lookups.byId.get(current.parentId) || null;
            }
        }

        return contextNodeIds;
    }

    function isNodeProjectedInViewport(state, node, visibleNodes, overscanPx, projectionContext) {
        const rect = projectionContext?.hostRect || state.host?.getBoundingClientRect?.();
        if (!rect) {
            return true;
        }

        const projectPoint = shared.worldToHostPoint;
        if (typeof projectPoint !== "function") {
            return true;
        }

        const resolved = projectionContext?.layoutPositions?.get?.(node.id);
        const nodePosition = resolved
            ? { x: resolved.x, y: resolved.y }
            : getNodePosition(state, node, visibleNodes);
        const position = projectPoint(state, nodePosition);
        const size = getNodeSize(state, node);
        const halfWidth = (size.width * state.ui.zoom) / 2;
        const halfHeight = (size.height * state.ui.zoom) / 2;
        return position.x + halfWidth >= -overscanPx &&
            position.x - halfWidth <= rect.width + overscanPx &&
            position.y + halfHeight >= -overscanPx &&
            position.y - halfHeight <= rect.height + overscanPx;
    }

    function getProjectedNodes(state, visibleNodes) {
        if (!Array.isArray(visibleNodes) || visibleNodes.length === 0) {
            return [];
        }

        const layoutPositions = ensureLayoutPositions(state, visibleNodes);
        const hostRect = state.host?.getBoundingClientRect?.();
        const overscanPx = getProjectionOverscanPx(state, hostRect);
        const contextNodeIds = collectProjectedContextNodeIds(state);
        const projectionContext = { hostRect, layoutPositions };
        const projectedNodes = visibleNodes.filter(node =>
            contextNodeIds.has(node.id) ||
            isNodeProjectedInViewport(state, node, visibleNodes, overscanPx, projectionContext));
        if (projectedNodes.length > 0) {
            return projectedNodes;
        }

        const fallbackNodeId = state.ui?.selectedNodeIds?.[0] || visibleNodes[0]?.id || null;
        const fallbackNode = fallbackNodeId
            ? visibleNodes.find(node => node.id === fallbackNodeId) || visibleNodes[0]
            : visibleNodes[0];
        return fallbackNode ? [fallbackNode] : [];
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
        if (Array.isArray(state.interaction?.nodeIds) &&
            state.interaction.nodeIds.includes(node.id)) {
            return 1;
        }

        if ((node.family || "").toLowerCase() === "root") {
            return 0.04;
        }

        if (node.isRequired) {
            return 0.18;
        }

        if (state.ui.manualPositions?.[node.id]) {
            return 0.08;
        }

        return 1;
    }

    function buildResolvedLayoutKey(state, visibleNodes) {
        return visibleNodes.map(node => {
            const base = getBaseNodePosition(state, node);
            const size = getNodeSize(state, node);
            const isActiveDragNode = Array.isArray(state.interaction?.nodeIds) &&
                state.interaction.nodeIds.includes(node.id);
            return [
                node.id,
                node.parentId || "",
                round(base.x),
                round(base.y),
                round(size.width),
                round(size.height),
                node.family || "",
                node.isInlineTextNode ? "1" : "0",
                isActiveDragNode ? "1" : "0"
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
                mobility: getNodeMobility(state, node),
                isActiveDragNode: Array.isArray(state.interaction?.nodeIds) &&
                    state.interaction.nodeIds.includes(node.id)
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

        const firstActive = !!first.isActiveDragNode;
        const secondActive = !!second.isActiveDragNode;
        let firstDelta = 0;
        let secondDelta = 0;

        if (firstActive !== secondActive) {
            firstDelta = firstActive ? amount : 0;
            secondDelta = secondActive ? amount : 0;
        }
        else {
            const totalMobility = Math.max(0.001, first.mobility + second.mobility);
            firstDelta = amount * (first.mobility / totalMobility);
            secondDelta = amount * (second.mobility / totalMobility);
        }

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

    function separateOverlappingPairs(items, positions) {
        let moved = false;

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

        return moved;
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
            moved = separateOverlappingPairs(items, positions) || moved;
            moved = relaxTowardBase(items, positions) || moved;
            if (!moved) {
                break;
            }
        }

        enforceParentClearance(itemsById, positions);
        enforceSiblingSpacing(itemsById, positions);
        for (let iteration = 0; iteration < 6; iteration++) {
            if (!separateOverlappingPairs(items, positions)) {
                break;
            }
        }

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
        state.sceneBounds = null;
        state.sceneBoundsKey = "";
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

        const positions = ensureLayoutPositions(state, nodes);
        if (state.sceneBounds && state.sceneBoundsKey === state.layoutKey) {
            return state.sceneBounds;
        }

        let minX = Number.POSITIVE_INFINITY;
        let maxX = Number.NEGATIVE_INFINITY;
        let minY = Number.POSITIVE_INFINITY;
        let maxY = Number.NEGATIVE_INFINITY;

        for (const node of nodes) {
            const resolved = positions.get(node.id);
            const position = resolved
                ? { x: resolved.x, y: resolved.y }
                : getBaseNodePosition(state, node);
            const size = getNodeSize(state, node);
            minX = Math.min(minX, position.x - (size.width / 2));
            maxX = Math.max(maxX, position.x + (size.width / 2));
            minY = Math.min(minY, position.y - (size.height / 2));
            maxY = Math.max(maxY, position.y + (size.height / 2));
        }

        state.sceneBounds = { minX, maxX, minY, maxY };
        state.sceneBoundsKey = state.layoutKey;
        return state.sceneBounds;
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

    function setPan(state, panX, panY, zoom, options) {
        if (options?.skipClamp) {
            state.ui.panX = round(panX);
            state.ui.panY = round(panY);
            return;
        }

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
            highlightedNodeIds: [...(state.highlightedIds || new Set())],
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

    function legacyApplySceneTransform(state) {
        state.scene.style.transform = `translate(${state.ui.panX}px, ${state.ui.panY}px) scale(${state.ui.zoom})`;
    }

    function cancelViewportAnimation(state) {
        state.animationTimeline?.cancel?.("viewport");
    }

    function applySceneTransformNow(state) {
        const applySceneTransformFn = shared.applySceneTransform;
        if (typeof applySceneTransformFn === "function") {
            applySceneTransformFn(state);
            return;
        }

        legacyApplySceneTransform(state);
    }

    function renderNow(state) {
        const renderFn = shared.render;
        if (typeof renderFn === "function") {
            renderFn(state);
        }
    }

    function publishStateNow(state) {
        const publishStateFn = shared.publishState;
        if (typeof publishStateFn === "function") {
            publishStateFn(state);
        }
    }

    function updateViewportTransform(state, viewport, options) {
        state.ui.zoom = viewport.zoom;
        setPan(state, viewport.panX, viewport.panY, viewport.zoom, options);
        applySceneTransformNow(state);
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
            updateViewportTransform(state, target, options);
            renderNow(state);
            if (options?.publish !== false) {
                publishStateNow(state);
            }

            return false;
        }

        if (!state.animationTimeline) {
            updateViewportTransform(state, target, options);
            renderNow(state);
            if (options?.publish !== false) {
                publishStateNow(state);
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
                updateViewportTransform(state, next, options);
            },
            complete() {
                renderNow(state);
                if (options?.publish !== false) {
                    publishStateNow(state);
                }
            }
        });

        return true;
    }

    const shared = root.canvasWorkbenchModule = root.canvasWorkbenchModule || {};
    Object.assign(shared, { contextSubmenuHoverDelayMs, MIN_ZOOM, MAX_ZOOM, selectionModel, getRequiredRootService, getTextMeasureService, getViewportControllerService, getAnimationTimelineService, clear, createElement, createSvgElement, normalizeInputField, normalizeInputValue, clamp, debounce, now, createWorkbenchMetrics, formatMetricDuration, cloneWorkbenchMetrics, incrementMetric, resetLastDragPatchMetrics, recordDragPatchMetrics, round, normalizeAction, normalizeAnnotation, normalizeCompactPath, normalizeDiagnosticsOptions, normalizeMinimapOptions, normalizeClipboardOptions, normalizeTooltipPopoverOptions, normalizeMarqueeOptions, normalizeSnapGuideOptions, normalizeConnectorAnchorOptions, normalizeTransformHandleOptions, normalizeGroupFrame, normalizeProgressPercent, normalizeMenuActionScale, normalizeSurface, toSelectionSet, toCollapsedSet, getDefaultNodeSize, resolveBaseNodeSize, estimateNodeSizeFromText, getNodeSize, buildNodeLookup, isNodeVisible, getVisibleNodes, getProjectionOverscanPx, collectProjectedContextNodeIds, isNodeProjectedInViewport, getProjectedNodes, getBaseNodePosition, getNodeDepth, getNodeMobility, buildResolvedLayoutKey, buildLayoutItems, getCollisionPaddingX, getCollisionPaddingY, getOverlapDelta, chooseCollisionAxis, resolveCollisionDirection, applyCollisionSeparation, enforceParentClearance, enforceSiblingSpacing, relaxTowardBase, computeResolvedNodePositions, ensureLayoutPositions, getNodePosition, getSceneBounds, clampPanToScene, setPan, syncMenuScaleCss, serializeState, legacyApplySceneTransform, cancelViewportAnimation, updateViewportTransform, animateViewportTransition });
})();
