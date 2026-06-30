(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const registry = root.workbenchComponents = root.workbenchComponents || {};

    registry.describeValidationOverlay = function describeValidationOverlay(element) {
        const metrics = Array.from(element?.querySelectorAll?.("[data-validation-metric]") || []);
        return {
            metricCount: metrics.length,
            names: metrics.map(metric => metric.getAttribute("data-validation-metric") || "")
        };
    };
})();
