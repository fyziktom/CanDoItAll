(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shortcutHandlers = new WeakMap();

    root.promptFactoryUndoRedo = root.promptFactoryUndoRedo || {
        shouldHandleHistoryShortcut() {
            const activeElement = document.activeElement;
            if (!activeElement) {
                return true;
            }

            const tagName = activeElement.tagName?.toLowerCase?.() || "";
            return tagName !== "input" &&
                tagName !== "textarea" &&
                !activeElement.isContentEditable;
        },
        registerHistoryShortcuts(dotNetRef) {
            if (!dotNetRef) {
                return;
            }

            this.unregisterHistoryShortcuts(dotNetRef);
            const handler = (event) => {
                const key = event.key?.trim()?.toLowerCase?.() || "";
                const isHistoryShortcut = (event.ctrlKey || event.metaKey) &&
                    !event.altKey &&
                    (key === "z" || key === "y");
                if (!isHistoryShortcut || !this.shouldHandleHistoryShortcut()) {
                    return;
                }

                event.preventDefault();
                dotNetRef.invokeMethodAsync(
                    "HandleHistoryShortcutAsync",
                    event.key || "",
                    !!event.ctrlKey,
                    !!event.metaKey,
                    !!event.shiftKey,
                    !!event.altKey)
                    .catch(() => { });
            };

            shortcutHandlers.set(dotNetRef, handler);
            window.addEventListener("keydown", handler, true);
        },
        unregisterHistoryShortcuts(dotNetRef) {
            const handler = shortcutHandlers.get(dotNetRef);
            if (!handler) {
                return;
            }

            window.removeEventListener("keydown", handler, true);
            shortcutHandlers.delete(dotNetRef);
        }
    };
})();
