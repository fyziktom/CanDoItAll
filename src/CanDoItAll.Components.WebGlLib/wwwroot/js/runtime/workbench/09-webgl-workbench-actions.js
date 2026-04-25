import {
    connectionActions,
    resolveHostPoint,
    resolveToolMode,
    toolModes
} from "./02-webgl-workbench-core.js";
import {
    applyConnectionSourceAnchor,
    buildConnectionRequest,
    clearConnectionDrafts,
    focusConnectionSourceNode,
    resolveConnectSourceAnchor,
    resolveSingleCompatibleTargetAnchor,
    resolveSingleOutputAnchor
} from "./11-webgl-workbench-anchor-flow.js";
import {
    resolveHitTarget
} from "./08-webgl-workbench-hit-testing.js";

export function applySelectedNodeIds(state, nodeIds) {
    const normalized = Array.isArray(nodeIds) ? nodeIds.filter(Boolean) : [];
    state.selectedNodeIds = new Set(normalized);
    if (state.surface?.uiState) {
        state.surface.uiState.selectedNodeIds = [...normalized];
    }

    if (state.sourceSurface?.uiState) {
        state.sourceSurface.uiState.selectedNodeIds = [...normalized];
    }
}

export function updateNodeSelection(state, nodeId, deps) {
    state.chromeState.selectedEdgeId = null;
    applySelectedNodeIds(state, nodeId ? [nodeId] : []);
    deps.scheduleRender(state);
    state.dotNetRef?.invokeMethodAsync(
        "OnSelectionChanged",
        nodeId || null,
        JSON.stringify(Array.from(state.selectedNodeIds)));
}

function updateEdgeSelection(state, edgeId, deps) {
    state.chromeState.selectedEdgeId = edgeId || null;
    applySelectedNodeIds(state, []);
    deps.scheduleRender(state);
    state.dotNetRef?.invokeMethodAsync("OnSelectionChanged", null, JSON.stringify([]));
}

function dispatchConnectionChangeRequested(state, request) {
    if (!request?.sourceNodeId || !request?.targetNodeId) {
        return false;
    }

    state.diagnostics.connectionCommitCount += 1;
    state.dotNetRef?.invokeMethodAsync("OnConnectionChangeRequested", JSON.stringify(request));
    return true;
}

function dispatchDeleteRequested(state, request) {
    if (!request?.nodeId && !request?.edgeId) {
        return false;
    }

    state.dotNetRef?.invokeMethodAsync("OnDeleteRequested", JSON.stringify(request));
    return true;
}

export function closeContextMenu(state, deps, shouldRender = true) {
    if (!state.chromeState?.contextMenu) {
        return;
    }

    state.chromeState.contextMenu = null;
    if (shouldRender) {
        deps.scheduleRender(state);
    }
}

function setToolMode(state, mode, deps) {
    const normalizedMode = Object.values(toolModes).includes(mode)
        ? mode
        : toolModes.select;
    if (state.surface?.uiState) {
        state.surface.uiState.toolMode = normalizedMode;
    }

    if (state.sourceSurface?.uiState) {
        state.sourceSurface.uiState.toolMode = normalizedMode;
    }

    state.chromeState.selectedEdgeId = null;
    clearConnectionDrafts(state);
    closeContextMenu(state, deps, false);
    deps.notifyStateChanged(state);
    deps.scheduleRender(state);
}

function setNodeInfoMode(state, mode, deps) {
    if (state.surface?.uiState) {
        state.surface.uiState.nodeInfoMode = mode;
    }

    if (state.sourceSurface?.uiState) {
        state.sourceSurface.uiState.nodeInfoMode = mode;
    }

    deps.notifyStateChanged(state);
    deps.scheduleRender(state);
}

function toggleUiFlag(state, key, deps) {
    const nextValue = !(state.surface?.uiState?.[key]);
    if (state.surface?.uiState) {
        state.surface.uiState[key] = nextValue;
    }

    if (state.sourceSurface?.uiState) {
        state.sourceSurface.uiState[key] = nextValue;
    }

    deps.notifyStateChanged(state);
    deps.scheduleRender(state);
}

