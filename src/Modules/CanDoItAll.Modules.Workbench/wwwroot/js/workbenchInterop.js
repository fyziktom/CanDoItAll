(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};

    function clear(host) {
        while (host.firstChild) {
            host.removeChild(host.firstChild);
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

    function safeParse(json, fallback) {
        if (!json) {
            return fallback;
        }

        try {
            return Object.assign({}, fallback, JSON.parse(json));
        } catch {
            return fallback;
        }
    }

    function clamp(value, min, max) {
        return Math.max(min, Math.min(max, value));
    }

    function hexToRgba(hex, alpha) {
        const normalized = (hex || "#475569").replace("#", "");
        const expanded = normalized.length === 3
            ? normalized.split("").map(part => part + part).join("")
            : normalized.padEnd(6, "0").slice(0, 6);
        const r = Number.parseInt(expanded.slice(0, 2), 16);
        const g = Number.parseInt(expanded.slice(2, 4), 16);
        const b = Number.parseInt(expanded.slice(4, 6), 16);
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    function debounce(callback, delayMs) {
        let handle;
        return (...args) => {
            window.clearTimeout(handle);
            handle = window.setTimeout(() => callback(...args), delayMs);
        };
    }

    function getCanvasNodeBounds(node) {
        const shape = node.visualProfile?.shape || "rect";
        if (shape === "circle") {
            return { width: 104, height: 104, radius: 52 };
        }

        if (shape === "pill") {
            return { width: 196, height: 64, radius: 32 };
        }

        return { width: 204, height: 80, radius: 18 };
    }

    function drawRoundedRect(context, x, y, width, height, radius) {
        context.beginPath();
        context.moveTo(x + radius, y);
        context.lineTo(x + width - radius, y);
        context.quadraticCurveTo(x + width, y, x + width, y + radius);
        context.lineTo(x + width, y + height - radius);
        context.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
        context.lineTo(x + radius, y + height);
        context.quadraticCurveTo(x, y + height, x, y + height - radius);
        context.lineTo(x, y + radius);
        context.quadraticCurveTo(x, y, x + radius, y);
        context.closePath();
    }

    function drawDiamond(context, centerX, centerY, width, height) {
        context.beginPath();
        context.moveTo(centerX, centerY - (height / 2));
        context.lineTo(centerX + (width / 2), centerY);
        context.lineTo(centerX, centerY + (height / 2));
        context.lineTo(centerX - (width / 2), centerY);
        context.closePath();
    }

    function drawHex(context, centerX, centerY, width, height) {
        const halfWidth = width / 2;
        const halfHeight = height / 2;
        context.beginPath();
        context.moveTo(centerX - (halfWidth * 0.55), centerY - halfHeight);
        context.lineTo(centerX + (halfWidth * 0.55), centerY - halfHeight);
        context.lineTo(centerX + halfWidth, centerY);
        context.lineTo(centerX + (halfWidth * 0.55), centerY + halfHeight);
        context.lineTo(centerX - (halfWidth * 0.55), centerY + halfHeight);
        context.lineTo(centerX - halfWidth, centerY);
        context.closePath();
    }

    function drawShield(context, centerX, centerY, width, height) {
        const halfWidth = width / 2;
        const halfHeight = height / 2;
        context.beginPath();
        context.moveTo(centerX - halfWidth, centerY - (halfHeight * 0.8));
        context.lineTo(centerX + halfWidth, centerY - (halfHeight * 0.8));
        context.lineTo(centerX + (halfWidth * 0.8), centerY + (halfHeight * 0.2));
        context.lineTo(centerX, centerY + halfHeight);
        context.lineTo(centerX - (halfWidth * 0.8), centerY + (halfHeight * 0.2));
        context.closePath();
    }

    function drawNodeShape(context, node, state, isSelected) {
        const bounds = getCanvasNodeBounds(node);
        const screenX = (node.x * state.viewport.zoom) + state.viewport.panX;
        const screenY = (node.y * state.viewport.zoom) + state.viewport.panY;
        const width = bounds.width * state.viewport.zoom;
        const height = bounds.height * state.viewport.zoom;
        const centerX = screenX;
        const centerY = screenY;
        const left = centerX - (width / 2);
        const top = centerY - (height / 2);
        const accent = node.visualProfile?.accentColor || "#475569";

        context.save();
        context.shadowColor = isSelected ? hexToRgba(accent, 0.4) : "rgba(15, 23, 42, 0.08)";
        context.shadowBlur = isSelected ? 18 : 8;
        context.lineWidth = isSelected ? 3 : 1.5;
        context.strokeStyle = isSelected ? accent : hexToRgba("#0f172a", 0.14);
        context.fillStyle = isSelected ? hexToRgba(accent, 0.16) : "#ffffff";

        switch (node.visualProfile?.shape) {
            case "pill":
                drawRoundedRect(context, left, top, width, height, height / 2);
                break;
            case "circle":
                context.beginPath();
                context.arc(centerX, centerY, (bounds.radius || 52) * state.viewport.zoom, 0, Math.PI * 2);
                context.closePath();
                break;
            case "diamond":
                drawDiamond(context, centerX, centerY, width, height);
                break;
            case "hex":
                drawHex(context, centerX, centerY, width, height);
                break;
            case "shield":
                drawShield(context, centerX, centerY, width, height);
                break;
            default:
                drawRoundedRect(context, left, top, width, height, 20 * state.viewport.zoom);
                break;
        }

        context.fill();
        context.stroke();

        context.shadowBlur = 0;
        context.fillStyle = "#0f172a";
        context.font = `${Math.max(12, 15 * state.viewport.zoom)}px ui-sans-serif, system-ui, sans-serif`;
        context.fillText(node.title, left + (16 * state.viewport.zoom), top + (30 * state.viewport.zoom), width - (32 * state.viewport.zoom));

        context.fillStyle = "#64748b";
        context.font = `${Math.max(10, 11 * state.viewport.zoom)}px ui-sans-serif, system-ui, sans-serif`;
        context.fillText(node.subtitle || "", left + (16 * state.viewport.zoom), top + (50 * state.viewport.zoom), width - (32 * state.viewport.zoom));

        context.fillStyle = accent;
        context.font = `${Math.max(9, 10 * state.viewport.zoom)}px ui-monospace, monospace`;
        context.fillText(node.visualProfile?.accentBadge || "", left + (16 * state.viewport.zoom), top + (16 * state.viewport.zoom), width - (32 * state.viewport.zoom));

        context.restore();
    }

    function drawCanvasGrid(state) {
        const context = state.canvas.getContext("2d");
        const width = state.canvas.width;
        const height = state.canvas.height;
        context.clearRect(0, 0, width, height);
        context.fillStyle = "#f8fafc";
        context.fillRect(0, 0, width, height);

        const spacing = 48 * state.viewport.zoom;
        context.strokeStyle = "rgba(148, 163, 184, 0.18)";
        context.lineWidth = 1;

        for (let x = state.viewport.panX % spacing; x < width; x += spacing) {
            context.beginPath();
            context.moveTo(x, 0);
            context.lineTo(x, height);
            context.stroke();
        }

        for (let y = state.viewport.panY % spacing; y < height; y += spacing) {
            context.beginPath();
            context.moveTo(0, y);
            context.lineTo(width, y);
            context.stroke();
        }
    }

    function drawCanvasLinks(state) {
        const context = state.canvas.getContext("2d");
        const lookup = new Map(state.surface.nodes.map(node => [node.id, node]));
        for (const link of state.surface.links || []) {
            const source = lookup.get(link.sourceId);
            const target = lookup.get(link.targetId);
            if (!source || !target) {
                continue;
            }

            const startX = (source.x * state.viewport.zoom) + state.viewport.panX;
            const startY = (source.y * state.viewport.zoom) + state.viewport.panY;
            const endX = (target.x * state.viewport.zoom) + state.viewport.panX;
            const endY = (target.y * state.viewport.zoom) + state.viewport.panY;
            const controlOffset = Math.max(48, Math.abs(endX - startX) * 0.35);
            context.beginPath();
            context.moveTo(startX, startY);
            context.bezierCurveTo(startX + controlOffset, startY, endX - controlOffset, endY, endX, endY);
            context.strokeStyle = link.isUserAuthored ? "rgba(14, 165, 233, 0.7)" : "rgba(100, 116, 139, 0.45)";
            context.lineWidth = link.isUserAuthored ? 2.5 : 1.5;
            context.stroke();
        }
    }

    function renderCanvas(state) {
        if (!state.surface) {
            return;
        }

        drawCanvasGrid(state);
        drawCanvasLinks(state);
        for (const node of state.surface.nodes || []) {
            drawNodeShape(state.canvas.getContext("2d"), node, state, node.id === state.selectedNodeId);
        }
    }

    function getWorldPoint(state, clientX, clientY) {
        const rect = state.canvas.getBoundingClientRect();
        return {
            x: (clientX - rect.left - state.viewport.panX) / state.viewport.zoom,
            y: (clientY - rect.top - state.viewport.panY) / state.viewport.zoom
        };
    }

    function hitTestNode(state, clientX, clientY) {
        const world = getWorldPoint(state, clientX, clientY);
        const nodes = [...(state.surface?.nodes || [])].reverse();
        for (const node of nodes) {
            const bounds = getCanvasNodeBounds(node);
            const halfWidth = bounds.width / 2;
            const halfHeight = bounds.height / 2;
            if (world.x >= node.x - halfWidth && world.x <= node.x + halfWidth &&
                world.y >= node.y - halfHeight && world.y <= node.y + halfHeight) {
                return node;
            }
        }

        return null;
    }

    function publishCanvasViewState(state) {
        if (!state.dotNetRef) {
            return;
        }

        state.publishViewState(JSON.stringify({
            viewport: state.viewport,
            selectedNodeId: state.selectedNodeId
        }));
    }

    function resizeCanvas(state) {
        const rect = state.host.getBoundingClientRect();
        state.canvas.width = Math.max(720, Math.floor(rect.width));
        state.canvas.height = Math.max(560, Math.floor(rect.height));
        renderCanvas(state);
    }

    function createHexMenu(state) {
        const document = state.host.ownerDocument;
        const menu = createElement(document, "div", "candoitall-hex-menu");
        Object.assign(menu.style, {
            position: "absolute",
            inset: "0",
            pointerEvents: "none",
            zIndex: "5"
        });
        state.wrapper.appendChild(menu);
        return menu;
    }

    function buildMenuActions(node) {
        if (!node) {
            return [
                { action: "add-note", label: "Note" },
                { action: "add-decision", label: "Decision" },
                { action: "add-milestone", label: "Milestone" },
                { action: "add-repository", label: "Repo" }
            ];
        }

        const actions = [
            { action: "open", label: "Open" },
            { action: "link", label: "Link" },
            { action: "test", label: "Test" },
            { action: "add-note", label: "Note" }
        ];

        if (node.objectType === "PromptStep") {
            actions.unshift({ action: "branch", label: "Branch" });
            actions.push({ action: "mark-used", label: "Used" });
            actions.push({ action: "skip", label: "Skip" });
        }

        return actions.slice(0, 6);
    }

    function showHexMenu(state, node, clientX, clientY, worldPoint) {
        state.menu.innerHTML = "";
        const actions = buildMenuActions(node);
        if (actions.length === 0) {
            return;
        }

        const center = createElement(state.host.ownerDocument, "div", null, node ? node.title : "Canvas");
        Object.assign(center.style, {
            position: "absolute",
            left: `${clientX - 34}px`,
            top: `${clientY - 18}px`,
            width: "68px",
            height: "36px",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            borderRadius: "999px",
            background: "rgba(15, 23, 42, 0.92)",
            color: "#fff",
            fontSize: "11px",
            fontWeight: "700",
            pointerEvents: "auto"
        });
        state.menu.appendChild(center);

        actions.forEach((item, index) => {
            const angle = ((Math.PI * 2) / actions.length) * index - (Math.PI / 2);
            const radius = 74;
            const button = createElement(state.host.ownerDocument, "button", null, item.label);
            button.type = "button";
            Object.assign(button.style, {
                position: "absolute",
                left: `${clientX + (Math.cos(angle) * radius) - 28}px`,
                top: `${clientY + (Math.sin(angle) * radius) - 28}px`,
                width: "56px",
                height: "56px",
                border: "0",
                borderRadius: "18px",
                background: "#ffffff",
                boxShadow: "0 18px 32px rgba(15, 23, 42, 0.12)",
                color: "#0f172a",
                fontSize: "10px",
                fontWeight: "700",
                cursor: "pointer",
                pointerEvents: "auto",
                clipPath: "polygon(25% 6%,75% 6%,100% 50%,75% 94%,25% 94%,0 50%)"
            });
            button.addEventListener("click", () => {
                state.menu.innerHTML = "";
                state.dotNetRef.invokeMethodAsync("OnContextAction", node ? node.id : null, item.action, node ? node.x : worldPoint.x, node ? node.y : worldPoint.y);
            });
            state.menu.appendChild(button);
        });
    }

    function hideHexMenu(state) {
        if (state.menu) {
            state.menu.innerHTML = "";
        }
    }

    function applyCanvasViewState(state, viewStateJson, selectedNodeId) {
        const parsed = safeParse(viewStateJson, { viewport: { panX: 80, panY: 80, zoom: 1 }, selectedNodeId: null });
        state.viewport.panX = typeof parsed.viewport?.panX === "number" ? parsed.viewport.panX : 80;
        state.viewport.panY = typeof parsed.viewport?.panY === "number" ? parsed.viewport.panY : 80;
        state.viewport.zoom = clamp(typeof parsed.viewport?.zoom === "number" ? parsed.viewport.zoom : 1, 0.55, 1.75);
        state.selectedNodeId = selectedNodeId || parsed.selectedNodeId || state.selectedNodeId;
    }

    function normalizeCanvasSurface(surface) {
        return {
            nodes: (surface?.nodes || []).map(node => ({
                ...node,
                x: typeof node.x === "number" ? node.x : 120,
                y: typeof node.y === "number" ? node.y : 120
            })),
            links: surface?.links || []
        };
    }

    root.workbenchCanvas = {
        create(host, dotNetRef, surface, selectedNodeId, viewStateJson) {
            clear(host);
            const document = host.ownerDocument;
            const wrapper = createElement(document, "div");
            Object.assign(wrapper.style, {
                position: "relative",
                width: "100%",
                minHeight: "36rem",
                overflow: "hidden"
            });

            const canvas = createElement(document, "canvas");
            Object.assign(canvas.style, {
                width: "100%",
                height: "100%",
                display: "block",
                cursor: "grab"
            });

            wrapper.appendChild(canvas);
            host.appendChild(wrapper);

            const state = {
                host,
                wrapper,
                canvas,
                dotNetRef,
                surface: normalizeCanvasSurface(surface),
                selectedNodeId: selectedNodeId || null,
                viewport: { panX: 80, panY: 80, zoom: 1 },
                drag: null,
                menu: null,
                resizeObserver: null,
                publishViewState: debounce((payload) => dotNetRef.invokeMethodAsync("OnViewStateChanged", payload), 180)
            };

            state.menu = createHexMenu(state);
            applyCanvasViewState(state, viewStateJson, selectedNodeId);
            state.resizeObserver = new ResizeObserver(() => resizeCanvas(state));
            state.resizeObserver.observe(host);

            canvas.addEventListener("mousedown", (event) => {
                hideHexMenu(state);
                const hit = hitTestNode(state, event.clientX, event.clientY);
                if (hit) {
                    state.selectedNodeId = hit.id;
                    dotNetRef.invokeMethodAsync("OnNodeSelected", hit.id);
                    const world = getWorldPoint(state, event.clientX, event.clientY);
                    state.drag = { type: "node", nodeId: hit.id, offsetX: world.x - hit.x, offsetY: world.y - hit.y };
                } else {
                    state.drag = { type: "pan", startX: event.clientX, startY: event.clientY, panX: state.viewport.panX, panY: state.viewport.panY };
                }

                renderCanvas(state);
            });

            canvas.addEventListener("mousemove", (event) => {
                if (!state.drag) {
                    return;
                }

                if (state.drag.type === "pan") {
                    state.viewport.panX = state.drag.panX + (event.clientX - state.drag.startX);
                    state.viewport.panY = state.drag.panY + (event.clientY - state.drag.startY);
                } else {
                    const node = state.surface.nodes.find(item => item.id === state.drag.nodeId);
                    if (node) {
                        const world = getWorldPoint(state, event.clientX, event.clientY);
                        node.x = world.x - state.drag.offsetX;
                        node.y = world.y - state.drag.offsetY;
                    }
                }

                renderCanvas(state);
            });

            const completeDrag = () => {
                if (!state.drag) {
                    return;
                }

                if (state.drag.type === "node") {
                    const node = state.surface.nodes.find(item => item.id === state.drag.nodeId);
                    if (node) {
                        dotNetRef.invokeMethodAsync("OnNodeMoved", node.id, node.x, node.y);
                    }
                }

                state.drag = null;
                publishCanvasViewState(state);
            };

            canvas.addEventListener("mouseup", completeDrag);
            canvas.addEventListener("mouseleave", completeDrag);
            canvas.addEventListener("dblclick", (event) => {
                const hit = hitTestNode(state, event.clientX, event.clientY);
                if (hit) {
                    dotNetRef.invokeMethodAsync("OnNodeOpened", hit.id);
                }
            });

            canvas.addEventListener("wheel", (event) => {
                event.preventDefault();
                const worldBefore = getWorldPoint(state, event.clientX, event.clientY);
                state.viewport.zoom = clamp(state.viewport.zoom + (event.deltaY > 0 ? -0.08 : 0.08), 0.55, 1.75);
                const rect = canvas.getBoundingClientRect();
                state.viewport.panX = event.clientX - rect.left - (worldBefore.x * state.viewport.zoom);
                state.viewport.panY = event.clientY - rect.top - (worldBefore.y * state.viewport.zoom);
                renderCanvas(state);
                publishCanvasViewState(state);
            }, { passive: false });

            canvas.addEventListener("contextmenu", (event) => {
                event.preventDefault();
                const hit = hitTestNode(state, event.clientX, event.clientY);
                const world = getWorldPoint(state, event.clientX, event.clientY);
                if (hit) {
                    state.selectedNodeId = hit.id;
                    dotNetRef.invokeMethodAsync("OnNodeSelected", hit.id);
                }

                showHexMenu(state, hit, event.clientX - canvas.getBoundingClientRect().left, event.clientY - canvas.getBoundingClientRect().top, world);
            });

            host.__workbenchCanvas = state;
            resizeCanvas(state);
        },
        update(host, surface, selectedNodeId, viewStateJson) {
            const state = host.__workbenchCanvas;
            if (!state) {
                return;
            }

            state.surface = normalizeCanvasSurface(surface);
            applyCanvasViewState(state, viewStateJson, selectedNodeId);
            renderCanvas(state);
        },
        dispose(host) {
            const state = host.__workbenchCanvas;
            if (state?.resizeObserver) {
                state.resizeObserver.disconnect();
            }

            delete host.__workbenchCanvas;
            clear(host);
        }
    };

    function normalizeCalendarSurface(surface) {
        return {
            preferredView: surface?.preferredView || "month",
            events: (surface?.events || []).map(item => ({
                ...item,
                start: new Date(item.startUtc),
                end: new Date(item.endUtc)
            }))
        };
    }

    function buildCalendarState(viewStateJson, surface, selectedEventId) {
        const parsed = safeParse(viewStateJson, { preferredView: surface.preferredView || "month", selectedEventId: null });
        return {
            preferredView: parsed.preferredView || surface.preferredView || "month",
            selectedEventId: selectedEventId || parsed.selectedEventId || null
        };
    }

    function publishCalendarState(state) {
        state.publishViewState(JSON.stringify({
            preferredView: state.ui.preferredView,
            selectedEventId: state.ui.selectedEventId
        }));
    }

    function createEventChip(document, event, accentColor) {
        const button = createElement(document, "button", null, event.title);
        button.type = "button";
        Object.assign(button.style, {
            width: "100%",
            border: "0",
            borderRadius: "12px",
            padding: "0.4rem 0.55rem",
            textAlign: "left",
            background: hexToRgba(accentColor, 0.14),
            color: "#0f172a",
            fontSize: "12px",
            fontWeight: "700",
            cursor: "pointer"
        });
        return button;
    }

    function renderListView(state) {
        const document = state.host.ownerDocument;
        const list = createElement(document, "div");
        list.style.display = "grid";
        list.style.gap = "0.75rem";
        for (const event of state.surface.events) {
            const card = createElement(document, "div");
            Object.assign(card.style, {
                border: "1px solid rgba(148, 163, 184, 0.22)",
                borderRadius: "18px",
                padding: "0.85rem",
                background: event.id === state.ui.selectedEventId ? "#0f172a" : "#ffffff",
                color: event.id === state.ui.selectedEventId ? "#ffffff" : "#0f172a"
            });
            const button = createElement(document, "button", null, event.title);
            button.type = "button";
            Object.assign(button.style, {
                border: "0",
                padding: "0",
                background: "transparent",
                color: "inherit",
                fontWeight: "700",
                fontSize: "14px",
                cursor: "pointer"
            });
            button.addEventListener("click", () => selectCalendarEvent(state, event));
            button.addEventListener("dblclick", () => openCalendarEvent(state, event));
            card.appendChild(button);

            const meta = createElement(document, "div", null, `${event.start.toLocaleString()} -> ${event.end.toLocaleString()} · ${event.status}`);
            meta.style.marginTop = "0.5rem";
            meta.style.fontSize = "12px";
            meta.style.opacity = "0.7";
            card.appendChild(meta);
            list.appendChild(card);
        }
        return list;
    }

    function sameDay(left, right) {
        return left.getFullYear() === right.getFullYear() && left.getMonth() === right.getMonth() && left.getDate() === right.getDate();
    }

    function renderMonthLikeView(state, monthOffsetCount) {
        const document = state.host.ownerDocument;
        const rootElement = createElement(document, "div");
        rootElement.style.display = "grid";
        rootElement.style.gap = "1rem";
        rootElement.style.gridTemplateColumns = monthOffsetCount === 12 ? "repeat(3, minmax(0, 1fr))" : "1fr";

        const anchor = state.surface.events[0]?.start || new Date();
        const months = monthOffsetCount === 12
            ? Array.from({ length: 12 }, (_, index) => new Date(anchor.getFullYear(), index, 1))
            : [new Date(anchor.getFullYear(), anchor.getMonth(), 1)];

        for (const monthStart of months) {
            const panel = createElement(document, "div");
            Object.assign(panel.style, {
                border: "1px solid rgba(148, 163, 184, 0.22)",
                borderRadius: "20px",
                background: "#ffffff",
                padding: "0.75rem"
            });

            const heading = createElement(document, "div", null, monthStart.toLocaleString(undefined, { month: "long", year: "numeric" }));
            heading.style.fontWeight = "700";
            heading.style.marginBottom = "0.75rem";
            panel.appendChild(heading);

            const grid = createElement(document, "div");
            Object.assign(grid.style, {
                display: "grid",
                gridTemplateColumns: "repeat(7, minmax(0, 1fr))",
                gap: "0.4rem"
            });

            const day = new Date(monthStart);
            const leading = day.getDay();
            day.setDate(day.getDate() - leading);

            for (let index = 0; index < 42; index++) {
                const cell = createElement(document, "div");
                Object.assign(cell.style, {
                    minHeight: "94px",
                    borderRadius: "14px",
                    border: "1px solid rgba(226, 232, 240, 0.9)",
                    background: day.getMonth() === monthStart.getMonth() ? "#f8fafc" : "#eef2f7",
                    padding: "0.45rem",
                    display: "grid",
                    gap: "0.35rem",
                    alignContent: "start"
                });
                const label = createElement(document, "div", null, String(day.getDate()));
                label.style.fontSize = "11px";
                label.style.fontWeight = "700";
                label.style.color = "#475569";
                cell.appendChild(label);

                for (const event of state.surface.events.filter(item => sameDay(item.start, day))) {
                    const chip = createEventChip(document, event, event.accentColor);
                    chip.addEventListener("click", () => selectCalendarEvent(state, event));
                    chip.addEventListener("dblclick", () => openCalendarEvent(state, event));
                    cell.appendChild(chip);
                }

                grid.appendChild(cell);
                day.setDate(day.getDate() + 1);
            }

            panel.appendChild(grid);
            rootElement.appendChild(panel);
        }

        return rootElement;
    }

    function renderTimelineView(state, spanDays) {
        const document = state.host.ownerDocument;
        const wrapper = createElement(document, "div");
        Object.assign(wrapper.style, {
            display: "grid",
            gridTemplateColumns: `120px repeat(${spanDays}, minmax(0, 1fr))`,
            gap: "0.35rem"
        });

        const focusDate = state.surface.events.find(event => event.id === state.ui.selectedEventId)?.start || new Date();
        const rangeStart = new Date(focusDate);
        rangeStart.setHours(0, 0, 0, 0);
        rangeStart.setDate(rangeStart.getDate() - (spanDays === 7 ? rangeStart.getDay() : 0));

        wrapper.appendChild(createElement(document, "div"));
        for (let dayIndex = 0; dayIndex < spanDays; dayIndex++) {
            const dayHeader = createElement(document, "div", null, new Date(rangeStart.getFullYear(), rangeStart.getMonth(), rangeStart.getDate() + dayIndex).toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" }));
            dayHeader.style.fontSize = "11px";
            dayHeader.style.fontWeight = "700";
            dayHeader.style.color = "#475569";
            dayHeader.style.padding = "0 0.25rem 0.35rem";
            wrapper.appendChild(dayHeader);
        }

        for (let hour = 0; hour < 24; hour++) {
            const label = createElement(document, "div", null, `${String(hour).padStart(2, "0")}:00`);
            label.style.fontSize = "11px";
            label.style.color = "#64748b";
            label.style.paddingTop = "0.55rem";
            wrapper.appendChild(label);

            for (let dayIndex = 0; dayIndex < spanDays; dayIndex++) {
                const cellDate = new Date(rangeStart.getFullYear(), rangeStart.getMonth(), rangeStart.getDate() + dayIndex, hour);
                const cell = createElement(document, "div");
                Object.assign(cell.style, {
                    minHeight: "58px",
                    borderRadius: "12px",
                    border: "1px solid rgba(226, 232, 240, 0.9)",
                    background: "#ffffff",
                    padding: "0.3rem",
                    display: "grid",
                    gap: "0.25rem"
                });

                for (const event of state.surface.events.filter(item => sameDay(item.start, cellDate) && item.start.getHours() === hour)) {
                    const chip = createEventChip(document, event, event.accentColor);
                    chip.addEventListener("click", () => selectCalendarEvent(state, event));
                    chip.addEventListener("dblclick", () => openCalendarEvent(state, event));
                    cell.appendChild(chip);
                }

                wrapper.appendChild(cell);
            }
        }

        return wrapper;
    }

    function selectCalendarEvent(state, event) {
        state.ui.selectedEventId = event.id;
        state.dotNetRef.invokeMethodAsync("OnEventSelected", event.id);
        publishCalendarState(state);
        renderCalendar(state);
    }

    function openCalendarEvent(state, event) {
        selectCalendarEvent(state, event);
        state.dotNetRef.invokeMethodAsync("OnEventOpened", event.id);
    }

    function renderCalendar(state) {
        state.body.innerHTML = "";
        switch (state.ui.preferredView) {
            case "day":
                state.body.appendChild(renderTimelineView(state, 1));
                break;
            case "week":
                state.body.appendChild(renderTimelineView(state, 7));
                break;
            case "year":
                state.body.appendChild(renderMonthLikeView(state, 12));
                break;
            case "list":
                state.body.appendChild(renderListView(state));
                break;
            default:
                state.body.appendChild(renderMonthLikeView(state, 1));
                break;
        }
    }

    root.workbenchCalendar = {
        create(host, dotNetRef, surface, selectedEventId, viewStateJson) {
            clear(host);
            const document = host.ownerDocument;
            const wrapper = createElement(document, "div");
            Object.assign(wrapper.style, {
                display: "grid",
                gap: "0.9rem",
                minHeight: "36rem",
                padding: "1rem"
            });

            const toolbar = createElement(document, "div");
            Object.assign(toolbar.style, {
                display: "flex",
                gap: "0.5rem",
                flexWrap: "wrap"
            });

            const body = createElement(document, "div");
            body.style.display = "grid";
            body.style.gap = "0.75rem";

            wrapper.appendChild(toolbar);
            wrapper.appendChild(body);
            host.appendChild(wrapper);

            const normalizedSurface = normalizeCalendarSurface(surface);
            const state = {
                host,
                dotNetRef,
                surface: normalizedSurface,
                wrapper,
                toolbar,
                body,
                ui: buildCalendarState(viewStateJson, normalizedSurface, selectedEventId),
                publishViewState: debounce((payload) => dotNetRef.invokeMethodAsync("OnViewStateChanged", payload), 120)
            };

            for (const view of ["day", "week", "month", "year", "list"]) {
                const button = createElement(document, "button", null, view);
                button.type = "button";
                Object.assign(button.style, {
                    border: "1px solid rgba(148, 163, 184, 0.25)",
                    borderRadius: "12px",
                    padding: "0.5rem 0.75rem",
                    background: "#ffffff",
                    color: "#0f172a",
                    fontSize: "12px",
                    fontWeight: "700",
                    cursor: "pointer"
                });
                button.addEventListener("click", () => {
                    state.ui.preferredView = view;
                    publishCalendarState(state);
                    renderCalendar(state);
                });
                toolbar.appendChild(button);
            }

            host.__workbenchCalendar = state;
            renderCalendar(state);
        },
        update(host, surface, selectedEventId, viewStateJson) {
            const state = host.__workbenchCalendar;
            if (!state) {
                return;
            }

            state.surface = normalizeCalendarSurface(surface);
            state.ui = buildCalendarState(viewStateJson, state.surface, selectedEventId);
            renderCalendar(state);
        },
        dispose(host) {
            delete host.__workbenchCalendar;
            clear(host);
        }
    };
})();
