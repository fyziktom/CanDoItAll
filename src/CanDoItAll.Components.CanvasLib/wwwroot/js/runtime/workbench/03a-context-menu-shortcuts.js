(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};
    const shared = root.canvasWorkbenchModule;
    if (!shared) { throw new Error('CanvasLib workbench foundation must load before 03a-context-menu-shortcuts.js.'); }
    const { createElement } = shared;

    function normalizeShortcutKey(value) {
        const normalized = typeof value === "string" ? value.trim().toLowerCase() : "";
        return /^[a-z0-9]$/i.test(normalized) ? normalized : "";
    }

    function resolveShortcutDisplay(shortcutKey) {
        const normalized = normalizeShortcutKey(shortcutKey);
        return normalized ? normalized.toUpperCase() : "";
    }

    function getMenuActionShortcutKey(action) {
        return normalizeShortcutKey(action?.shortcutKey);
    }

    function buildShortcutLabelParts(labelText, shortcutKey) {
        const normalizedLabel = typeof labelText === "string" && labelText.trim().length > 0
            ? labelText.trim()
            : "Item";
        const normalizedShortcut = normalizeShortcutKey(shortcutKey);
        const shortcutDisplay = resolveShortcutDisplay(normalizedShortcut);
        const matchIndex = normalizedShortcut
            ? normalizedLabel.toLowerCase().indexOf(normalizedShortcut)
            : -1;

        return {
            labelText: normalizedLabel,
            shortcutDisplay,
            shortcutMatchesLabel: matchIndex >= 0,
            matchIndex,
            fullText: shortcutDisplay && matchIndex < 0
                ? `${shortcutDisplay} ${normalizedLabel}`
                : normalizedLabel
        };
    }

    function appendShortcutAwareText(labelTextElement, parts, document) {
        if (!parts.shortcutMatchesLabel) {
            labelTextElement.textContent = parts.labelText;
            return;
        }

        const prefix = parts.labelText.slice(0, parts.matchIndex);
        const matchedCharacter = parts.labelText.charAt(parts.matchIndex);
        const suffix = parts.labelText.slice(parts.matchIndex + 1);

        if (prefix) {
            labelTextElement.appendChild(document.createTextNode(prefix));
        }

        labelTextElement.appendChild(createElement(document, "span", "cw-context-menu__shortcut-char", matchedCharacter));

        if (suffix) {
            labelTextElement.appendChild(document.createTextNode(suffix));
        }
    }

    function buildContextMenuActionLabel(state, action) {
        const labelText = typeof shared.resolveMenuLabel === "function"
            ? shared.resolveMenuLabel(action)
            : (action?.menuLabel || action?.label || action?.actionId || "Item");
        const shortcutKey = getMenuActionShortcutKey(action);
        const parts = buildShortcutLabelParts(labelText, shortcutKey);
        const label = createElement(state.document, "strong", "cw-context-menu__label");
        const labelTextElement = createElement(state.document, "span", "cw-context-menu__label-text");
        label.dataset.fullText = parts.fullText;

        if (parts.shortcutDisplay && !parts.shortcutMatchesLabel) {
            const prefix = createElement(state.document, "span", "cw-context-menu__shortcut-prefix");
            prefix.appendChild(createElement(state.document, "span", "cw-context-menu__shortcut-char", parts.shortcutDisplay));
            label.appendChild(prefix);
        }

        appendShortcutAwareText(labelTextElement, parts, state.document);
        label.appendChild(labelTextElement);
        return label;
    }

    function resolveMenuActionAriaLabel(action) {
        const label = action?.label || (typeof shared.resolveMenuLabel === "function"
            ? shared.resolveMenuLabel(action)
            : action?.menuLabel || action?.actionId || "Canvas action");
        const shortcutDisplay = resolveShortcutDisplay(action?.shortcutKey);
        const suffix = action?.children?.length
            ? ", opens submenu"
            : "";

        return shortcutDisplay
            ? `${label}, shortcut ${shortcutDisplay}${suffix}`
            : `${label}${suffix}`;
    }

    function resolveActiveContextMenuLayer(state) {
        const layers = state?.contextMenuState?.layers;
        return Array.isArray(layers) && layers.length > 0
            ? layers[layers.length - 1]
            : null;
    }

    function findShortcutEntry(layerState, shortcutKey) {
        if (!layerState?.actionEntries || typeof layerState.actionEntries.values !== "function") {
            return null;
        }

        const normalizedShortcut = normalizeShortcutKey(shortcutKey);
        if (!normalizedShortcut) {
            return null;
        }

        for (const entry of layerState.actionEntries.values()) {
            if (getMenuActionShortcutKey(entry?.action) === normalizedShortcut) {
                return entry;
            }
        }

        return null;
    }

    function flashShortcutTarget(entry) {
        if (!(entry?.button instanceof HTMLElement)) {
            return;
        }

        entry.button.classList.add("is-keyboard-active");
        entry.button.focus({ preventScroll: true });
        window.setTimeout(() => {
            entry.button?.classList?.remove("is-keyboard-active");
        }, 220);
    }

    function routeContextMenuShortcut(state, event) {
        if (!state?.contextMenuState || state.contextMenu?.style.display === "none") {
            return false;
        }

        if (!event || event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) {
            return false;
        }

        const shortcutKey = normalizeShortcutKey(event.key);
        if (!shortcutKey) {
            return false;
        }

        const activeLayer = resolveActiveContextMenuLayer(state);
        const entry = findShortcutEntry(activeLayer, shortcutKey);
        if (!entry) {
            return false;
        }

        event.preventDefault();
        event.stopPropagation?.();
        flashShortcutTarget(entry);

        if (entry.action?.children?.length) {
            const openContextSubmenu = shared.openContextSubmenu;
            if (typeof openContextSubmenu === "function") {
                openContextSubmenu(state, activeLayer, entry.options, entry.action, entry.offset);
                return true;
            }
        }

        const executeContextAction = shared.executeContextAction;
        if (typeof executeContextAction === "function") {
            executeContextAction(
                state,
                entry.options?.node || null,
                entry.action,
                entry.options?.clientX,
                entry.options?.clientY,
                entry.options?.placementKind);
            return true;
        }

        return false;
    }

    Object.assign(shared, {
        getMenuActionShortcutKey,
        buildContextMenuActionLabel,
        resolveMenuActionAriaLabel,
        routeContextMenuShortcut
    });
})();
