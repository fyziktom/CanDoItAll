(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const bridges = new WeakMap();

    function apply(host, snapshot) {
        host.dataset.boundaryReady = snapshot?.isEnabled ? "true" : "false";
        host.dataset.boundaryMetrics = String(Array.isArray(snapshot?.metrics) ? snapshot.metrics.length : 0);
    }

    root.clipboardBridge = {
        create(host, snapshot) {
            if (!host) {
                return;
            }

            bridges.set(host, snapshot);
            apply(host, snapshot);
        },
        update(host, snapshot) {
            if (!host) {
                return;
            }

            bridges.set(host, snapshot);
            apply(host, snapshot);
        },
        dispose(host) {
            if (!host) {
                return;
            }

            bridges.delete(host);
            delete host.dataset.boundaryReady;
            delete host.dataset.boundaryMetrics;
        }
    };
})();
