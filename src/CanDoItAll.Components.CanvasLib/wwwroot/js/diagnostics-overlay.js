(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const overlays = new WeakMap();

    function apply(host, snapshot) {
        host.dataset.boundaryReady = "true";
        host.dataset.boundaryVisible = snapshot?.isVisible ? "true" : "false";
        host.dataset.boundaryMetrics = String(Array.isArray(snapshot?.metrics) ? snapshot.metrics.length : 0);
    }

    root.diagnosticsOverlay = {
        create(host, snapshot) {
            if (!host) {
                return;
            }

            overlays.set(host, snapshot);
            apply(host, snapshot);
        },
        update(host, snapshot) {
            if (!host) {
                return;
            }

            overlays.set(host, snapshot);
            apply(host, snapshot);
        },
        dispose(host) {
            if (!host) {
                return;
            }

            overlays.delete(host);
            delete host.dataset.boundaryReady;
            delete host.dataset.boundaryVisible;
            delete host.dataset.boundaryMetrics;
        }
    };
})();
