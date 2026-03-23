(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};

    function clamp(value, min, max) {
        return Math.max(min, Math.min(max, value));
    }

    function round(value) {
        return Math.round(value * 100) / 100;
    }

    function createFloatingInspectorState(panel) {
        const state = {
            panel,
            handle: null,
            container: null,
            observedContainer: null,
            observedPanel: null,
            resizeObserver: null,
            pointerId: null,
            startX: 0,
            startY: 0,
            startLeft: 0,
            startTop: 0,
            pointerMove: event => {
                if (event.pointerId !== state.pointerId || !state.container) {
                    return;
                }

                const nextLeft = state.startLeft + (event.clientX - state.startX);
                const nextTop = state.startTop + (event.clientY - state.startY);
                setFloatingInspectorPosition(state.panel, state.container, nextLeft, nextTop);
            },
            pointerUp: event => {
                if (event.pointerId !== state.pointerId) {
                    return;
                }

                releaseFloatingInspectorDrag(state);
            },
            pointerDown: event => {
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
                state.panel.dataset.dragged = "true";
                state.panel.classList.add("is-dragging");
                state.panel.style.right = "auto";
                state.panel.style.bottom = "auto";
                state.panel.style.left = `${round(state.startLeft)}px`;
                state.panel.style.top = `${round(state.startTop)}px`;

                window.addEventListener("pointermove", state.pointerMove, true);
                window.addEventListener("pointerup", state.pointerUp, true);
                window.addEventListener("pointercancel", state.pointerUp, true);
            }
        };

        panel.__pfFloatingInspectorState = state;
        return state;
    }

    function releaseFloatingInspectorDrag(state) {
        window.removeEventListener("pointermove", state.pointerMove, true);
        window.removeEventListener("pointerup", state.pointerUp, true);
        window.removeEventListener("pointercancel", state.pointerUp, true);
        state.pointerId = null;
        state.panel.classList.remove("is-dragging");
    }

    function updateFloatingInspectorHandle(state, handle) {
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

    function updateFloatingInspectorObserver(state) {
        if (state.observedContainer === state.container && state.observedPanel === state.panel) {
            return;
        }

        if (state.resizeObserver) {
            state.resizeObserver.disconnect();
            state.resizeObserver = null;
        }

        state.observedContainer = state.container;
        state.observedPanel = state.panel;
        if (!state.container || typeof window.ResizeObserver !== "function") {
            return;
        }

        state.resizeObserver = new window.ResizeObserver(() => {
            if (!state.panel.isConnected || !state.container) {
                return;
            }

            clampFloatingInspector(state.panel, state.container);
        });

        state.resizeObserver.observe(state.container);
        state.resizeObserver.observe(state.panel);
    }

    function setFloatingInspectorPosition(panel, container, left, top) {
        if (!panel || !container) {
            return;
        }

        const margin = 16;
        const containerRect = container.getBoundingClientRect();
        const maxLeft = Math.max(margin, container.clientWidth - panel.offsetWidth - margin);
        const workbenchFrame = panel.closest(".cw-workbench-frame");
        const toolbar = workbenchFrame ? workbenchFrame.querySelector(".cw-toolbar") : null;
        const minTop = toolbar
            ? Math.max(margin, Math.round(toolbar.getBoundingClientRect().bottom - containerRect.top + 12))
            : margin;
        const maxTop = Math.max(minTop, container.clientHeight - panel.offsetHeight - margin);
        const clampedLeft = clamp(round(left), margin, maxLeft);
        const clampedTop = clamp(round(top), minTop, maxTop);
        panel.style.left = `${clampedLeft}px`;
        panel.style.top = `${clampedTop}px`;
    }

    function clampFloatingInspector(panel, container) {
        if (!panel || !container || panel.dataset.dragged !== "true") {
            return;
        }

        const left = parseFloat(panel.style.left || "0");
        const top = parseFloat(panel.style.top || "0");
        setFloatingInspectorPosition(panel, container, left, top);
    }

    function mountFloatingInspector(panel, handle) {
        if (!panel || !panel.isConnected) {
            return;
        }

        const state = panel.__pfFloatingInspectorState || createFloatingInspectorState(panel);
        state.container = panel.closest(".cw-stage-surface") || panel.parentElement;
        updateFloatingInspectorHandle(state, handle);
        updateFloatingInspectorObserver(state);

        window.requestAnimationFrame(() => {
            if (!panel.isConnected || !state.container) {
                return;
            }

            clampFloatingInspector(panel, state.container);
        });
    }

    function resetFloatingInspector(panel) {
        if (!panel) {
            return;
        }

        const state = panel.__pfFloatingInspectorState;
        if (state) {
            releaseFloatingInspectorDrag(state);
        }

        panel.removeAttribute("data-dragged");
        panel.classList.remove("is-dragging");
        panel.style.left = "";
        panel.style.top = "";
        panel.style.right = "";
        panel.style.bottom = "";
    }

    root.promptFactory = root.promptFactory || {
        mountFloatingInspector(panel, handle) {
            mountFloatingInspector(panel, handle);
        },
        resetFloatingInspector(panel) {
            resetFloatingInspector(panel);
        }
    };
})();
