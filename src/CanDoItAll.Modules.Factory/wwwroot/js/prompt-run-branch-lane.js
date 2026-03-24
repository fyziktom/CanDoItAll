(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const registry = root.promptFactoryComponents = root.promptFactoryComponents || {};

    registry.describeBranchLane = function describeBranchLane(node) {
        return {
            id: node?.id || "",
            branchKey: node?.subtitle || "",
            itemCount: node?.statusPill || "",
            isPrimary: Array.isArray(node?.chips) && node.chips.some(chip => chip?.text === "Primary")
        };
    };
})();
