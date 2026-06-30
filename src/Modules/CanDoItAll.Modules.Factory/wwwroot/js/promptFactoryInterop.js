(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};

    root.promptFactory = root.promptFactory || {
        mountFloatingInspector(panel, handle) {
            root.canvasFloatingWindow?.mountLegacy(panel, handle);
        },
        resetFloatingInspector(panel) {
            root.canvasFloatingWindow?.resetLegacy(panel);
        }
    };
})();
