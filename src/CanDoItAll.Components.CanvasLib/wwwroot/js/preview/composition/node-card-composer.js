(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const systems = new WeakMap();

    function apply(host, snapshot) {
        host.dataset.boundaryReady = snapshot?.cardTitle ? "true" : "false";
        host.dataset.boundaryMetrics = String(Array.isArray(snapshot?.metrics) ? snapshot.metrics.length : 0);
    }

    root.nodeCardComposer = {
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
