(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const hosts = new WeakMap();

    function apply(host, snapshot) {
        host.dataset.boundaryReady = snapshot?.isEnabled ? "true" : "false";
        host.dataset.boundaryMetrics = String(Array.isArray(snapshot?.metrics) ? snapshot.metrics.length : 0);
    }

    root.tooltipPopoverHost = {
        create(host, snapshot) {
            if (!host) {
                return;
            }

            hosts.set(host, snapshot);
            apply(host, snapshot);
        },
        update(host, snapshot) {
            if (!host) {
                return;
            }

            hosts.set(host, snapshot);
            apply(host, snapshot);
        },
        dispose(host) {
            if (!host) {
                return;
            }

            hosts.delete(host);
            delete host.dataset.boundaryReady;
            delete host.dataset.boundaryMetrics;
        }
    };
})();
