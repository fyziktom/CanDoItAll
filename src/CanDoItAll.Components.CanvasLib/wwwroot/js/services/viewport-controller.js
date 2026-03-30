(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const defaults = Object.freeze({
        minZoom: 0.15,
        maxZoom: 1.75,
        defaultZoom: 1,
        defaultPanX: 90,
        defaultPanY: 110,
        clampMarginX: 160,
        clampMarginY: 140,
        fitPadding: 120
    });

    function clamp(value, min, max) {
        return Math.max(min, Math.min(max, value));
    }

    function round(value) {
        return Math.round(value * 100) / 100;
    }

    function normalizeNumber(value, fallback) {
        return Number.isFinite(value) ? value : fallback;
    }

    function normalizeZoom(value) {
        return clamp(normalizeNumber(value, defaults.defaultZoom), defaults.minZoom, defaults.maxZoom);
    }

    function normalizeUiState(uiState) {
        return {
            zoom: normalizeZoom(uiState?.zoom),
            panX: normalizeNumber(uiState?.panX, defaults.defaultPanX),
            panY: normalizeNumber(uiState?.panY, defaults.defaultPanY)
        };
    }

    function normalizeBounds(bounds) {
        if (!bounds
            || !Number.isFinite(bounds.minX)
            || !Number.isFinite(bounds.maxX)
            || !Number.isFinite(bounds.minY)
            || !Number.isFinite(bounds.maxY)) {
            return null;
        }

        return {
            minX: bounds.minX,
            maxX: bounds.maxX,
            minY: bounds.minY,
            maxY: bounds.maxY
        };
    }

    function clampPanToScene(request) {
        const bounds = normalizeBounds(request?.bounds);
        const zoom = normalizeZoom(request?.zoom);
        const hostWidth = normalizeNumber(request?.hostWidth, 0);
        const hostHeight = normalizeNumber(request?.hostHeight, 0);
        const panX = normalizeNumber(request?.panX, defaults.defaultPanX);
        const panY = normalizeNumber(request?.panY, defaults.defaultPanY);

        if (!bounds || hostWidth <= 0 || hostHeight <= 0) {
            return {
                zoom,
                panX: round(panX),
                panY: round(panY)
            };
        }

        const marginX = Math.max(defaults.clampMarginX, hostWidth * 0.5);
        const marginY = Math.max(defaults.clampMarginY, hostHeight * 0.5);
        const minPanX = hostWidth - marginX - (bounds.maxX * zoom);
        const maxPanX = marginX - (bounds.minX * zoom);
        const minPanY = hostHeight - marginY - (bounds.maxY * zoom);
        const maxPanY = marginY - (bounds.minY * zoom);

        return {
            zoom,
            panX: round(clamp(panX, Math.min(minPanX, maxPanX), Math.max(minPanX, maxPanX))),
            panY: round(clamp(panY, Math.min(minPanY, maxPanY), Math.max(minPanY, maxPanY)))
        };
    }

    function createFitViewTarget(request) {
        const bounds = normalizeBounds(request?.bounds);
        const hostWidth = normalizeNumber(request?.hostWidth, 0);
        const hostHeight = normalizeNumber(request?.hostHeight, 0);
        if (!bounds || hostWidth <= 0 || hostHeight <= 0) {
            return normalizeUiState(request);
        }

        const padding = normalizeNumber(request?.padding, defaults.fitPadding);
        const width = Math.max(bounds.maxX - bounds.minX, 320);
        const height = Math.max(bounds.maxY - bounds.minY, 240);
        const zoom = clamp(Math.min(
            (hostWidth - padding) / width,
            (hostHeight - padding) / height), defaults.minZoom, defaults.maxZoom);

        return clampPanToScene({
            bounds,
            hostWidth,
            hostHeight,
            zoom,
            panX: (hostWidth / 2) - ((bounds.minX + (width / 2)) * zoom),
            panY: (hostHeight / 2) - ((bounds.minY + (height / 2)) * zoom)
        });
    }

    function createFocusTarget(request) {
        const bounds = normalizeBounds(request?.bounds);
        const hostWidth = normalizeNumber(request?.hostWidth, 0);
        const hostHeight = normalizeNumber(request?.hostHeight, 0);
        const pointX = normalizeNumber(request?.pointX, 0);
        const pointY = normalizeNumber(request?.pointY, 0);
        const zoom = normalizeZoom(request?.zoom);

        return clampPanToScene({
            bounds,
            hostWidth,
            hostHeight,
            zoom,
            panX: (hostWidth / 2) - (pointX * zoom),
            panY: (hostHeight / 2) - (pointY * zoom)
        });
    }

    function hostToScene(request) {
        const state = normalizeUiState(request);
        return {
            x: round((normalizeNumber(request?.pointX, 0) - state.panX) / Math.max(state.zoom, 0.001)),
            y: round((normalizeNumber(request?.pointY, 0) - state.panY) / Math.max(state.zoom, 0.001))
        };
    }

    function sceneToHost(request) {
        const state = normalizeUiState(request);
        return {
            x: round((normalizeNumber(request?.pointX, 0) * state.zoom) + state.panX),
            y: round((normalizeNumber(request?.pointY, 0) * state.zoom) + state.panY)
        };
    }

    function zoomAroundPoint(request) {
        const current = normalizeUiState(request);
        const hostWidth = normalizeNumber(request?.hostWidth, 0);
        const hostHeight = normalizeNumber(request?.hostHeight, 0);
        const anchorX = normalizeNumber(request?.anchorX, hostWidth / 2);
        const anchorY = normalizeNumber(request?.anchorY, hostHeight / 2);
        const nextZoom = normalizeZoom((normalizeNumber(request?.percent, 100)) / 100);
        const world = hostToScene({
            zoom: current.zoom,
            panX: current.panX,
            panY: current.panY,
            pointX: anchorX,
            pointY: anchorY
        });

        return clampPanToScene({
            bounds: request?.bounds,
            hostWidth,
            hostHeight,
            zoom: nextZoom,
            panX: anchorX - (world.x * nextZoom),
            panY: anchorY - (world.y * nextZoom)
        });
    }

    root.viewportController = {
        getConstants() {
            return { ...defaults };
        },
        normalizeUiState,
        clampPanToScene,
        createFitViewTarget,
        createFocusTarget,
        hostToScene,
        sceneToHost,
        zoomAroundPoint
    };
})();
