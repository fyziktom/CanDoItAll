(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const registry = root.promptFactoryComponents = root.promptFactoryComponents || {};

    registry.describeRecommendationOverlay = function describeRecommendationOverlay(element) {
        const items = Array.from(element?.querySelectorAll?.("[data-recommendation-tone]") || []);
        return {
            itemCount: items.length,
            tones: items.map(item => item.getAttribute("data-recommendation-tone") || "")
        };
    };
})();
