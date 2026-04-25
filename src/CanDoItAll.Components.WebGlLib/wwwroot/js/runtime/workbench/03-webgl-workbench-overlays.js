import {
    clamp,
    isBranchNode,
    isRoleNode,
    projectPoint,
    resolveAnchorPosition,
    resolveNodeInfoMode,
    resolveNodeScreenBounds,
    resolveToolMode,
    round
} from "./02-webgl-workbench-core.js";
import {
    resolveCompatibleTargetAnchorIds,
    resolveConnectSourceAnchor,
    resolvePendingSourceOutputAnchorIds,
    resolveReconnectEdge
} from "./11-webgl-workbench-anchor-flow.js";

function ensureNodeLabel(state, node) {
    let label = state.labelElements.get(node.id);
    if (label) {
        return label;
    }

    label = document.createElement("div");
    label.className = "wgl-node-label";
    label.setAttribute("data-webgl-node-id", node.id);
    label.setAttribute("aria-label", `${node.title || node.id} node`);

    const kicker = document.createElement("p");
    kicker.className = "wgl-node-label__kicker";
    const title = document.createElement("h3");
    title.className = "wgl-node-label__title";
    const subtitle = document.createElement("p");
    subtitle.className = "wgl-node-label__subtitle";
    const tags = document.createElement("div");
    tags.className = "wgl-node-label__tags";

    label.append(kicker, title, subtitle, tags);
    state.shell.labelLayer.appendChild(label);
    state.labelElements.set(node.id, label);

    return label;
}

function syncNodeLabelContent(label, node) {
    const tagsKey = (node.tags || []).join("|");
    const contentKey = JSON.stringify({
        kind: node.kind || node.family || "Node",
        title: node.title || node.id,
        subtitle: node.subtitle || node.description || "",
        tagsKey
    });
    if (label.dataset.contentKey === contentKey) {
        return;
    }

    label.dataset.contentKey = contentKey;
    label.querySelector(".wgl-node-label__kicker").textContent = node.kind || node.family || "Node";
    label.querySelector(".wgl-node-label__title").textContent = node.title || node.id;
    label.querySelector(".wgl-node-label__subtitle").textContent = node.subtitle || node.description || "";

    const tags = label.querySelector(".wgl-node-label__tags");
    tags.replaceChildren(...(node.tags || []).map(tag => {
        const tagElement = document.createElement("span");
        tagElement.className = "wgl-node-label__tag";
        tagElement.textContent = tag;
        return tagElement;
    }));
}

function ensurePortElement(state, anchor) {
    let element = state.anchorElements.get(anchor.id);
    if (element) {
        return element;
    }

    element = document.createElement("div");
    element.className = "wgl-port-anchor";
    element.setAttribute("data-webgl-port-id", anchor.id);
    element.setAttribute("data-webgl-node-id", anchor.nodeId || "");
    element.setAttribute("data-webgl-anchor-role", anchor.role || "");
    element.setAttribute("data-webgl-anchor-side", anchor.side || "");
    element.setAttribute("aria-label", `${anchor.label || anchor.portId || anchor.id} anchor`);
    state.shell.mirrorLayer.appendChild(element);
    state.anchorElements.set(anchor.id, element);

    return element;
}

function ensurePortLabelElement(state, anchor) {
    let element = state.anchorLabelElements.get(anchor.id);
    if (element) {
        return element;
    }

    element = document.createElement("div");
    element.className = "wgl-port-anchor-label";
    element.setAttribute("data-webgl-port-label-for", anchor.id);
    element.setAttribute("data-webgl-node-id", anchor.nodeId || "");
    element.setAttribute("data-webgl-anchor-side", anchor.side || "");
    state.shell.mirrorLayer.appendChild(element);
    state.anchorLabelElements.set(anchor.id, element);

    return element;
}

function syncPortLabelContent(element, anchor) {
    const labelText = anchor.label || anchor.portId || anchor.id;
    if (element.dataset.labelText === labelText) {
        return;
    }

    element.dataset.labelText = labelText;
    element.textContent = labelText;
}

function ensureEdgeElement(state, edge) {
    let element = state.edgeElements.get(edge.id);
    if (element) {
        return element;
    }

    element = document.createElement("div");
    element.className = "wgl-edge-anchor";
    element.setAttribute("data-webgl-edge-id", edge.id);
    element.setAttribute("aria-label", `${edge.label || edge.kind || "connection"} edge`);
    state.shell.mirrorLayer.appendChild(element);
    state.edgeElements.set(edge.id, element);

    return element;
}

function resolveNodeLabelScale(node, bounds, nodeInfoMode) {
    const targetWidth = isRoleNode(node)
        ? 158
        : isBranchNode(node)
            ? 164
            : 188;
    const targetHeight = isRoleNode(node)
        ? 88
        : 96;
    const widthScale = Math.max(0.01, bounds.width) / targetWidth;
    const heightScale = Math.max(0.01, bounds.height) / targetHeight;
    const fittedWidthScale = widthScale * 1.06;
    const fittedHeightScale = heightScale * 1.95;
    const resolvedScale = clamp(Math.min(fittedWidthScale, fittedHeightScale), 0.46, 1);

    return nodeInfoMode === "miniature"
        ? round(Math.min(resolvedScale, 0.72))
        : round(resolvedScale);
}

