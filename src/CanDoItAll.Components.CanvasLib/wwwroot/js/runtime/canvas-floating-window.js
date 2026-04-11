(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const windowHosts = new WeakMap();
    const legacyHosts = new WeakMap();
    let zIndexSeed = 640;

    function clamp(value, min, max) {
        return Math.max(min, Math.min(max, value));
    }

    function round(value) {
        return Math.round(value * 100) / 100;
    }

    function createResizeObserver(callback) {
        return typeof window.ResizeObserver === "function"
            ? new window.ResizeObserver(callback)
            : null;
    }

    function resolveMargin(container) {
        return container && container.clientWidth < 720 ? 12 : 16;
    }

    function resolveContainer(host) {
        return host.closest(".cw-stage-surface") || host.parentElement;
    }

    function resolveSafeTop(container) {
        if (!container) {
            return 16;
        }

        const margin = resolveMargin(container);
        const workbenchFrame = container.closest(".cw-workbench-frame");
        const toolbar = workbenchFrame ? workbenchFrame.querySelector(".cw-toolbar") : null;
        if (!toolbar) {
            return margin;
        }

        const containerRect = container.getBoundingClientRect();
        const toolbarRect = toolbar.getBoundingClientRect();
        return Math.max(margin, Math.round(toolbarRect.bottom - containerRect.top + 12));
    }

    function resolveMaxWidth(container, options) {
        const margin = resolveMargin(container);
        const available = Math.max(options.minWidth, container.clientWidth - margin * 2);
        return options.maxWidth ? Math.min(options.maxWidth, available) : available;
    }

    function resolveMaxHeight(container, options) {
        const margin = resolveMargin(container);
        const safeTop = resolveSafeTop(container);
        const available = Math.max(options.minHeight, container.clientHeight - safeTop - margin);
        return options.maxHeight ? Math.min(options.maxHeight, available) : available;
    }

    function resolveEffectiveSize(container, options, state) {
        const maxWidth = resolveMaxWidth(container, options);
        const maxHeight = resolveMaxHeight(container, options);
        const width = clamp(state.width ?? options.defaultWidth, options.minWidth, maxWidth);
        const height = clamp(state.height ?? options.defaultHeight, options.minHeight, maxHeight);
        if (container.clientWidth < 720) {
            return {
                width: maxWidth,
                height: Math.min(height, maxHeight)
            };
        }

        return { width, height };
    }

    function resolveEffectivePosition(container, options, state, size) {
        const margin = resolveMargin(container);
        const safeTop = resolveSafeTop(container);
        const maxLeft = Math.max(margin, container.clientWidth - size.width - margin);
        const maxTop = Math.max(safeTop, container.clientHeight - size.height - margin);
        const defaultLeft = options.placement === "top-left"
            ? margin
            : Math.max(margin, container.clientWidth - size.width - margin);
        const left = clamp(state.left ?? defaultLeft, margin, maxLeft);
        const top = clamp(state.top ?? safeTop, safeTop, maxTop);

        return { left, top };
    }

    function applyWindowGeometry(state) {
        if (!state.host.isConnected || !state.container) {
            return;
        }

        const host = state.host;
        const options = state.options;
        const minimized = !!options.state?.isMinimized;
        host.classList.toggle("is-minimized", minimized);
        host.dataset.windowId = options.windowId;
        host.style.setProperty("--cw-window-min-width", `${options.minWidth}px`);
        host.style.setProperty("--cw-window-min-height", `${options.minHeight}px`);
        if (options.maxWidth) {
            host.style.setProperty("--cw-window-max-width", `${options.maxWidth}px`);
        }
        else {
            host.style.removeProperty("--cw-window-max-width");
        }

        if (options.maxHeight) {
            host.style.setProperty("--cw-window-max-height", `${options.maxHeight}px`);
        }
        else {
            host.style.removeProperty("--cw-window-max-height");
        }

        if (minimized || options.allowResize === false) {
            host.style.resize = "none";
        }
        else {
            host.style.resize = "both";
        }

        const effectiveState = options.state || {};
        const size = resolveEffectiveSize(state.container, options, effectiveState);
        const position = resolveEffectivePosition(state.container, options, effectiveState, size);
        host.style.left = `${round(position.left)}px`;
        host.style.top = `${round(position.top)}px`;

        if (!minimized) {
            host.style.width = `${round(size.width)}px`;
            host.style.height = `${round(size.height)}px`;
        }
        else {
            host.style.removeProperty("width");
            host.style.removeProperty("height");
        }
    }

    function bringToFront(host) {
        zIndexSeed += 1;
        host.style.zIndex = String(zIndexSeed);
    }

    function releasePointerDrag(state) {
        window.removeEventListener("pointermove", state.pointerMove, true);
        window.removeEventListener("pointerup", state.pointerUp, true);
        window.removeEventListener("pointercancel", state.pointerUp, true);
        state.pointerId = null;
        state.host.classList.remove("is-dragging");
    }

    function buildGeometryPayload(state) {
        if (!state.container || !state.host.isConnected) {
            return null;
        }

        const hostRect = state.host.getBoundingClientRect();
        const containerRect = state.container.getBoundingClientRect();
        const minimized = !!state.options.state?.isMinimized;
        return {
            left: round(hostRect.left - containerRect.left),
            top: round(hostRect.top - containerRect.top),
            width: minimized ? state.options.state?.width ?? null : round(state.host.offsetWidth),
            height: minimized ? state.options.state?.height ?? null : round(state.host.offsetHeight)
        };
    }

    function rememberGeometry(state) {
        const payload = buildGeometryPayload(state);
        if (!payload) {
            return;
        }

        state.lastPayload = payload;
    }

    function publishGeometry(state) {
        if (!state.dotNetRef) {
            rememberGeometry(state);
            return;
        }

        const payload = buildGeometryPayload(state);
        if (!payload) {
            return;
        }

        if (state.lastPayload &&
            state.lastPayload.left === payload.left &&
            state.lastPayload.top === payload.top &&
            state.lastPayload.width === payload.width &&
            state.lastPayload.height === payload.height) {
            return;
        }

        state.lastPayload = payload;
        state.dotNetRef.invokeMethodAsync("OnGeometryChanged", payload.left, payload.top, payload.width, payload.height)
            .catch(() => { });
    }

    function scheduleGeometryCommit(state, delay) {
        window.clearTimeout(state.geometryTimer);
        state.geometryTimer = window.setTimeout(() => publishGeometry(state), delay ?? 180);
    }

    function clampDraggedWindow(state, nextLeft, nextTop) {
        if (!state.container) {
            return;
        }

        const host = state.host;
        const margin = resolveMargin(state.container);
        const safeTop = resolveSafeTop(state.container);
        const width = host.offsetWidth;
        const height = host.offsetHeight;
        const maxLeft = Math.max(margin, state.container.clientWidth - width - margin);
        const maxTop = Math.max(safeTop, state.container.clientHeight - height - margin);
        host.style.left = `${round(clamp(nextLeft, margin, maxLeft))}px`;
        host.style.top = `${round(clamp(nextTop, safeTop, maxTop))}px`;
    }

    function attachWindowEvents(state) {
        if (state.eventsAttached) {
            return;
        }

        state.pointerMove = event => {
            if (event.pointerId !== state.pointerId) {
                return;
            }

            clampDraggedWindow(
                state,
                state.startLeft + (event.clientX - state.startX),
                state.startTop + (event.clientY - state.startY));
        };

        state.pointerUp = event => {
            if (event.pointerId !== state.pointerId) {
                return;
            }

            releasePointerDrag(state);
            publishGeometry(state);
        };

        state.pointerDown = event => {
            if (event.button !== 0 || !state.container) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            bringToFront(state.host);

            const hostRect = state.host.getBoundingClientRect();
            const containerRect = state.container.getBoundingClientRect();
            state.pointerId = event.pointerId;
            state.startX = event.clientX;
            state.startY = event.clientY;
            state.startLeft = hostRect.left - containerRect.left;
            state.startTop = hostRect.top - containerRect.top;
            state.host.classList.add("is-dragging");
            state.host.style.left = `${round(state.startLeft)}px`;
            state.host.style.top = `${round(state.startTop)}px`;

            window.addEventListener("pointermove", state.pointerMove, true);
            window.addEventListener("pointerup", state.pointerUp, true);
            window.addEventListener("pointercancel", state.pointerUp, true);
        };

        state.host.addEventListener("pointerdown", () => bringToFront(state.host), true);
        state.host.addEventListener("focusin", () => bringToFront(state.host), true);
        state.eventsAttached = true;
    }

    function syncWindowHandle(state) {
        const handle = state.host.querySelector("[data-cw-window-drag]");
        if (state.handle === handle) {
            return;
        }

        if (state.handle) {
            state.handle.removeEventListener("pointerdown", state.pointerDown, true);
        }

        state.handle = handle || null;
        if (state.handle) {
            state.handle.addEventListener("pointerdown", state.pointerDown, true);
        }
    }

    function syncWindowObservers(state) {
        if (state.resizeObserver) {
            state.resizeObserver.disconnect();
        }

        state.resizeObserver = createResizeObserver(() => {
            if (!state.host.isConnected || !state.container) {
                return;
            }

            if (state.host.classList.contains("is-dragging")) {
                return;
            }

            clampDraggedWindow(
                state,
                parseFloat(state.host.style.left || "0") || 0,
                parseFloat(state.host.style.top || "0") || 0);
            scheduleGeometryCommit(state);
        });

        if (state.resizeObserver) {
            state.resizeObserver.observe(state.host);
            state.resizeObserver.observe(state.container);
        }
    }

    function createWindowState(host, dotNetRef, options) {
        const state = {
            host,
            dotNetRef,
            options,
            container: resolveContainer(host),
            handle: null,
            resizeObserver: null,
            geometryTimer: 0,
            eventsAttached: false,
            pointerId: null,
            startX: 0,
            startY: 0,
            startLeft: 0,
            startTop: 0,
            lastPayload: null
        };

        attachWindowEvents(state);
        syncWindowHandle(state);
        syncWindowObservers(state);
        return state;
    }

    function updateWindowState(host, dotNetRef, options) {
        const normalized = {
            windowId: options.windowId || host.dataset.windowId || "canvas-window",
            placement: options.placement || "top-right",
            defaultWidth: options.defaultWidth || 360,
            defaultHeight: options.defaultHeight || 480,
            minWidth: options.minWidth || 280,
            minHeight: options.minHeight || 180,
            maxWidth: options.maxWidth || null,
            maxHeight: options.maxHeight || null,
            allowResize: options.allowResize !== false,
            state: options.state || {}
        };

        const state = windowHosts.get(host) || createWindowState(host, dotNetRef, normalized);
        state.dotNetRef = dotNetRef || state.dotNetRef;
        state.container = resolveContainer(host);
        state.options = normalized;
        syncWindowHandle(state);
        applyWindowGeometry(state);
        rememberGeometry(state);
        bringToFront(host);
        windowHosts.set(host, state);
    }

    function disposeWindowState(host) {
        const state = windowHosts.get(host);
        if (!state) {
            return;
        }

        releasePointerDrag(state);
        window.clearTimeout(state.geometryTimer);
        if (state.handle) {
            state.handle.removeEventListener("pointerdown", state.pointerDown, true);
        }

        if (state.resizeObserver) {
            state.resizeObserver.disconnect();
        }

        windowHosts.delete(host);
    }

    function createLegacyState(panel) {
        const state = {
            panel,
            handle: null,
            container: null,
            pointerId: null,
            startX: 0,
            startY: 0,
            startLeft: 0,
            startTop: 0,
            resizeObserver: null
        };

        state.pointerMove = event => {
            if (event.pointerId !== state.pointerId || !state.container) {
                return;
            }

            const nextLeft = state.startLeft + (event.clientX - state.startX);
            const nextTop = state.startTop + (event.clientY - state.startY);
            const margin = resolveMargin(state.container);
            const safeTop = resolveSafeTop(state.container);
            const maxLeft = Math.max(margin, state.container.clientWidth - state.panel.offsetWidth - margin);
            const maxTop = Math.max(safeTop, state.container.clientHeight - state.panel.offsetHeight - margin);
            state.panel.style.left = `${round(clamp(nextLeft, margin, maxLeft))}px`;
            state.panel.style.top = `${round(clamp(nextTop, safeTop, maxTop))}px`;
        };

        state.pointerUp = event => {
            if (event.pointerId !== state.pointerId) {
                return;
            }

            window.removeEventListener("pointermove", state.pointerMove, true);
            window.removeEventListener("pointerup", state.pointerUp, true);
            window.removeEventListener("pointercancel", state.pointerUp, true);
            state.pointerId = null;
            state.panel.classList.remove("is-dragging");
        };

        state.pointerDown = event => {
            if (event.button !== 0 || !state.container) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            const panelRect = state.panel.getBoundingClientRect();
            const containerRect = state.container.getBoundingClientRect();
            state.pointerId = event.pointerId;
            state.startX = event.clientX;
            state.startY = event.clientY;
            state.startLeft = panelRect.left - containerRect.left;
            state.startTop = panelRect.top - containerRect.top;
            state.panel.classList.add("is-dragging");
            state.panel.style.right = "auto";
            state.panel.style.bottom = "auto";
            state.panel.style.left = `${round(state.startLeft)}px`;
            state.panel.style.top = `${round(state.startTop)}px`;

            window.addEventListener("pointermove", state.pointerMove, true);
            window.addEventListener("pointerup", state.pointerUp, true);
            window.addEventListener("pointercancel", state.pointerUp, true);
        };

        return state;
    }

    function mountLegacy(panel, handle) {
        if (!panel || !panel.isConnected) {
            return;
        }

        const state = legacyHosts.get(panel) || createLegacyState(panel);
        state.container = resolveContainer(panel);
        if (state.handle && state.handle !== handle) {
            state.handle.removeEventListener("pointerdown", state.pointerDown, true);
        }

        state.handle = handle || null;
        if (state.handle) {
            state.handle.addEventListener("pointerdown", state.pointerDown, true);
        }

        if (state.resizeObserver) {
            state.resizeObserver.disconnect();
        }

        state.resizeObserver = createResizeObserver(() => {
            if (!panel.isConnected || !state.container) {
                return;
            }

            const margin = resolveMargin(state.container);
            const safeTop = resolveSafeTop(state.container);
            const currentLeft = parseFloat(panel.style.left || "0");
            const currentTop = parseFloat(panel.style.top || "0");
            const maxLeft = Math.max(margin, state.container.clientWidth - panel.offsetWidth - margin);
            const maxTop = Math.max(safeTop, state.container.clientHeight - panel.offsetHeight - margin);
            panel.style.left = `${round(clamp(currentLeft || margin, margin, maxLeft))}px`;
            panel.style.top = `${round(clamp(currentTop || safeTop, safeTop, maxTop))}px`;
        });

        if (state.resizeObserver && state.container) {
            state.resizeObserver.observe(state.container);
            state.resizeObserver.observe(panel);
        }

        legacyHosts.set(panel, state);
    }

    function resetLegacy(panel) {
        if (!panel) {
            return;
        }

        const state = legacyHosts.get(panel);
        if (state) {
            window.removeEventListener("pointermove", state.pointerMove, true);
            window.removeEventListener("pointerup", state.pointerUp, true);
            window.removeEventListener("pointercancel", state.pointerUp, true);
        }

        panel.classList.remove("is-dragging");
        panel.style.left = "";
        panel.style.top = "";
        panel.style.right = "";
        panel.style.bottom = "";
    }

    root.canvasFloatingWindow = {
        create(host, dotNetRef, options) {
            if (!host) {
                return;
            }

            updateWindowState(host, dotNetRef, options || {});
        },
        update(host, options) {
            if (!host) {
                return;
            }

            updateWindowState(host, null, options || {});
        },
        dispose(host) {
            if (!host) {
                return;
            }

            disposeWindowState(host);
        },
        mountLegacy(panel, handle) {
            mountLegacy(panel, handle);
        },
        resetLegacy(panel) {
            resetLegacy(panel);
        }
    };
})();