function toggleLocalFilter(state, key, deps) {
    state.chromeState[key] = state.chromeState[key] !== false
        ? false
        : true;
    deps.syncRuntimeState(state, state.sourceSurface);
    deps.scheduleRender(state);
}

function executeConnectionBetweenAnchors(state, sourceAnchorId, targetAnchorId, deps) {
    const request = buildConnectionRequest(state, sourceAnchorId, targetAnchorId, connectionActions.connect);
    if (!request) {
        return false;
    }

    const connected = dispatchConnectionChangeRequested(state, request);
    if (connected) {
        updateNodeSelection(state, request.sourceNodeId, deps);
    }

    return connected;
}

function executeConnectionToNode(state, targetNodeId, deps) {
    const sourceAnchor = resolveConnectSourceAnchor(state);
    if (!sourceAnchor) {
        return false;
    }

    const targetAnchor = resolveSingleCompatibleTargetAnchor(state, sourceAnchor.id, targetNodeId);
    if (!targetAnchor) {
        return false;
    }

    return executeConnectionBetweenAnchors(state, sourceAnchor.id, targetAnchor.id, deps);
}

function executeReconnectToAnchor(state, edgeId, targetAnchorId, deps) {
    const request = buildConnectionRequest(state, null, targetAnchorId, connectionActions.reconnectTarget, edgeId);
    if (!request) {
        return false;
    }

    const reconnected = dispatchConnectionChangeRequested(state, request);
    if (reconnected) {
        state.chromeState.reconnectEdgeId = null;
        state.chromeState.selectedEdgeId = request.edgeId;
        deps.scheduleRender(state);
    }

    return reconnected;
}

function executeReconnectToNode(state, edgeId, targetNodeId, deps) {
    const edge = (state.surface.edges || []).find(candidate => candidate.id === edgeId);
    if (!edge) {
        return false;
    }

    const targetAnchor = resolveSingleCompatibleTargetAnchor(state, edge.sourceAnchorId, targetNodeId);
    return targetAnchor
        ? executeReconnectToAnchor(state, edgeId, targetAnchor.id, deps)
        : false;
}

function disconnectEdgeById(state, edgeId) {
    const edge = (state.surface.edges || []).find(candidate => candidate.id === edgeId);
    if (!edge) {
        return false;
    }

    return dispatchConnectionChangeRequested(state, {
        actionId: connectionActions.disconnect,
        edgeId: edge.id,
        sourceNodeId: edge.sourceNodeId,
        sourceAnchorId: edge.sourceAnchorId,
        sourcePortId: edge.sourcePortId || null,
        targetNodeId: edge.targetNodeId,
        targetAnchorId: edge.targetAnchorId,
        targetPortId: edge.targetPortId || null,
        kind: edge.kind || "",
        categoryKey: edge.categoryKey || ""
    });
}

function deleteNodeById(state, nodeId) {
    const node = state.nodeLookup.get(nodeId);
    if (!node || node.isReadOnly) {
        return false;
    }

    return dispatchDeleteRequested(state, {
        nodeId,
        edgeId: null
    });
}

function resolveSelectedNodeId(hitTarget) {
    if (hitTarget?.type === "anchor") {
        return hitTarget.nodeId || null;
    }

    if (hitTarget?.type === "node") {
        return hitTarget.nodeId || null;
    }

    return null;
}

function resolveAnchorMenuTitle(anchor) {
    return anchor?.label || anchor?.portId || anchor?.id || "Connection point";
}

function resolveAnchorMenuSubtitle(anchor, node) {
    const role = anchor?.role === "output"
        ? "Output"
        : "Input";
    const nodeTitle = node?.title || anchor?.nodeId || "Node";
    return `${role} · ${nodeTitle}`;
}

