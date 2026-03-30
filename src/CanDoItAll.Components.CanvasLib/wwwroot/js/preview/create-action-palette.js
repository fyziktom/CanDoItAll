(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const systems = new WeakMap();

    function apply(host, snapshot) {
        const count = (snapshot?.quickActions?.length || 0) + (snapshot?.groupActions?.length || 0);
        host.dataset.boundaryReady = count > 0 ? "true" : "false";
        host.dataset.boundaryMetrics = String(Array.isArray(snapshot?.metrics) ? snapshot.metrics.length : 0);
    }

    root.createActionPalette = {
        create(host, snapshot) {
            if (!host) {
                return;
            }

            systems.set(host, snapshot);
            apply(host, snapshot);
        },
        update(host, snapshot) {
            if (!host) {
                return;
            }

            systems.set(host, snapshot);
            apply(host, snapshot);
        },
        dispose(host) {
            if (!host) {
                return;
            }

            systems.delete(host);
            delete host.dataset.boundaryReady;
            delete host.dataset.boundaryMetrics;
        }
    };
})();
