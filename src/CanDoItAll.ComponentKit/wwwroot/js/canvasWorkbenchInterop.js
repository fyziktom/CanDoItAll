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
                contextActions: Array.isArray(node.contextActions) ? node.contextActions : []
            })) : [],
            links: Array.isArray(surface?.links) ? surface.links : [],
            uiState: {
                version: surface?.uiState?.version || "canvas-workbench.v1",
                selectedNodeIds: Array.isArray(surface?.uiState?.selectedNodeIds) ? [...surface.uiState.selectedNodeIds] : [],
                collapsedNodeIds: Array.isArray(surface?.uiState?.collapsedNodeIds) ? [...surface.uiState.collapsedNodeIds] : [],
                manualPositions: surface?.uiState?.manualPositions || {},
                zoom: typeof surface?.uiState?.zoom === "number" ? clamp(surface.uiState.zoom, 0.55, 1.75) : 1,
                panX: typeof surface?.uiState?.panX === "number" ? surface.uiState.panX : 90,
                panY: typeof surface?.uiState?.panY === "number" ? surface.uiState.panY : 110,
                isMaximized: !!surface?.uiState?.isMaximized,
                activeInspectorTab: surface?.uiState?.activeInspectorTab || ""
            },
            chrome: {
                quickCreateActions: Array.isArray(surface?.chrome?.quickCreateActions) ? surface.chrome.quickCreateActions : [],
                showQuickCreateRail: surface?.chrome?.showQuickCreateRail !== false
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

    function getNodePosition(state, node) {
        const manual = state.ui.manualPositions?.[node.id];
        return manual && typeof manual.x === "number" && typeof manual.y === "number"
            ? { x: manual.x, y: manual.y }
            : { x: node.x, y: node.y };
    }

    function serializeState(state) {
        return JSON.stringify({
            version: state.ui.version || "canvas-workbench.v1",
            selectedNodeIds: [...state.selectedIds],
            collapsedNodeIds: [...state.collapsedIds],
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
            const controlOffset = Math.max(92, Math.abs(targetPosition.x - sourcePosition.x) * 0.38);
            const path = createSvgElement(state.document, "path");
            path.setAttribute("d", [
                `M ${sourcePosition.x} ${sourcePosition.y}`,
                `C ${sourcePosition.x + controlOffset} ${sourcePosition.y}`,
                `${targetPosition.x - controlOffset} ${targetPosition.y}`,
                `${targetPosition.x} ${targetPosition.y}`
            ].join(" "));
            path.setAttribute("fill", "none");
            path.setAttribute("stroke", link.isUserAuthored ? "rgba(14, 165, 233, 0.78)" : "rgba(100, 116, 139, 0.4)");
            path.setAttribute("stroke-width", link.isUserAuthored ? "3" : "2");
            path.setAttribute("stroke-linecap", "round");
            path.setAttribute("stroke-linejoin", "round");
            state.links.appendChild(path);
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

            const surface = createElement(state.document, "div", "cw-node__surface");
            const header = createElement(state.document, "div", "cw-node__header");
            const eyebrow = createElement(state.document, "div", "cw-node__eyebrow");
            const icon = createElement(state.document, "span", "cw-node__icon", node.icon || node.kind || "node");
            const kicker = createElement(state.document, "span", "cw-node__kicker", node.kind || node.family || "item");
            eyebrow.appendChild(icon);
            eyebrow.appendChild(kicker);
            header.appendChild(eyebrow);

            const rightMeta = createElement(state.document, "div", "cw-chip-row");
            if (node.durationLabel) {
                rightMeta.appendChild(createElement(state.document, "span", "cw-node__pill", node.durationLabel));
            }

            if (node.statusPill) {
                rightMeta.appendChild(createElement(state.document, "span", "cw-node__pill", node.statusPill));
            }

            header.appendChild(rightMeta);
            surface.appendChild(header);

            const title = createElement(state.document, "h3", "cw-node__title", node.title || "Untitled");
            surface.appendChild(title);

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
                const collapse = createElement(state.document, "button", "cw-node__collapse", state.collapsedIds.has(node.id) ? "+" : "−");
                collapse.type = "button";
                collapse.dataset.nodeId = node.id;
                collapse.addEventListener("click", event => {
                    event.stopPropagation();
                    toggleCollapse(state, node.id);
                });
                surface.appendChild(collapse);
            }

            nodeElement.appendChild(surface);
            state.nodeLayer.appendChild(nodeElement);
        }
    }

    function render(state) {
        applySceneTransform(state);
        const visibleNodes = state.surface.nodes.filter(node => isNodeVisible(state, node.id));
        renderLinks(state, visibleNodes);
        renderNodes(state, visibleNodes);
    }

    function getHostPoint(state, clientX, clientY) {
        const rect = state.host.getBoundingClientRect();
        return {
            x: clientX - rect.left,
            y: clientY - rect.top
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
    }

    function getContextActions(state, node) {
        if (node) {
            return node.contextActions || [];
        }

        return state.surface.chrome.quickCreateActions || [];
    }

    function showContextMenu(state, node, clientX, clientY) {
        clearContextMenu(state);
        const actions = getContextActions(state, node);
        if (!actions.length) {
            return;
        }

        const hostPoint = getHostPoint(state, clientX, clientY);
        state.contextMenu.style.display = "grid";
        state.contextMenu.style.left = `${hostPoint.x + 10}px`;
        state.contextMenu.style.top = `${hostPoint.y + 10}px`;

        for (const action of actions) {
            const button = createElement(state.document, "button", "cw-context-menu__action");
            button.type = "button";
            const label = createElement(state.document, "strong", null, action.label || action.actionId);
            const icon = createElement(state.document, "span", null, action.icon || action.tone || "action");
            button.appendChild(label);
            button.appendChild(icon);
            button.addEventListener("click", () => {
                clearContextMenu(state);
                if (node) {
                    const position = getNodePosition(state, node);
                    state.dotNetRef.invokeMethodAsync("OnContextAction", node.id, action.actionId, round(position.x), round(position.y));
                }
                else {
                    const world = getWorldPoint(state, clientX, clientY);
                    state.dotNetRef.invokeMethodAsync("OnCreateAction", action.actionId, null, round(world.x), round(world.y));
                }
            });
            state.contextMenu.appendChild(button);
        }
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
            publishSelection(state);
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
        state.ui.panX = round(state.interaction.panX + deltaX);
        state.ui.panY = round(state.interaction.panY + deltaY);
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
                else {
                    setSelection(state, [], true);
                }
                break;
            case "marquee":
                applyMarqueeSelection(state);
                break;
        }
    }

    function resize(state) {
        const rect = state.host.getBoundingClientRect();
        state.links.setAttribute("width", `${Math.max(rect.width, 1)}`);
        state.links.setAttribute("height", `${Math.max(rect.height, 1)}`);
    }

    function fitView(state) {
        const visibleNodes = state.surface.nodes.filter(node => isNodeVisible(state, node.id));
        if (!visibleNodes.length) {
            return;
        }

        let minX = Number.POSITIVE_INFINITY;
        let maxX = Number.NEGATIVE_INFINITY;
        let minY = Number.POSITIVE_INFINITY;
        let maxY = Number.NEGATIVE_INFINITY;

        for (const node of visibleNodes) {
            const position = getNodePosition(state, node);
            const size = getNodeSize(node);
            minX = Math.min(minX, position.x - (size.width / 2));
            maxX = Math.max(maxX, position.x + (size.width / 2));
            minY = Math.min(minY, position.y - (size.height / 2));
            maxY = Math.max(maxY, position.y + (size.height / 2));
        }

        const rect = state.host.getBoundingClientRect();
        const padding = 120;
        const width = Math.max(maxX - minX, 320);
        const height = Math.max(maxY - minY, 240);
        const zoom = clamp(Math.min((rect.width - padding) / width, (rect.height - padding) / height), 0.55, 1.75);
        state.ui.zoom = zoom;
        state.ui.panX = round((rect.width / 2) - ((minX + (width / 2)) * zoom));
        state.ui.panY = round((rect.height / 2) - ((minY + (height / 2)) * zoom));
        render(state);
        publishState(state);
    }

    function focusNode(state, nodeId) {
        const node = state.lookups.byId.get(nodeId);
        if (!node) {
            return;
        }

        const rect = state.host.getBoundingClientRect();
        const position = getNodePosition(state, node);
        state.ui.panX = round((rect.width / 2) - (position.x * state.ui.zoom));
        state.ui.panY = round((rect.height / 2) - (position.y * state.ui.zoom));
        state.selectedIds = toSelectionSet([nodeId]);
        state.ui.selectedNodeIds = [nodeId];
        render(state);
        publishSelection(state);
        publishState(state);
    }

    function setZoomPercent(state, percent, anchorPoint) {
        const nextZoom = clamp((percent || 100) / 100, 0.55, 1.75);
        const rect = state.host.getBoundingClientRect();
        const anchor = anchorPoint || { x: rect.width / 2, y: rect.height / 2 };
        const worldX = (anchor.x - state.ui.panX) / state.ui.zoom;
        const worldY = (anchor.y - state.ui.panY) / state.ui.zoom;
        state.ui.zoom = nextZoom;
        state.ui.panX = round(anchor.x - (worldX * nextZoom));
        state.ui.panY = round(anchor.y - (worldY * nextZoom));
        render(state);
        publishState(state);
    }

    function toggleHelp(state) {
        state.helpOpen = !state.helpOpen;
        state.dotNetRef.invokeMethodAsync("OnHelpToggled", state.helpOpen);
    }

    function attachEvents(state) {
        state.handlers = {
            pointerDown: event => {
            clearContextMenu(state);
            state.host.focus();

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
                if (event.ctrlKey || event.metaKey) {
                    toggleSelection(state, targetNode.id);
                }
                else if (!state.selectedIds.has(targetNode.id) || state.selectedIds.size > 1) {
                    state.selectedIds = toSelectionSet([targetNode.id]);
                    state.ui.selectedNodeIds = [targetNode.id];
                    publishSelection(state);
                }

                startDrag(state, event, targetNode.id);
                return;
            }

            startPan(state, event);
            },
            pointerMove: event => {
                if (!state.interaction) {
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
                const targetNode = hitTestNode(state, event.target);
                if (!targetNode) {
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
                const hostPoint = getHostPoint(state, event.clientX, event.clientY);
                const delta = event.deltaY > 0 ? -10 : 10;
                setZoomPercent(state, (state.ui.zoom * 100) + delta, hostPoint);
            },
            contextMenu: event => {
                event.preventDefault();
                const targetNode = hitTestNode(state, event.target);
                if (targetNode) {
                    setSelection(state, [targetNode.id], true);
                }

                showContextMenu(state, targetNode, event.clientX, event.clientY);
            },
            keyDown: event => {
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
                        clearContextMenu(state);
                        setSelection(state, [], true);
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
        state.host.addEventListener("keydown", state.handlers.keyDown);
    }

    function buildWorkbench(state) {
        clear(state.host);
        state.host.classList.add("cw-workbench");

        const backdrop = createElement(state.document, "div", "cw-workbench__backdrop");
        const scene = createElement(state.document, "div", "cw-workbench__scene");
        const links = createSvgElement(state.document, "svg", "cw-workbench__links");
        const nodeLayer = createElement(state.document, "div", "cw-workbench__node-layer");
        const marquee = createElement(state.document, "div", "cw-marquee");
        const contextMenu = createElement(state.document, "div", "cw-context-menu");
        contextMenu.style.display = "none";
        marquee.style.display = "none";

        scene.appendChild(links);
        scene.appendChild(nodeLayer);
        state.host.appendChild(backdrop);
        state.host.appendChild(scene);
        state.host.appendChild(marquee);
        state.host.appendChild(contextMenu);

        state.scene = scene;
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
            links: null,
            nodeLayer: null,
            marquee: null,
            contextMenu: null,
            publishStateDebounced: debounce(stateJson => dotNetRef.invokeMethodAsync("OnStateChanged", stateJson), 140)
        };
    }

    function refresh(state, surface) {
        state.surface = normalizeSurface(surface);
        state.lookups = buildNodeLookup(state.surface.nodes);
        state.ui = state.surface.uiState;
        state.selectedIds = toSelectionSet(state.ui.selectedNodeIds);
        state.collapsedIds = toCollapsedSet(state.ui.collapsedNodeIds);
        render(state);
        resize(state);
    }

    root.canvasWorkbench = {
        create(host, dotNetRef, surface) {
            const state = hydrateState(host, dotNetRef, surface);
            buildWorkbench(state);
            attachEvents(state);
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
        getState(host) {
            const state = host.__canvasWorkbenchState;
            return state ? serializeState(state) : JSON.stringify({});
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

            if (state.handlers) {
                host.removeEventListener("pointerdown", state.handlers.pointerDown);
                window.removeEventListener("pointermove", state.handlers.pointerMove);
                window.removeEventListener("pointerup", state.handlers.pointerUp);
                window.removeEventListener("blur", state.handlers.blur);
                host.removeEventListener("dblclick", state.handlers.doubleClick);
                host.removeEventListener("wheel", state.handlers.wheel);
                host.removeEventListener("contextmenu", state.handlers.contextMenu);
                host.removeEventListener("keydown", state.handlers.keyDown);
            }

            clear(host);
            delete host.__canvasWorkbenchState;
        }
    };
})();