function openContextMenu(state, event, deps) {
    const hitTarget = resolveHitTarget(state, event);
    const hostPoint = resolveHostPoint(state.host, event.clientX, event.clientY);
    if (!hostPoint) {
        return;
    }

    const items = [];
    let title = "Scene actions";
    let subtitle = "WebGL context menu";
    let nodeId = null;
    let edgeId = null;
    let anchorId = null;

    if (hitTarget?.type === "anchor") {
        nodeId = hitTarget.nodeId || null;
        anchorId = hitTarget.anchorId || null;
        const node = nodeId ? state.nodeLookup.get(nodeId) : null;
        const anchor = anchorId ? state.anchorLookup.get(anchorId) : null;
        title = resolveAnchorMenuTitle(anchor);
        subtitle = resolveAnchorMenuSubtitle(anchor, node);
        items.push({ id: "menu:select-node", label: "Select node", tone: "accent" });
        if (anchor?.role === "output") {
            items.push({ id: "menu:connect-from-anchor", label: "Connect from point", tone: "positive" });
        }

        if (anchor?.role === "input" && state.chromeState.connectSourceAnchorId) {
            items.push({ id: "menu:connect-to-anchor", label: "Connect here", tone: "positive" });
        }

        if (anchor?.role === "input" && state.chromeState.reconnectEdgeId) {
            items.push({ id: "menu:reconnect-to-anchor", label: "Reconnect here", tone: "warning" });
        }

        items.push({ id: "menu:focus-node", label: "Focus node", tone: "neutral" });
    } else if (hitTarget?.type === "node") {
        nodeId = hitTarget.nodeId || null;
        const node = nodeId ? state.nodeLookup.get(nodeId) : null;
        title = node?.title || "Node actions";
        subtitle = node?.kind || "Process node";
        items.push({ id: "menu:select-node", label: "Select node", tone: "accent" });
        if (state.chromeState.connectSourceAnchorId && state.chromeState.connectSourceNodeId !== nodeId) {
            items.push({ id: "menu:connect-to-node", label: "Connect here", tone: "positive" });
        } else {
            items.push({ id: "menu:connect-from-node", label: "Connect from node", tone: "positive" });
        }

        if (state.chromeState.reconnectEdgeId) {
            items.push({ id: "menu:reconnect-to-node", label: "Reconnect here", tone: "warning" });
        }

        items.push({ id: "menu:focus-node", label: "Focus node", tone: "neutral" });
        if (!node?.isReadOnly) {
            items.push({ id: "menu:delete-node", label: "Delete node", tone: "danger" });
        }
    } else if (hitTarget?.type === "edge") {
        edgeId = hitTarget.edgeId || null;
        const edge = edgeId
            ? (state.surface.edges || []).find(candidate => candidate.id === edgeId)
            : null;
        title = edge?.label || edge?.kind || "Connection actions";
        subtitle = edge?.categoryKey || "Connection";
        items.push({ id: "menu:delete-edge", label: "Disconnect", tone: "danger" });
        items.push({ id: "menu:reconnect-edge", label: "Reconnect target", tone: "warning" });
    } else {
        items.push({ id: "menu:fit", label: "Fit view", tone: "accent" });
        items.push({ id: "menu:reset", label: "Reset camera", tone: "neutral" });
        items.push({ id: "menu:settings", label: "Open settings", tone: "neutral" });
    }

    state.chromeState.contextMenu = {
        title,
        subtitle,
        x: hostPoint.x,
        y: hostPoint.y,
        nodeId,
        edgeId,
        anchorId,
        items
    };
    deps.scheduleRender(state);
}

