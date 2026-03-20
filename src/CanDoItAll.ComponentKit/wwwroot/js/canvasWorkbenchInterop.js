(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};

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
            requiresInput: !!action?.requiresInput,
            createMode: action?.createMode || "command",
            objectSubtype: action?.objectSubtype || "",
            titleLabel: action?.titleLabel || "Title",
            titlePlaceholder: action?.titlePlaceholder || "",
            subtitleLabel: action?.subtitleLabel || "Subtitle",
            subtitlePlaceholder: action?.subtitlePlaceholder || "",
            notesLabel: action?.notesLabel || "Notes",
            notesPlaceholder: action?.notesPlaceholder || "",
            requiresFile: !!action?.requiresFile,
            acceptedFileTypes: action?.acceptedFileTypes || "",
            filePrompt: action?.filePrompt || "Drop a file here or choose one.",
            supportsDragDrop: action?.supportsDragDrop !== false,
            children: Array.isArray(action?.children) ? action.children.map(normalizeAction) : []
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

    function normalizeSurface(surface) {
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
                progressMode: node?.progressMode || "na",
                progressPercent: normalizeProgressPercent(node?.progressPercent)
            })) : [],
            links: Array.isArray(surface?.links) ? surface.links : [],
            uiState: {
                version: surface?.uiState?.version || "canvas-workbench.v1",
                selectedNodeIds: Array.isArray(surface?.uiState?.selectedNodeIds) ? [...surface.uiState.selectedNodeIds] : [],
                collapsedNodeIds: Array.isArray(surface?.uiState?.collapsedNodeIds) ? [...surface.uiState.collapsedNodeIds] : [],
                groupFrames: Array.isArray(surface?.uiState?.groupFrames) ? surface.uiState.groupFrames.map(normalizeGroupFrame) : [],
                manualPositions: surface?.uiState?.manualPositions || {},
                zoom: typeof surface?.uiState?.zoom === "number" ? clamp(surface.uiState.zoom, 0.55, 1.75) : 1,
                panX: typeof surface?.uiState?.panX === "number" ? surface.uiState.panX : 90,
                panY: typeof surface?.uiState?.panY === "number" ? surface.uiState.panY : 110,
                isMaximized: !!surface?.uiState?.isMaximized,
                activeInspectorTab: surface?.uiState?.activeInspectorTab || ""
            },
            chrome: {
                quickCreateActions: Array.isArray(surface?.chrome?.quickCreateActions) ? surface.chrome.quickCreateActions.map(normalizeAction) : [],
                groupContextActions: Array.isArray(surface?.chrome?.groupContextActions) ? surface.chrome.groupContextActions.map(normalizeAction) : [],
                showQuickCreateRail: surface?.chrome?.showQuickCreateRail !== false,
                childNoteActionId: surface?.chrome?.childNoteActionId || "",
                siblingNoteActionId: surface?.chrome?.siblingNoteActionId || "",
                inlineNotePlaceholder: surface?.chrome?.inlineNotePlaceholder || "Write note"
            }
        };
    }

    function toSelectionSet(selectedNodeIds) {
        return new Set((selectedNodeIds || []).filter(Boolean));
    }

    function toCollapsedSet(collapsedNodeIds) {
        return new Set((collapsedNodeIds || []).filter(Boolean));
    }

    function getNodeSize(node) {
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

    function getNodePosition(state, node) {
        const manual = state.ui.manualPositions?.[node.id];
        return manual && typeof manual.x === "number" && typeof manual.y === "number"
            ? { x: manual.x, y: manual.y }
            : { x: node.x, y: node.y };
    }

    function getSceneBounds(state, visibleNodes) {
        const nodes = Array.isArray(visibleNodes) ? visibleNodes : getVisibleNodes(state);
        if (!nodes.length) {
            return null;
        }

        let minX = Number.POSITIVE_INFINITY;
        let maxX = Number.NEGATIVE_INFINITY;
        let minY = Number.POSITIVE_INFINITY;
        let maxY = Number.NEGATIVE_INFINITY;

        for (const node of nodes) {
            const position = getNodePosition(state, node);
            const size = getNodeSize(node);
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
        if (!bounds || rect.width <= 0 || rect.height <= 0) {
            return { x: panX, y: panY };
        }

        const nextZoom = zoom || state.ui.zoom;
        const marginX = Math.min(176, Math.max(72, rect.width * 0.16));
        const marginY = Math.min(160, Math.max(64, rect.height * 0.16));
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

    function serializeState(state) {
        return JSON.stringify({
            version: state.ui.version || "canvas-workbench.v1",
            selectedNodeIds: [...state.selectedIds],
            collapsedNodeIds: [...state.collapsedIds],
            groupFrames: Array.isArray(state.ui.groupFrames) ? state.ui.groupFrames.map(normalizeGroupFrame) : [],
            manualPositions: state.ui.manualPositions || {},
            zoom: round(state.ui.zoom),
            panX: round(state.ui.panX),
            panY: round(state.ui.panY),
            isMaximized: !!state.ui.isMaximized,
            activeInspectorTab: state.ui.activeInspectorTab || ""
        });
    }

    function applySceneTransform(state) {
        state.scene.style.transform = `translate(${state.ui.panX}px, ${state.ui.panY}px) scale(${state.ui.zoom})`;
    }

    function getLinkAnchorPoint(state, node, side) {
        const position = getNodePosition(state, node);
        const size = getNodeSize(node);
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

            const sourcePosition = getNodePosition(state, source);
            const targetPosition = getNodePosition(state, target);
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
                const position = getNodePosition(state, node);
                const size = getNodeSize(node);
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
            label.appendChild(createElement(state.document, "span", "cw-group-frame__count", `${memberNodes.length}`));
            frameElement.appendChild(label);
            state.frameLayer.appendChild(frameElement);
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

    function renderInlineTextNode(state, node, nodeElement) {
        nodeElement.classList.add("is-inline-text");
        const surface = createElement(state.document, "div", "cw-node__surface");
        const noteText = node.inlineText || node.title || node.leadText || "Write note";
        surface.appendChild(createElement(state.document, "p", "cw-note-node__text", noteText));

        if (node.statusPill || node.progressMode) {
            const meta = createElement(state.document, "div", "cw-note-node__meta");
            meta.appendChild(createProgressMarker(state, node));
            if (node.statusPill) {
                meta.appendChild(createElement(state.document, "span", "cw-node__chip tone-accent", node.statusPill));
            }
            surface.appendChild(meta);
        }

        nodeElement.appendChild(surface);
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
        rightMeta.appendChild(createProgressMarker(state, node));
        if (node.durationLabel) {
            rightMeta.appendChild(createElement(state.document, "span", "cw-node__pill", node.durationLabel));
        }

        if (node.statusPill) {
            rightMeta.appendChild(createElement(state.document, "span", "cw-node__pill", node.statusPill));
        }

        header.appendChild(rightMeta);
        surface.appendChild(header);
        surface.appendChild(createElement(state.document, "h3", "cw-node__title", node.title || "Untitled"));

        if (node.subtitle) {
            surface.appendChild(createElement(state.document, "p", "cw-node__subtitle", node.subtitle));
        }

        if (node.leadText) {
            surface.appendChild(createElement(state.document, "p", "cw-node__lead", node.leadText));
        }

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

        for (const node of visibleNodes) {
            const position = getNodePosition(state, node);
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

    function getHostPoint(state, clientX, clientY) {
        const rect = state.host.getBoundingClientRect();
        return {
            x: clientX - rect.left,
            y: clientY - rect.top
        };
    }

    function worldToHostPoint(state, point) {
        return {
            x: (point.x * state.ui.zoom) + state.ui.panX,
            y: (point.y * state.ui.zoom) + state.ui.panY
        };
    }

    function getWorldPoint(state, clientX, clientY) {
        const hostPoint = getHostPoint(state, clientX, clientY);
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

    function isOverlayTarget(target) {
        return !!target?.closest?.(".cw-context-menu, .cw-canvas-composer");
    }

    function publishSelection(state) {
        const selectedNodeIds = [...state.selectedIds];
        const primaryNodeId = selectedNodeIds[0] || null;
        state.dotNetRef.invokeMethodAsync("OnSelectionChanged", primaryNodeId, JSON.stringify(selectedNodeIds));
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
        state.selectedIds = toSelectionSet(nodeIds);
        state.ui.selectedNodeIds = keepOrderPrimary ? [...nodeIds] : [...state.selectedIds];
        render(state);
        publishSelection(state);
        publishState(state);
    }

    function toggleSelection(state, nodeId) {
        if (state.selectedIds.has(nodeId)) {
            state.selectedIds.delete(nodeId);
        }
        else {
            state.selectedIds.add(nodeId);
        }

        state.ui.selectedNodeIds = [...state.selectedIds];
        render(state);
        publishSelection(state);
        publishState(state);
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
        applySceneTransform(state);
        const visibleNodes = getVisibleNodes(state);
        renderGroupFrames(state, visibleNodes);
        renderLinks(state, visibleNodes);
        renderNodes(state, visibleNodes);
        layoutComposer(state);
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
            placementKind: placementKind || (sourceNode ? "child" : "canvas"),
            createMode: action.createMode || (action.requiresInput ? "dialog" : "command")
        };
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
        const radiusStart = typeof baseRadius === "number" ? baseRadius : 96;
        const radiusStep = typeof ringStep === "number" ? ringStep : 84;

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

    function getContextMenuLayerBounds(originOffset, offsets, radius) {
        const actionHalfWidth = 58;
        const actionHalfHeight = 54;
        const coreHalf = 44;
        const coreLabelAllowance = 34;
        const padding = 14;
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

        for (const offset of offsets || []) {
            const centerX = originOffset.x + offset.x;
            const centerY = originOffset.y + offset.y;
            bounds.minX = Math.min(bounds.minX, centerX - actionHalfWidth);
            bounds.maxX = Math.max(bounds.maxX, centerX + actionHalfWidth);
            bounds.minY = Math.min(bounds.minY, centerY - actionHalfHeight);
            bounds.maxY = Math.max(bounds.maxY, centerY + actionHalfHeight);
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

    function positionContextMenu(state, center, offsets) {
        const hostRect = state.host.getBoundingClientRect();
        const radius = getContextMenuOrbitRadius(offsets || []);
        const bounds = getContextMenuLayerBounds({ x: 0, y: 0 }, offsets || [], radius);
        const x = round(clamp(center.x, -bounds.minX, Math.max(-bounds.minX, hostRect.width - bounds.maxX)));
        const y = round(clamp(center.y, -bounds.minY, Math.max(-bounds.minY, hostRect.height - bounds.maxY)));
        state.contextMenu.style.left = `${x}px`;
        state.contextMenu.style.top = `${y}px`;
        return { x, y };
    }

    function getContextMenuOrbitRadius(offsets) {
        let radius = 92;

        for (const offset of offsets || []) {
            radius = Math.max(radius, Math.hypot(offset.x, offset.y) + 58);
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

        const dx = localPoint.x - layerState.originOffset.x;
        const dy = localPoint.y - layerState.originOffset.y;
        return Math.hypot(dx, dy) <= layerState.radius;
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
        if (!layers?.length || layers.length < 2) {
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

    function resolveSubmenuOrigin(parentLayer, offset) {
        const length = Math.hypot(offset.x, offset.y) || 1;
        const outwardDistance = Math.max(108, round(parentLayer.radius * 0.34));
        return {
            x: round(parentLayer.originOffset.x + offset.x + ((offset.x / length) * outwardDistance)),
            y: round(parentLayer.originOffset.y + offset.y + ((offset.y / length) * outwardDistance))
        };
    }

    function clampLayerOriginToHost(state, originOffset, offsets, radius) {
        const bounds = getContextMenuLayerBounds(originOffset, offsets || [], radius);
        const shift = clampLayerBoundsToHost(state, bounds);
        return {
            x: round(originOffset.x + shift.x),
            y: round(originOffset.y + shift.y)
        };
    }

    function createContextMenuLayer(state, options) {
        const layer = createElement(state.document, "div", `cw-context-menu__layer${options.depth > 0 ? " is-submenu" : ""}`);
        layer.style.zIndex = `${options.depth + 1}`;

        const orbit = createElement(state.document, "div", `cw-context-menu__orbit${options.depth > 0 ? " is-submenu" : ""}`);
        orbit.style.setProperty("--cw-orbit-x", `${options.originOffset.x}px`);
        orbit.style.setProperty("--cw-orbit-y", `${options.originOffset.y}px`);
        orbit.addEventListener("pointerdown", event => event.stopPropagation());

        const core = createElement(state.document, "div", `cw-context-menu__core${options.depth > 0 ? " is-submenu" : ""}`);
        core.appendChild(createElement(state.document, "span", "cw-context-menu__core-dot"));
        core.appendChild(createElement(state.document, "span", "cw-context-menu__core-label", options.label || "Canvas"));
        orbit.appendChild(core);
        layer.appendChild(orbit);

        return {
            depth: options.depth,
            element: layer,
            orbit,
            originOffset: options.originOffset,
            radius: 0,
            ownerActionId: options.ownerActionId || "",
            ownerDepth: typeof options.ownerDepth === "number" ? options.ownerDepth : -1
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

            submitCreateRequest(state, {
                ...state.composer.request,
                title: state.composer.titleInput.value.trim(),
                subtitle: state.composer.subtitleInput.value.trim(),
                notes: state.composer.notesInput.value.trim(),
                objectSubtype: state.composer.request.objectSubtype || "",
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
            composer.createButton.disabled = !!composer.requiresFile && !composer.uploadedFile;
        }
    }

    function openCreateComposer(state, action, request) {
        clearContextMenu(state);
        closeComposer(state, { focusHost: false });

        const shell = decorateComposerShell(state, `Create ${action.label || "item"}`, action.label || "Create", "dialog");
        const fields = createElement(state.document, "div", "cw-canvas-composer__fields");

        const titleField = createElement(state.document, "label", "cw-canvas-composer__field");
        titleField.appendChild(createElement(state.document, "span", null, action.titleLabel || "Title"));
        const titleInput = createElement(state.document, "input", "cw-canvas-composer__input");
        titleInput.type = "text";
        titleInput.value = request?.title || "";
        titleInput.placeholder = action.titlePlaceholder || "";
        titleField.appendChild(titleInput);
        fields.appendChild(titleField);

        const subtitleField = createElement(state.document, "label", "cw-canvas-composer__field");
        subtitleField.appendChild(createElement(state.document, "span", null, action.subtitleLabel || "Subtitle"));
        const subtitleInput = createElement(state.document, "input", "cw-canvas-composer__input");
        subtitleInput.type = "text";
        subtitleInput.value = request?.subtitle || "";
        subtitleInput.placeholder = action.subtitlePlaceholder || "";
        subtitleField.appendChild(subtitleInput);
        fields.appendChild(subtitleField);

        const notesField = createElement(state.document, "label", "cw-canvas-composer__field");
        notesField.appendChild(createElement(state.document, "span", null, action.notesLabel || "Notes"));
        const notesInput = createElement(state.document, "textarea", "cw-canvas-composer__textarea");
        notesInput.value = request?.notes || "";
        notesInput.placeholder = action.notesPlaceholder || "";
        notesField.appendChild(notesInput);
        fields.appendChild(notesField);

        let uploadedFile = request?.uploadedFile || null;
        let fileSummary = null;
        let fileInput = null;
        if (action.requiresFile) {
            const uploadField = createElement(state.document, "div", "cw-canvas-composer__upload");
            const uploadTitle = createElement(state.document, "span", "cw-canvas-composer__upload-title", action.filePrompt || "Drop a file here or choose one.");
            uploadField.appendChild(uploadTitle);

            const dropZone = createElement(state.document, "label", "cw-canvas-composer__dropzone");
            dropZone.tabIndex = 0;
            fileInput = createElement(state.document, "input", "cw-canvas-composer__file-input");
            fileInput.type = "file";
            if (action.acceptedFileTypes) {
                fileInput.accept = action.acceptedFileTypes;
            }

            const dropHeadline = createElement(state.document, "strong", null, "Choose file");
            const dropCopy = createElement(state.document, "span", null, action.supportsDragDrop ? "Click or drop a file here." : "Click to choose a file.");
            fileSummary = createElement(state.document, "small", "cw-canvas-composer__upload-summary", "");
            dropZone.appendChild(fileInput);
            dropZone.appendChild(dropHeadline);
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

            dropZone.addEventListener("pointerdown", event => event.stopPropagation());
            dropZone.addEventListener("click", event => {
                event.stopPropagation();
                if (event.target === fileInput) {
                    return;
                }

                event.preventDefault();
                fileInput.click();
            });
            dropZone.addEventListener("keydown", event => {
                if (event.key !== "Enter" && event.key !== " ") {
                    return;
                }

                event.preventDefault();
                fileInput.click();
            });
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

        const actions = createElement(state.document, "div", "cw-canvas-composer__actions");
        const cancel = createElement(state.document, "button", "cw-button");
        cancel.type = "button";
        cancel.textContent = "Cancel";
        cancel.addEventListener("click", () => closeComposer(state));
        actions.appendChild(cancel);

        const create = createElement(state.document, "button", "cw-button");
        create.type = "button";
        create.dataset.tone = "accent";
        create.textContent = action.label || "Create";
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
            titleInput.focus();
            titleInput.select();
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

        const submenuOffsets = getRadialOffsets((action.children || []).length, 82, 70);
        const submenuRadius = getContextMenuOrbitRadius(submenuOffsets);
        const submenuOrigin = clampLayerOriginToHost(state, resolveSubmenuOrigin(parentLayer, offset), submenuOffsets, submenuRadius);

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
            baseRadius: 82,
            ringStep: 70
        });

        state.contextMenu.appendChild(submenuLayer.element);
        state.contextMenuState.layers.push(submenuLayer);
    }

    function renderContextMenuLayer(state, layerState, options) {
        const offsets = options.offsets || getRadialOffsets(options.actions.length, options.baseRadius, options.ringStep);
        layerState.radius = getContextMenuOrbitRadius(offsets);
        layerState.orbit.style.setProperty("--cw-orbit-size", `${round(layerState.radius * 2)}px`);
        options.actions.forEach((action, index) => {
            const offset = offsets[index];
            const button = createElement(state.document, "button", `cw-context-menu__action tone-${action.tone || "neutral"}`);
            button.type = "button";
            button.dataset.actionId = action.actionId || "";
            button.dataset.layerDepth = `${options.depth || 0}`;
            button.title = action.description || action.label || action.actionId || "action";
            button.style.setProperty("--cw-menu-x", `${round(offset.x)}px`);
            button.style.setProperty("--cw-menu-y", `${round(offset.y)}px`);
            button.addEventListener("pointerdown", event => event.stopPropagation());
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

            button.appendChild(createElement(state.document, "span", "cw-context-menu__icon", action.icon || action.label || "action"));
            button.appendChild(createElement(state.document, "strong", "cw-context-menu__label", action.label || action.actionId));
            if (action.children?.length) {
                button.appendChild(createElement(state.document, "span", "cw-context-menu__caret", "+"));
                button.classList.add("has-children");
            }

            layerState.orbit.appendChild(button);
        });

        return offsets;
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
        const rootOffsets = getRadialOffsets(actions.length);
        const rootCenter = positionContextMenu(state, hostPoint, rootOffsets);
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
            baseRadius: 96,
            ringStep: 84
        });
        state.contextMenu.appendChild(rootLayer.element);
        state.contextMenuState.layers.push(rootLayer);
    }

    function startPan(state, event) {
        state.interaction = {
            kind: "pan",
            startClientX: event.clientX,
            startClientY: event.clientY,
            panX: state.ui.panX,
            panY: state.ui.panY,
            moved: false
        };
    }

    function startMarquee(state, event) {
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
            state.selectedIds = toSelectionSet([nodeId]);
            state.ui.selectedNodeIds = [nodeId];
            render(state);
            publishSelection(state);
            publishState(state);
        }
    }

    function startDrag(state, event, nodeId) {
        ensureSelectedForDrag(state, nodeId);
        const draggedNodes = [...state.selectedIds].filter(id => state.lookups.byId.has(id));
        const startPositions = {};
        for (const id of draggedNodes) {
            const node = state.lookups.byId.get(id);
            startPositions[id] = getNodePosition(state, node);
        }

        state.interaction = {
            kind: "drag",
            startClientX: event.clientX,
            startClientY: event.clientY,
            moved: false,
            nodeIds: draggedNodes,
            startPositions
        };
        render(state);
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
        const selected = [];
        for (const element of state.nodeLayer.querySelectorAll(".cw-node")) {
            const rect = element.getBoundingClientRect();
            const intersects = rect.left < marqueeRect.right &&
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
        const deltaX = (event.clientX - state.interaction.startClientX) / state.ui.zoom;
        const deltaY = (event.clientY - state.interaction.startClientY) / state.ui.zoom;
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

        switch (interaction.kind) {
            case "drag":
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
    }

    function isNodeVisibleInViewport(state, node, margin) {
        const rect = state.host.getBoundingClientRect();
        const position = worldToHostPoint(state, getNodePosition(state, node));
        const size = getNodeSize(node);
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
        const padding = 120;
        const width = Math.max(bounds.maxX - bounds.minX, 320);
        const height = Math.max(bounds.maxY - bounds.minY, 240);
        const zoom = clamp(Math.min((rect.width - padding) / width, (rect.height - padding) / height), 0.55, 1.75);
        state.ui.zoom = zoom;
        setPan(
            state,
            (rect.width / 2) - ((bounds.minX + (width / 2)) * zoom),
            (rect.height / 2) - ((bounds.minY + (height / 2)) * zoom),
            zoom);
        render(state);
        publishState(state);
    }

    function focusNode(state, nodeId) {
        const node = state.lookups.byId.get(nodeId);
        if (!node) {
            return;
        }

        state.selectedIds = toSelectionSet([nodeId]);
        state.ui.selectedNodeIds = [nodeId];
        render(state);
        if (centerNodeElementInViewport(state, nodeId)) {
            render(state);
        }
        ensureHostFocus(state);
        deferHostFocus(state);
        publishSelection(state);
        publishState(state);
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
        const nextZoom = clamp((percent || 100) / 100, 0.55, 1.75);
        const rect = state.host.getBoundingClientRect();
        const anchor = anchorPoint || { x: rect.width / 2, y: rect.height / 2 };
        const worldX = (anchor.x - state.ui.panX) / state.ui.zoom;
        const worldY = (anchor.y - state.ui.panY) / state.ui.zoom;
        state.ui.zoom = nextZoom;
        setPan(
            state,
            anchor.x - (worldX * nextZoom),
            anchor.y - (worldY * nextZoom),
            nextZoom);
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
        state.selectedIds = toSelectionSet([node.id]);
        state.ui.selectedNodeIds = [node.id];
        render(state);
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
                ensureHostFocus(state);
                deferHostFocus(state);

                if (event.button === 2) {
                    return;
                }

                if (event.button === 1) {
                    startPan(state, event);
                    return;
                }

                const targetNode = hitTestNode(state, event.target);
                if (event.altKey) {
                    startMarquee(state, event);
                    return;
                }

                if (targetNode) {
                    const isMultiToggle = (event.ctrlKey || event.metaKey) && event.shiftKey;
                    if (event.button === 0 &&
                        !event.altKey &&
                        !event.ctrlKey &&
                        !event.metaKey &&
                        isManualDoubleActivation(state, targetNode.id)) {
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
                        state.selectedIds = toSelectionSet([targetNode.id]);
                        state.ui.selectedNodeIds = [targetNode.id];
                        render(state);
                        publishSelection(state);
                        publishState(state);
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

        const backdrop = createElement(state.document, "div", "cw-workbench__backdrop");
        const scene = createElement(state.document, "div", "cw-workbench__scene");
        const frameLayer = createElement(state.document, "div", "cw-workbench__frame-layer");
        const links = createSvgElement(state.document, "svg", "cw-workbench__links");
        const nodeLayer = createElement(state.document, "div", "cw-workbench__node-layer");
        const marquee = createElement(state.document, "div", "cw-marquee");
        const contextMenu = createElement(state.document, "div", "cw-context-menu");
        contextMenu.style.display = "none";
        marquee.style.display = "none";
        contextMenu.addEventListener("pointerdown", event => event.stopPropagation());
        contextMenu.addEventListener("contextmenu", event => {
            event.preventDefault();
            event.stopPropagation();

            const depth = (state.contextMenuState?.layers?.length || 1) - 1;
            if (depth > 0) {
                closeContextMenuLayersFrom(state, depth);
            }
        });

        scene.appendChild(frameLayer);
        scene.appendChild(links);
        scene.appendChild(nodeLayer);
        state.host.appendChild(backdrop);
        state.host.appendChild(scene);
        state.host.appendChild(marquee);
        state.host.appendChild(contextMenu);

        state.scene = scene;
        state.frameLayer = frameLayer;
        state.links = links;
        state.nodeLayer = nodeLayer;
        state.marquee = marquee;
        state.contextMenu = contextMenu;
        resize(state);
    }

    function hydrateState(host, dotNetRef, surface) {
        const normalizedSurface = normalizeSurface(surface);
        const lookups = buildNodeLookup(normalizedSurface.nodes);

        return {
            host,
            shell: host.closest(".cw-workbench-shell"),
            document: host.ownerDocument,
            dotNetRef,
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
            nodeLayer: null,
            marquee: null,
            contextMenu: null,
            contextMenuState: null,
            composer: null,
            pendingCreate: null,
            containingBlockOverride: null,
            resizeObserver: null,
            lastPointerTarget: null,
            recentDoubleActivationAt: 0,
            wheelZoom: null,
            publishStateDebounced: debounce(stateJson => dotNetRef.invokeMethodAsync("OnStateChanged", stateJson), 140)
        };
    }

    function refresh(state, surface) {
        const previousNodeIds = new Set((state.surface?.nodes || []).map(node => node.id));
        const previousSelectedId = state.ui?.selectedNodeIds?.[0] || null;
        const pendingCreate = state.pendingCreate;
        state.surface = normalizeSurface(surface);
        state.lookups = buildNodeLookup(state.surface.nodes);
        state.ui = state.surface.uiState;
        state.selectedIds = toSelectionSet(state.ui.selectedNodeIds);
        state.collapsedIds = toCollapsedSet(state.ui.collapsedNodeIds);
        clearContextMenu(state);
        if (state.composer?.nodeId && !state.lookups.byId.has(state.composer.nodeId)) {
            closeComposer(state, { focusHost: false });
        }

        setMaximized(state, !!state.ui.isMaximized);
        resize(state);
        const selectedNodeId = state.ui.selectedNodeIds[0] || null;
        const shouldRevealSelection = !!selectedNodeId &&
            (!!pendingCreate || (!!selectedNodeId && selectedNodeId !== previousSelectedId));
        if (shouldRevealSelection) {
            const isNewNode = !previousNodeIds.has(selectedNodeId);
            ensureNodeVisible(state, selectedNodeId, { forceCenter: isNewNode });
        }

        render(state);

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
        getState(host) {
            const state = host.__canvasWorkbenchState;
            return state ? serializeState(state) : JSON.stringify({});
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
            const state = host.__canvasWorkbenchState;
            if (!state) {
                return;
            }

            if (state.resizeObserver) {
                state.resizeObserver.disconnect();
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
