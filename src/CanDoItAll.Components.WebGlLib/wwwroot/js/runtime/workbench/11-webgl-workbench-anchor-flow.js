import {
    connectionActions,
    toolModes
} from "./02-webgl-workbench-core.js";

export function clearConnectionDrafts(state) {
    state.chromeState.connectSourceNodeId = null;
    state.chromeState.connectSourceAnchorId = null;
    state.chromeState.reconnectEdgeId = null;
}

export function resolveCategoryKey(value) {
    const category = (value?.categoryKey || "").toLowerCase();
    if (!category) {
        return "default";
    }

    if (category.includes("branch")) {
        return "branch";
    }

    if (category.includes("struct")) {
        return "structural";
    }

    if (category.includes("artifact")) {
        return "artifact";
    }

    if (category.includes("message")) {
        return "messaging";
    }

    if (category.includes("decision")) {
        return "decision";
    }

    if (category.includes("responsibility")) {
        return "responsibility";
    }

    return category;
}

export function areCategoriesCompatible(sourceCategory, targetCategory) {
    if (sourceCategory === targetCategory) {
        return true;
    }

    if (sourceCategory === "default" || targetCategory === "default") {
        return true;
    }

    return sourceCategory === "branch" && targetCategory === "structural";
}

export function resolveConnectionKind(sourceAnchor, edge) {
    if ((edge?.kind || "").trim()) {
        return edge.kind;
    }

    const category = resolveCategoryKey(sourceAnchor);
    return category === "messaging"
        ? "messaging"
        : "flow";
}

export function resolveNodeAnchorsByRole(node, role) {
    return (node?.anchors || []).filter(anchor => anchor.role === role);
}

export function resolveNodeById(state, nodeId) {
    return nodeId
        ? state.nodeLookup.get(nodeId) || null
        : null;
}

export function resolveAnchorById(state, anchorId) {
    return anchorId
        ? state.anchorLookup.get(anchorId) || null
        : null;
}

export function resolveSingleOutputAnchor(state, nodeId) {
    const node = resolveNodeById(state, nodeId);
    if (!node) {
        return null;
    }

    const outputs = resolveNodeAnchorsByRole(node, "output");
    return outputs.length === 1
        ? outputs[0]
        : null;
}

export function resolveConnectSourceAnchor(state) {
    return resolveAnchorById(state, state.chromeState?.connectSourceAnchorId || "");
}

export function resolveReconnectEdge(state) {
    const edgeId = state.chromeState?.reconnectEdgeId || "";
    return (state.surface?.edges || []).find(candidate => candidate.id === edgeId) || null;
}

export function resolveReconnectSourceAnchor(state) {
    const edge = resolveReconnectEdge(state);
    return edge
        ? resolveAnchorById(state, edge.sourceAnchorId)
        : null;
}

export function resolveActiveAuthoringSourceAnchor(state) {
    const connectSourceAnchor = resolveConnectSourceAnchor(state);
    if (connectSourceAnchor) {
        return connectSourceAnchor;
    }

    return resolveReconnectSourceAnchor(state);
}

export function resolvePendingSourceOutputAnchorIds(state) {
    if (state.chromeState?.connectSourceAnchorId || !state.chromeState?.connectSourceNodeId) {
        return new Set();
    }

    const node = resolveNodeById(state, state.chromeState.connectSourceNodeId);
    return new Set(resolveNodeAnchorsByRole(node, "output").map(anchor => anchor.id));
}

function isCompatibleAnchorPair(sourceAnchor, targetAnchor) {
    if (!sourceAnchor || !targetAnchor) {
        return false;
    }

    if (sourceAnchor.id === targetAnchor.id || sourceAnchor.nodeId === targetAnchor.nodeId) {
        return false;
    }

    if (sourceAnchor.role !== "output" || targetAnchor.role !== "input") {
        return false;
    }

    return areCategoriesCompatible(resolveCategoryKey(sourceAnchor), resolveCategoryKey(targetAnchor));
}

export function resolveCompatibleTargetAnchors(state, sourceAnchorId, options = {}) {
    const sourceAnchor = resolveAnchorById(state, sourceAnchorId);
    if (!sourceAnchor) {
        return [];
    }

    const targetNodeId = options.nodeId || "";
    const excludeAnchorId = options.excludeAnchorId || "";
    const compatibleAnchors = [];

    for (const node of state.surface.nodes || []) {
        if (targetNodeId && node.id !== targetNodeId) {
            continue;
        }

        for (const anchor of resolveNodeAnchorsByRole(node, "input")) {
            if (anchor.id === excludeAnchorId || !isCompatibleAnchorPair(sourceAnchor, anchor)) {
                continue;
            }

            compatibleAnchors.push(anchor);
        }
    }

    return compatibleAnchors;
}