export function applyChromeAction(state, actionId, deps) {
    if (actionId?.startsWith?.("host:")) {
        closeContextMenu(state, deps, false);
        deps.requestChromeAction?.(actionId);
        return true;
    }

    switch (actionId) {
        case "tool:select":
            setToolMode(state, toolModes.select, deps);
            return true;
        case "tool:delete":
            setToolMode(state, toolModes.delete, deps);
            return true;
        case "tool:connect":
            setToolMode(state, toolModes.connect, deps);
            return true;
        case "tool:reconnect":
            setToolMode(state, toolModes.reconnect, deps);
            return true;
        case "view:fit":
        case "menu:fit":
            closeContextMenu(state, deps, false);
            deps.fitView(state);
            return true;
        case "view:reset":
        case "menu:reset":
            closeContextMenu(state, deps, false);
            deps.resetView(state);
            return true;
        case "camera:perspective":
            closeContextMenu(state, deps, false);
            deps.setCameraViewMode?.(state, "perspective", true);
            return true;
        case "camera:xy":
            closeContextMenu(state, deps, false);
            deps.setCameraViewMode?.(state, "xy", true);
            return true;
        case "camera:xz":
            closeContextMenu(state, deps, false);
            deps.setCameraViewMode?.(state, "xz", true);
            return true;
        case "camera:yz":
            closeContextMenu(state, deps, false);
            deps.setCameraViewMode?.(state, "yz", true);
            return true;
        case "chrome:toggle-stage-size":
            closeContextMenu(state, deps, false);
            toggleUiFlag(state, "isStageMaximized", deps);
            return true;
        case "chrome:settings":
        case "menu:settings":
            state.chromeState.settingsOpen = !state.chromeState.settingsOpen;
            closeContextMenu(state, deps, false);
            deps.scheduleRender(state);
            return true;
        case "info:detailed":
            setNodeInfoMode(state, "detailed", deps);
            return true;
        case "info:miniature":
            setNodeInfoMode(state, "miniature", deps);
            return true;
        case "info:hidden":
            setNodeInfoMode(state, "hidden", deps);
            return true;
        case "toggle:grid":
            toggleUiFlag(state, "showGrid", deps);
            return true;
        case "toggle:transparent-ground":
            toggleUiFlag(state, "transparentGround", deps);
            return true;
        case "toggle:anchors":
            toggleUiFlag(state, "showAnchors", deps);
            return true;
        case "toggle:edge-labels":
            toggleUiFlag(state, "showEdgeLabels", deps);
            return true;
        case "toggle:diagnostics":
            toggleUiFlag(state, "showDiagnostics", deps);
            return true;
        case "toggle:roles":
            toggleLocalFilter(state, "showRoleNodes", deps);
            return true;
        case "toggle:branches":
            toggleLocalFilter(state, "showBranchNodes", deps);
            return true;
        case "menu:select-node": {
            const nodeId = state.chromeState?.contextMenu?.nodeId || null;
            closeContextMenu(state, deps, false);
            updateNodeSelection(state, nodeId, deps);
            return true;
        }
        case "menu:focus-node": {
            const nodeId = state.chromeState?.contextMenu?.nodeId || null;
            closeContextMenu(state, deps, false);
            if (nodeId) {
                deps.focusNode(state, nodeId);
            }
            return true;
        }
        case "menu:connect-from-node": {
            const nodeId = state.chromeState?.contextMenu?.nodeId || null;
            setToolMode(state, toolModes.connect, deps);
            closeContextMenu(state, deps, false);
            if (nodeId) {
                const singleOutputAnchor = resolveSingleOutputAnchor(state, nodeId);
                if (singleOutputAnchor) {
                    applyConnectionSourceAnchor(state, singleOutputAnchor.id);
                } else {
                    focusConnectionSourceNode(state, nodeId);
                }
                updateNodeSelection(state, nodeId, deps);
            }
            deps.scheduleRender(state);
            return true;
        }
        case "menu:connect-from-anchor": {
            const anchorId = state.chromeState?.contextMenu?.anchorId || null;
            const anchor = anchorId ? state.anchorLookup.get(anchorId) : null;
            setToolMode(state, toolModes.connect, deps);
            closeContextMenu(state, deps, false);
            if (anchor && applyConnectionSourceAnchor(state, anchor.id)) {
                updateNodeSelection(state, anchor.nodeId, deps);
            }
            deps.scheduleRender(state);
            return true;
        }
        case "menu:connect-to-node": {
            const targetNodeId = state.chromeState?.contextMenu?.nodeId || null;
            const sourceAnchor = resolveConnectSourceAnchor(state);
            closeContextMenu(state, deps, false);
            if (sourceAnchor && targetNodeId) {
                if (executeConnectionToNode(state, targetNodeId, deps)) {
                    return true;
                }

                updateNodeSelection(state, targetNodeId, deps);
                deps.scheduleRender(state);
                return true;
            }
            return true;
        }
        case "menu:connect-to-anchor": {
            const targetAnchorId = state.chromeState?.contextMenu?.anchorId || null;
            const sourceAnchor = resolveConnectSourceAnchor(state);
            closeContextMenu(state, deps, false);
            if (sourceAnchor && targetAnchorId) {
                executeConnectionBetweenAnchors(state, sourceAnchor.id, targetAnchorId, deps);
            }
            return true;
        }
        case "menu:reconnect-edge": {
            const edgeId = state.chromeState?.contextMenu?.edgeId || null;
            if (edgeId) {
                setToolMode(state, toolModes.reconnect, deps);
                state.chromeState.reconnectEdgeId = edgeId;
                state.chromeState.selectedEdgeId = edgeId;
            }

            closeContextMenu(state, deps, false);
            deps.scheduleRender(state);
            return true;
        }
        case "menu:reconnect-to-node": {
            const targetNodeId = state.chromeState?.contextMenu?.nodeId || null;
            const edgeId = state.chromeState?.reconnectEdgeId || null;
            closeContextMenu(state, deps, false);
            if (edgeId && targetNodeId) {
                if (executeReconnectToNode(state, edgeId, targetNodeId, deps)) {
                    return true;
                }

                updateNodeSelection(state, targetNodeId, deps);
                deps.scheduleRender(state);
                return true;
            }
            return true;
        }
        case "menu:reconnect-to-anchor": {
            const targetAnchorId = state.chromeState?.contextMenu?.anchorId || null;
            const edgeId = state.chromeState?.reconnectEdgeId || null;
            closeContextMenu(state, deps, false);
            if (edgeId && targetAnchorId) {
                executeReconnectToAnchor(state, edgeId, targetAnchorId, deps);
            }
            return true;
        }
        case "menu:delete-edge": {
            const edgeId = state.chromeState?.contextMenu?.edgeId || null;
            closeContextMenu(state, deps, false);
            if (edgeId) {
                disconnectEdgeById(state, edgeId);
            }
            return true;
        }
        case "menu:delete-node": {
            const nodeId = state.chromeState?.contextMenu?.nodeId || null;
            closeContextMenu(state, deps, false);
            if (nodeId) {
                deleteNodeById(state, nodeId);
            }
            return true;
        }
        default:
            return false;
    }
}