function syncNodeLabels(state) {
    state.projectedNodes.clear();
    const activeNodeIds = new Set();
    const nodeInfoMode = resolveNodeInfoMode(state.surface);

    for (const node of state.surface.nodes || []) {
        activeNodeIds.add(node.id);
        const label = ensureNodeLabel(state, node);
        const bounds = resolveNodeScreenBounds(state, node);
        const labelScale = resolveNodeLabelScale(node, bounds, nodeInfoMode);
        state.projectedNodes.set(node.id, {
            left: round(bounds.left),
            top: round(bounds.top),
            width: round(bounds.width),
            height: round(bounds.height)
        });

        label.style.left = `${round(bounds.centerX)}px`;
        label.style.top = `${round(bounds.centerY)}px`;
        label.style.display = nodeInfoMode === "hidden" ? "none" : "block";
        label.style.setProperty("--wgl-label-scale", `${labelScale}`);
        label.classList.toggle("is-selected", state.selectedNodeIds.has(node.id));
        label.classList.toggle("is-condensed", labelScale < 0.74);
        label.classList.toggle("is-compact", labelScale < 0.56);
        label.classList.toggle("is-miniature", nodeInfoMode === "miniature");
        syncNodeLabelContent(label, node);
    }

    for (const [nodeId, element] of state.labelElements.entries()) {
        if (!activeNodeIds.has(nodeId)) {
            element.remove();
            state.labelElements.delete(nodeId);
        }
    }
}

function syncAnchors(state) {
    state.projectedAnchors.clear();
    const activeAnchorIds = new Set();
    const showAnchors = state.surface.uiState?.showAnchors !== false;
    const toolMode = resolveToolMode(state.surface);
    const nodeInfoMode = resolveNodeInfoMode(state.surface);
    const connectSourceAnchor = resolveConnectSourceAnchor(state);
    const reconnectEdge = resolveReconnectEdge(state);
    const authoringSourceAnchorId = connectSourceAnchor?.id || reconnectEdge?.sourceAnchorId || "";
    const compatibleTargetAnchorIds = authoringSourceAnchorId
        ? resolveCompatibleTargetAnchorIds(state, authoringSourceAnchorId)
        : new Set();
    const pendingSourceAnchorIds = resolvePendingSourceOutputAnchorIds(state);
    const authoringAnchorsVisible = showAnchors || toolMode === "connect" || toolMode === "reconnect";

    for (const node of state.surface.nodes || []) {
        const nodeBounds = resolveNodeScreenBounds(state, node);
        const isSelectedNode = state.selectedNodeIds.has(node.id) || state.chromeState?.connectSourceNodeId === node.id;
        const zoom = Number(state.cameraState?.zoom) || 1;
        for (const anchor of node.anchors || []) {
            activeAnchorIds.add(anchor.id);
            const position = resolveAnchorPosition(node, anchor);
            const projected = projectPoint(state, position);
            const element = ensurePortElement(state, anchor);
            const labelElement = ensurePortLabelElement(state, anchor);
            const isSourceActive = connectSourceAnchor?.id === anchor.id;
            const isReconnectTarget = reconnectEdge?.targetAnchorId === anchor.id;
            const isPendingSource = pendingSourceAnchorIds.has(anchor.id);
            const isCompatible = compatibleTargetAnchorIds.has(anchor.id);
            const largeNode = nodeBounds.width >= 170 || nodeBounds.height >= 84 || (zoom >= 1.18 && nodeBounds.width >= 138);
            const labelVisible = authoringAnchorsVisible && (
                isSourceActive ||
                isReconnectTarget ||
                isPendingSource ||
                isCompatible ||
                (nodeInfoMode !== "hidden" && largeNode && (nodeInfoMode === "detailed" || isSelectedNode || zoom >= 1.32))
            );
            element.style.left = `${round(projected.x)}px`;
            element.style.top = `${round(projected.y)}px`;
            element.style.display = authoringAnchorsVisible ? "block" : "none";
            const accentColor = anchor.accentColor || "#2563eb";
            if (element.dataset.accentColor !== accentColor) {
                element.dataset.accentColor = accentColor;
                element.style.backgroundColor = accentColor;
            }
            element.classList.toggle("is-source", isSourceActive);
            element.classList.toggle("is-compatible", isCompatible);
            element.classList.toggle("is-current-target", isReconnectTarget);
            element.classList.toggle("is-pending-source", isPendingSource);
            element.classList.toggle("is-required", !!anchor.isRequired);

            syncPortLabelContent(labelElement, anchor);
            labelElement.style.left = `${round(projected.x)}px`;
            labelElement.style.top = `${round(projected.y)}px`;
            labelElement.style.display = labelVisible ? "block" : "none";
            labelElement.classList.toggle("is-source", isSourceActive);
            labelElement.classList.toggle("is-compatible", isCompatible);
            labelElement.classList.toggle("is-current-target", isReconnectTarget);
            labelElement.classList.toggle("is-pending-source", isPendingSource);
            labelElement.classList.toggle("is-required", !!anchor.isRequired);
            labelElement.classList.toggle("is-compact", nodeBounds.width < 152 || nodeBounds.height < 76);
            labelElement.setAttribute("data-webgl-anchor-side", anchor.side || "");
            state.projectedAnchors.set(anchor.id, {
                nodeId: node.id,
                portId: anchor.portId,
                label: anchor.label,
                role: anchor.role,
                side: anchor.side,
                categoryKey: anchor.categoryKey,
                isRequired: !!anchor.isRequired,
                isVisible: authoringAnchorsVisible,
                labelVisible,
                isActive: isSourceActive || isReconnectTarget,
                isCompatible,
                x: round(projected.x),
                y: round(projected.y)
            });
        }
    }

    for (const [anchorId, element] of state.anchorElements.entries()) {
        if (!activeAnchorIds.has(anchorId)) {
            element.remove();
            state.anchorElements.delete(anchorId);
        }
    }

    for (const [anchorId, element] of state.anchorLabelElements.entries()) {
        if (!activeAnchorIds.has(anchorId)) {
            element.remove();
            state.anchorLabelElements.delete(anchorId);
        }
    }
}