export function resolveCompatibleTargetAnchorIds(state, sourceAnchorId, options = {}) {
    return new Set(resolveCompatibleTargetAnchors(state, sourceAnchorId, options).map(anchor => anchor.id));
}

export function resolveSingleCompatibleTargetAnchor(state, sourceAnchorId, targetNodeId, options = {}) {
    const compatibleAnchors = resolveCompatibleTargetAnchors(state, sourceAnchorId, {
        ...options,
        nodeId: targetNodeId
    });
    return compatibleAnchors.length === 1
        ? compatibleAnchors[0]
        : null;
}

export function applyConnectionSourceAnchor(state, anchorId) {
    const anchor = resolveAnchorById(state, anchorId);
    if (!anchor || anchor.role !== "output") {
        return false;
    }

    state.chromeState.connectSourceNodeId = anchor.nodeId;
    state.chromeState.connectSourceAnchorId = anchor.id;
    return true;
}

export function focusConnectionSourceNode(state, nodeId) {
    if (!resolveNodeById(state, nodeId)) {
        return false;
    }

    state.chromeState.connectSourceNodeId = nodeId;
    state.chromeState.connectSourceAnchorId = null;
    return true;
}

export function buildConnectionRequest(state, sourceAnchorId, targetAnchorId, actionId = connectionActions.connect, edgeId = null) {
    const targetAnchor = resolveAnchorById(state, targetAnchorId);
    const reconnectEdge = actionId === connectionActions.reconnectTarget
        ? (state.surface.edges || []).find(candidate => candidate.id === edgeId) || null
        : null;
    const sourceAnchor = reconnectEdge
        ? resolveAnchorById(state, reconnectEdge.sourceAnchorId)
        : resolveAnchorById(state, sourceAnchorId);

    if (!sourceAnchor || !targetAnchor || !isCompatibleAnchorPair(sourceAnchor, targetAnchor)) {
        return null;
    }

    const sourceNode = resolveNodeById(state, sourceAnchor.nodeId);
    const targetNode = resolveNodeById(state, targetAnchor.nodeId);
    if (!sourceNode || !targetNode || sourceNode.id === targetNode.id) {
        return null;
    }

    return {
        actionId,
        edgeId: reconnectEdge?.id || null,
        sourceNodeId: reconnectEdge?.sourceNodeId || sourceNode.id,
        sourceAnchorId: reconnectEdge?.sourceAnchorId || sourceAnchor.id,
        sourcePortId: reconnectEdge?.sourcePortId || sourceAnchor.portId || null,
        targetNodeId: targetNode.id,
        targetAnchorId: targetAnchor.id,
        targetPortId: targetAnchor.portId || null,
        kind: reconnectEdge?.kind || resolveConnectionKind(sourceAnchor, reconnectEdge),
        categoryKey: reconnectEdge?.categoryKey || sourceAnchor.categoryKey || ""
    };
}

function resolveAnchorSummary(anchor) {
    return anchor?.label || anchor?.portId || anchor?.id || "anchor";
}

export function resolveConnectionHintText(state) {
    const toolMode = state.surface?.uiState?.toolMode || toolModes.select;
    if (toolMode === toolModes.connect) {
        const sourceAnchor = resolveConnectSourceAnchor(state);
        if (sourceAnchor) {
            const sourceNode = resolveNodeById(state, sourceAnchor.nodeId);
            return `Connect mode | choose target input for ${sourceNode?.title || sourceAnchor.nodeId} · ${resolveAnchorSummary(sourceAnchor)}`;
        }

        if (state.chromeState?.connectSourceNodeId) {
            const sourceNode = resolveNodeById(state, state.chromeState.connectSourceNodeId);
            return `Connect mode | choose an output point on ${sourceNode?.title || state.chromeState.connectSourceNodeId}`;
        }

        return "Connect mode | choose a source output point";
    }

    if (toolMode === toolModes.reconnect) {
        const edge = resolveReconnectEdge(state);
        const sourceAnchor = resolveReconnectSourceAnchor(state);
        if (edge && sourceAnchor) {
            const sourceNode = resolveNodeById(state, edge.sourceNodeId);
            return `Reconnect mode | choose a new target input for ${sourceNode?.title || edge.sourceNodeId} · ${resolveAnchorSummary(sourceAnchor)}`;
        }

        if (edge) {
            return "Reconnect mode | choose a new target input";
        }

        return "Reconnect mode | choose a connection first";
    }

    return "";
}