function handleToolClick(state, event, deps) {
    const toolMode = resolveToolMode(state.surface);
    const hitTarget = resolveHitTarget(state, event);
    const selectedNodeId = resolveSelectedNodeId(hitTarget);

    switch (toolMode) {
        case toolModes.delete:
            if (hitTarget?.type === "edge") {
                disconnectEdgeById(state, hitTarget.edgeId);
                return true;
            }

            if (selectedNodeId) {
                deleteNodeById(state, selectedNodeId);
                return true;
            }
            return false;
        case toolModes.connect: {
            if (hitTarget?.type === "anchor") {
                const anchor = state.anchorLookup.get(hitTarget.anchorId || "");
                if (!anchor) {
                    return false;
                }

                if (anchor.role === "output") {
                    applyConnectionSourceAnchor(state, anchor.id);
                    updateNodeSelection(state, anchor.nodeId, deps);
                    deps.scheduleRender(state);
                    return true;
                }

                const sourceAnchor = resolveConnectSourceAnchor(state);
                if (sourceAnchor && executeConnectionBetweenAnchors(state, sourceAnchor.id, anchor.id, deps)) {
                    return true;
                }

                updateNodeSelection(state, anchor.nodeId, deps);
                deps.scheduleRender(state);
                return true;
            }

            if (hitTarget?.type !== "node") {
                state.chromeState.connectSourceNodeId = null;
                state.chromeState.connectSourceAnchorId = null;
                deps.scheduleRender(state);
                return false;
            }

            const sourceAnchor = resolveConnectSourceAnchor(state);
            if (!sourceAnchor) {
                const singleOutputAnchor = resolveSingleOutputAnchor(state, hitTarget.nodeId);
                if (singleOutputAnchor) {
                    applyConnectionSourceAnchor(state, singleOutputAnchor.id);
                } else {
                    focusConnectionSourceNode(state, hitTarget.nodeId);
                }
                updateNodeSelection(state, hitTarget.nodeId, deps);
                deps.scheduleRender(state);
                return true;
            }

            if (sourceAnchor.nodeId === hitTarget.nodeId) {
                updateNodeSelection(state, hitTarget.nodeId, deps);
                deps.scheduleRender(state);
                return true;
            }

            if (executeConnectionToNode(state, hitTarget.nodeId, deps)) {
                return true;
            }

            updateNodeSelection(state, hitTarget.nodeId, deps);
            deps.scheduleRender(state);
            return true;
        }
        case toolModes.reconnect:
            if (hitTarget?.type === "edge") {
                state.chromeState.reconnectEdgeId = hitTarget.edgeId;
                state.chromeState.selectedEdgeId = hitTarget.edgeId;
                deps.scheduleRender(state);
                return true;
            }

            if (hitTarget?.type === "anchor" && state.chromeState.reconnectEdgeId) {
                const anchor = state.anchorLookup.get(hitTarget.anchorId || "");
                if (anchor?.role === "input" && executeReconnectToAnchor(state, state.chromeState.reconnectEdgeId, anchor.id, deps)) {
                    return true;
                }

                return false;
            }

            if (hitTarget?.type === "node" && state.chromeState.reconnectEdgeId) {
                if (executeReconnectToNode(state, state.chromeState.reconnectEdgeId, hitTarget.nodeId, deps)) {
                    return true;
                }

                updateNodeSelection(state, hitTarget.nodeId, deps);
                deps.scheduleRender(state);
                return true;
            }

            return false;
        default:
            if (hitTarget?.type === "edge") {
                updateEdgeSelection(state, hitTarget.edgeId, deps);
                return true;
            }

            if (selectedNodeId) {
                updateNodeSelection(state, selectedNodeId, deps);
                return true;
            }

            updateNodeSelection(state, null, deps);
            return false;
    }
}