function syncEdges(state) {
    state.projectedEdges.clear();
    const activeEdgeIds = new Set();
    const showEdgeLabels = state.surface.uiState?.showEdgeLabels !== false;

    for (const edge of state.surface.edges || []) {
        activeEdgeIds.add(edge.id);
        const sourceAnchor = state.projectedAnchors.get(edge.sourceAnchorId);
        const targetAnchor = state.projectedAnchors.get(edge.targetAnchorId);
        if (!sourceAnchor || !targetAnchor) {
            continue;
        }

        const element = ensureEdgeElement(state, edge);
        const x = (sourceAnchor.x + targetAnchor.x) / 2;
        const y = (sourceAnchor.y + targetAnchor.y) / 2;
        element.style.left = `${round(x)}px`;
        element.style.top = `${round(y)}px`;
        element.style.display = showEdgeLabels ? "block" : "none";
        element.style.opacity = `${edge.opacity ?? 0.82}`;
        element.classList.toggle("is-primary", !!edge.isPrimaryPath);
        element.classList.toggle("is-selected", state.chromeState?.selectedEdgeId === edge.id);
        const label = edge.label || "";
        if (element.textContent !== label) {
            element.textContent = label;
        }
        state.projectedEdges.set(edge.id, {
            x: round(x),
            y: round(y),
            sourceNodeId: edge.sourceNodeId,
            sourceAnchorId: edge.sourceAnchorId,
            sourcePortId: edge.sourcePortId,
            targetNodeId: edge.targetNodeId,
            targetAnchorId: edge.targetAnchorId,
            targetPortId: edge.targetPortId,
            kind: edge.kind,
            categoryKey: edge.categoryKey,
            isPrimaryPath: !!edge.isPrimaryPath,
            emphasis: round(edge.emphasis ?? 1),
            opacity: round(edge.opacity ?? 0.82)
        });
    }

    for (const [edgeId, element] of state.edgeElements.entries()) {
        if (!activeEdgeIds.has(edgeId)) {
            element.remove();
            state.edgeElements.delete(edgeId);
        }
    }
}

function syncEmptyState(state) {
    const hasNodes = (state.surface.nodes?.length || 0) > 0;
    state.shell.emptyState.classList.toggle("is-visible", !hasNodes);
    state.shell.emptyTitle.textContent = state.surface.chrome?.emptyStateTitle || "No process geometry";
    state.shell.emptyBody.textContent = state.surface.chrome?.emptyStateDescription || "";
}

function syncDiagnostics(state) {
    const showDiagnostics = !!state.surface.uiState?.showDiagnostics;
    const chromeHasPriority = !!state.chromeState?.settingsOpen || !!state.chromeState?.contextMenu;
    state.shell.diagnosticsPanel.style.display = showDiagnostics && !chromeHasPriority
        ? "flex"
        : "none";
    state.shell.diagnosticsMeta.textContent =
        `${state.diagnostics.nodeCount} nodes, ${state.diagnostics.edgeCount} edges, ` +
        `${state.diagnostics.renderCount} renders, ${state.cameraState.viewMode}/${state.cameraState.projectionMode}`;
}

export function syncDomOverlays(state) {
    syncNodeLabels(state);
    syncAnchors(state);
    syncEdges(state);
    syncEmptyState(state);
    syncDiagnostics(state);
}
