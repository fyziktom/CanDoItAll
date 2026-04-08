(function () {
    const root = window.CanDoItAll = window.CanDoItAll || {};

    function normalizeId(value) {
        const normalized = typeof value === "string" ? value.trim() : "";
        return normalized || null;
    }

    function uniqueIds(values) {
        const result = [];
        const seen = new Set();

        for (const value of Array.isArray(values) ? values : []) {
            const normalized = normalizeId(value);
            if (!normalized || seen.has(normalized)) {
                continue;
            }

            seen.add(normalized);
            result.push(normalized);
        }

        return result;
    }

    function normalize(selectedNodeIds, primaryNodeId) {
        const ordered = uniqueIds(selectedNodeIds);
        const normalizedPrimary = normalizeId(primaryNodeId);
        if (normalizedPrimary) {
            const existingIndex = ordered.indexOf(normalizedPrimary);
            if (existingIndex >= 0) {
                ordered.splice(existingIndex, 1);
            }

            ordered.unshift(normalizedPrimary);
        }

        return {
            primaryNodeId: ordered[0] || null,
            selectedNodeIds: ordered
        };
    }

    function replace(selectedNodeIds, primaryNodeId) {
        return normalize(selectedNodeIds, primaryNodeId);
    }

    function selectOne(nodeId) {
        return normalize([nodeId], nodeId);
    }

    function toggle(selectedNodeIds, nodeId, primaryNodeId) {
        const ordered = uniqueIds(selectedNodeIds);
        const normalizedNodeId = normalizeId(nodeId);
        if (!normalizedNodeId) {
            return normalize(ordered, primaryNodeId);
        }

        const existingIndex = ordered.indexOf(normalizedNodeId);
        if (existingIndex >= 0) {
            ordered.splice(existingIndex, 1);
            return normalize(ordered, normalizeId(primaryNodeId) === normalizedNodeId ? null : primaryNodeId);
        }

        ordered.push(normalizedNodeId);
        return normalize(ordered, primaryNodeId || normalizedNodeId);
    }

    function clear() {
        return {
            primaryNodeId: null,
            selectedNodeIds: []
        };
    }

    function removeMissing(selectedNodeIds, validNodeIds, primaryNodeId) {
        const valid = new Set(uniqueIds(validNodeIds));
        const filtered = uniqueIds(selectedNodeIds).filter(nodeId => valid.has(nodeId));
        const normalizedPrimary = normalizeId(primaryNodeId);
        return normalize(filtered, normalizedPrimary && valid.has(normalizedPrimary) ? normalizedPrimary : null);
    }

    root.selectionModel = {
        normalize,
        replace,
        selectOne,
        toggle,
        clear,
        removeMissing
    };
})();