export function handleClick(state, event, deps) {
    if (state.suppressClick) {
        state.suppressClick = false;
        return;
    }

    const chromeHit = state.chromeController?.hitTest(event.clientX, event.clientY);
    if (chromeHit) {
        applyChromeAction(state, chromeHit.id, deps);
        return;
    }

    handleToolClick(state, event, deps);
}

export function handleContextMenu(state, event, deps) {
    event.preventDefault?.();
    event.stopPropagation?.();

    const chromeHit = state.chromeController?.hitTest(event.clientX, event.clientY);
    if (chromeHit) {
        return;
    }

    openContextMenu(state, event, deps);
}

export function simulateConnection(state, request) {
    const normalized = {
        actionId: request?.actionId === connectionActions.disconnect
            ? connectionActions.disconnect
            : request?.actionId === connectionActions.reconnectTarget
                ? connectionActions.reconnectTarget
                : connectionActions.connect,
        edgeId: request?.edgeId || null,
        sourceNodeId: request?.sourceNodeId || "",
        sourceAnchorId: request?.sourceAnchorId || "",
        sourcePortId: request?.sourcePortId || null,
        targetNodeId: request?.targetNodeId || "",
        targetAnchorId: request?.targetAnchorId || "",
        targetPortId: request?.targetPortId || null,
        kind: request?.kind || "",
        categoryKey: request?.categoryKey || ""
    };
    if (!normalized.sourceNodeId || !normalized.targetNodeId) {
        return false;
    }

    return dispatchConnectionChangeRequested(state, normalized);
}

export function getAnchorCenter(state, request) {
    if (request?.edgeId) {
        const edge = state.projectedEdges.get(request.edgeId);
        return edge
            ? {
                x: edge.x,
                y: edge.y
            }
            : null;
    }

    const anchorId = request?.anchorId || "";
    if (anchorId && state.projectedAnchors.has(anchorId)) {
        const anchor = state.projectedAnchors.get(anchorId);
        return {
            x: anchor.x,
            y: anchor.y
        };
    }

    if (request?.nodeId) {
        const node = state.projectedNodes.get(request.nodeId);
        if (!node) {
            return null;
        }

        return {
            x: node.left + (node.width / 2),
            y: node.top + (node.height / 2)
        };
    }

    return null;
}
