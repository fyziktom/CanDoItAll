(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const registry = root.promptFactoryComponents = root.promptFactoryComponents || {};

    registry.describeAttachmentNode = function describeAttachmentNode(node) {
        return {
            id: node?.id || "",
            mediaKind: node?.mediaKind || "",
            statusPill: node?.statusPill || "",
            fileName: node?.mediaFileName || ""
        };
    };
})();
