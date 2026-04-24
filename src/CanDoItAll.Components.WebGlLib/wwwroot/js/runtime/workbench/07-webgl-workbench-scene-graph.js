import {
    THREE,
    clamp,
    isBranchNode,
    isProcessStepNode,
    isRoleNode,
    isVisualGuideNode,
    resolveAnchorPosition,
    resolveFiniteNumber,
    toSceneY
} from "./02-webgl-workbench-core.js";
import { GLTFLoader } from "../../../vendor/GLTFLoader.js";
import { clone as cloneSkeleton } from "../../../vendor/utils/SkeletonUtils.js";

const modelLoader = new GLTFLoader();
const nodeModelConfigs = Object.freeze({
    role: {
        assetUrl: new URL("../../../assets/model/lowpoly_person_boxing.glb", import.meta.url).href,
        fitWidthFactor: 0.44,
        fitHeightFactor: 0.9,
        fitDepthFactor: 0.4
    },
    branch: {
        assetUrl: new URL("../../../assets/model/question_box.glb", import.meta.url).href,
        fitWidthFactor: 0.4,
        fitHeightFactor: 0.72,
        fitDepthFactor: 0.42
    },
    step: {
        assetUrl: new URL("../../../assets/model/gears.glb", import.meta.url).href,
        fitWidthFactor: 0.42,
        fitHeightFactor: 0.74,
        fitDepthFactor: 0.42
    }
});
const modelAssetPromises = new Map();

function destroyObject3D(object) {
    if (!object) {
        return;
    }

    object.traverse(child => {
        if (child.userData?.skipDispose) {
            return;
        }

        child.geometry?.dispose?.();
        if (Array.isArray(child.material)) {
            for (const material of child.material) {
                material?.map?.dispose?.();
                material?.dispose?.();
            }
            return;
        }

        child.material?.map?.dispose?.();
        child.material?.dispose?.();
    });
}

function resolveNodeColors(node) {
    return {
        fill: node.fillColor || "#ffffff",
        border: node.borderColor || "#cbd5e1",
        accent: node.accentColor || "#2563eb"
    };
}

function resolveEdgeDepth(edge) {
    const explicitDepth = Number(edge.depthOffset);
    if (Number.isFinite(explicitDepth) && explicitDepth !== 0) {
        return explicitDepth;
    }

    const category = edge.categoryKey || "";
    if (category.includes("branch")) {
        return 34;
    }

    if (category.includes("decision")) {
        return 26;
    }

    if (category.includes("messaging")) {
        return 18;
    }

    if (category.includes("artifact")) {
        return 12;
    }

    return 8;
}

function resolveEdgeOpacity(edge) {
    return clamp(resolveFiniteNumber(edge?.opacity, edge?.isPrimaryPath ? 0.96 : 0.58), 0.18, 1);
}

function resolveNodeVisualKind(node) {
    if (isRoleNode(node)) {
        return "role";
    }

    if (isBranchNode(node)) {
        return "branch";
    }

    return "step";
}

function loadModelAsset(kind) {
    if (modelAssetPromises.has(kind)) {
        return modelAssetPromises.get(kind);
    }

    const config = nodeModelConfigs[kind];
    if (!config?.assetUrl) {
        return Promise.resolve(null);
    }

    const promise = modelLoader.loadAsync(config.assetUrl)
        .then(gltf => {
            const template = gltf.scene || gltf.scenes?.[0];
            if (!template) {
                throw new Error(`${kind} node GLB did not contain a scene.`);
            }

            const bounds = new THREE.Box3().setFromObject(template);
            const size = bounds.getSize(new THREE.Vector3());
            const center = bounds.getCenter(new THREE.Vector3());
            const hasSkinnedMesh = (() => {
                let skinned = false;
                template.traverse(child => {
                    if (child.isSkinnedMesh) {
                        skinned = true;
                    }
                });
                return skinned;
            })();
            return {
                template,
                min: bounds.min.clone(),
                center,
                size,
                hasSkinnedMesh
            };
        })
        .catch(error => {
            modelAssetPromises.delete(kind);
            console.error(`CanDoItAll WebGL ${kind} model failed to load.`, error);
            throw error;
        });

    modelAssetPromises.set(kind, promise);
    return promise;
}

function resolveNodeFrame(node, state) {
    const width = Number(node.width) || 220;
    const height = Number(node.height) || 128;
    const depth = Number(node.depth) || 28;
    const isSelected = state.selectedNodeIds.has(node.id) || state.chromeState.connectSourceNodeId === node.id;

    return {
        width,
        height,
        depth,
        isSelected
    };
}

function createHitMesh(width, height, depth, nodeId) {
    const hitMesh = new THREE.Mesh(
        new THREE.BoxGeometry(width, height, Math.max(34, depth)),
        new THREE.MeshBasicMaterial({
            transparent: true,
            opacity: 0,
            depthWrite: false
        }));
    hitMesh.userData = {
        nodeId
    };
    return hitMesh;
}

function createStandardNodeVisual(node, colors, frame) {
    const { width, height, depth } = frame;
    const geometry = new THREE.BoxGeometry(width, height, depth);
    const material = new THREE.MeshPhongMaterial({
        color: colors.fill,
        emissive: new THREE.Color(frame.isSelected ? colors.accent : "#000000"),
        emissiveIntensity: frame.isSelected ? 0.12 : 0,
        shininess: 55,
        transparent: true,
        opacity: 0.96
    });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.userData = {
        nodeId: node.id
    };
    const edges = new THREE.LineSegments(
        new THREE.EdgesGeometry(geometry),
        new THREE.LineBasicMaterial({
            color: colors.border,
            transparent: true,
            opacity: 0.92
        }));

    const accentBand = new THREE.Mesh(
        new THREE.BoxGeometry(width * 0.98, 8, 4),
        new THREE.MeshBasicMaterial({
            color: colors.accent
        }));
    accentBand.position.set(0, (height / 2) - 6, (depth / 2) + 2);

    return {
        mesh,
        objects: [mesh, edges, accentBand]
    };
}

function createRoleNodeBase(colors, frame) {
    const radius = Math.max(24, Math.min(frame.width, frame.depth) * 0.24);
    const haloRadius = radius * 1.18;
    const baseY = (-frame.height / 2) + 10;

    const pedestal = new THREE.Mesh(
        new THREE.CylinderGeometry(radius * 1.04, radius * 0.92, 10, 28),
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(frame.isSelected ? colors.accent : "#111827"),
            emissiveIntensity: frame.isSelected ? 0.22 : 0.04,
            shininess: 75,
            transparent: true,
            opacity: 0.96
        }));
    pedestal.position.y = baseY;

    const rim = new THREE.Mesh(
        new THREE.TorusGeometry(haloRadius, 3.6, 18, 42),
        new THREE.MeshBasicMaterial({
            color: colors.accent,
            transparent: true,
            opacity: frame.isSelected ? 0.72 : 0.3
        }));
    rim.rotation.x = Math.PI / 2;
    rim.position.y = baseY + 6.5;

    return {
        pedestal,
        rim,
        modelBottomY: baseY + 7
    };
}

function createRoleNodeFallback(colors, frame) {
    const fallback = new THREE.Mesh(
        new THREE.BoxGeometry(frame.width * 0.32, frame.height * 0.58, frame.depth * 0.22),
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(colors.accent),
            emissiveIntensity: frame.isSelected ? 0.18 : 0.05,
            shininess: 55,
            transparent: true,
            opacity: 0.88
        }));
    fallback.position.y = (-frame.height / 2) + ((frame.height * 0.58) / 2) + 18;
    return fallback;
}

function createStepNodeBase(colors, frame) {
    const baseY = (-frame.height / 2) + 9;
    const platform = new THREE.Mesh(
        new THREE.BoxGeometry(frame.width * 0.44, 12, frame.depth * 0.46),
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(frame.isSelected ? colors.accent : "#0f172a"),
            emissiveIntensity: frame.isSelected ? 0.24 : 0.06,
            shininess: 88,
            transparent: true,
            opacity: 0.94
        }));
    platform.position.y = baseY;

    const rail = new THREE.LineSegments(
        new THREE.EdgesGeometry(new THREE.BoxGeometry(frame.width * 0.48, 14, frame.depth * 0.5)),
        new THREE.LineBasicMaterial({
            color: colors.border,
            transparent: true,
            opacity: frame.isSelected ? 0.88 : 0.34
        }));
    rail.position.y = baseY + 1;

    return {
        platform,
        rim: rail,
        modelBottomY: baseY + 8
    };
}

function createStepNodeFallback(colors, frame) {
    const fallback = new THREE.Mesh(
        new THREE.BoxGeometry(frame.width * 0.24, frame.height * 0.34, frame.depth * 0.2),
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(colors.accent),
            emissiveIntensity: frame.isSelected ? 0.16 : 0.04,
            shininess: 62,
            transparent: true,
            opacity: 0.86
        }));
    fallback.position.y = (-frame.height / 2) + ((frame.height * 0.34) / 2) + 24;
    return fallback;
}

function createBranchNodeBase(colors, frame) {
    const radius = Math.max(28, Math.min(frame.width, frame.depth) * 0.2);
    const baseY = (-frame.height / 2) + 10;
    const pedestal = new THREE.Mesh(
        new THREE.CylinderGeometry(radius, radius * 0.92, 11, 8),
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(frame.isSelected ? colors.accent : "#111827"),
            emissiveIntensity: frame.isSelected ? 0.26 : 0.06,
            shininess: 78,
            transparent: true,
            opacity: 0.94
        }));
    pedestal.position.y = baseY;

    const halo = new THREE.LineSegments(
        new THREE.EdgesGeometry(new THREE.CylinderGeometry(radius * 1.14, radius * 1.06, 13, 8)),
        new THREE.LineBasicMaterial({
            color: colors.border,
            transparent: true,
            opacity: frame.isSelected ? 0.86 : 0.38
        }));
    halo.position.y = baseY + 0.5;

    return {
        pedestal,
        rim: halo,
        modelBottomY: baseY + 8
    };
}

function createBranchNodeFallback(colors, frame) {
    const fallback = new THREE.Mesh(
        new THREE.BoxGeometry(frame.width * 0.28, frame.height * 0.28, frame.depth * 0.28),
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(colors.accent),
            emissiveIntensity: frame.isSelected ? 0.16 : 0.04,
            shininess: 70,
            transparent: true,
            opacity: 0.88
        }));
    fallback.rotation.y = Math.PI / 4;
    fallback.position.y = (-frame.height / 2) + ((frame.height * 0.28) / 2) + 24;
    return fallback;
}

function markModelInstance(instance) {
    instance.traverse(child => {
        child.userData = {
            ...child.userData,
            skipDispose: true
        };
        child.frustumCulled = false;
    });
}

function buildNodeModelInstance(kind, asset, frame, modelBottomY) {
    const config = nodeModelConfigs[kind] || nodeModelConfigs.step;
    const instance = asset.hasSkinnedMesh
        ? cloneSkeleton(asset.template)
        : asset.template.clone(true);
    markModelInstance(instance);

    const availableWidth = Math.max(40, frame.width * config.fitWidthFactor);
    const availableHeight = Math.max(54, frame.height * config.fitHeightFactor);
    const availableDepth = Math.max(34, frame.depth * config.fitDepthFactor);
    const scale = Math.min(
        availableWidth / Math.max(asset.size.x, 1),
        availableHeight / Math.max(asset.size.y, 1),
        availableDepth / Math.max(asset.size.z, 1));

    instance.scale.setScalar(scale);
    instance.position.set(
        -asset.center.x * scale,
        modelBottomY - (asset.min.y * scale),
        -asset.center.z * scale);

    return instance;
}

function attachNodeModel(state, group, fallback, frame, node, kind) {
    loadModelAsset(kind)
        .then(asset => {
            if (state.nodeObjects.get(node.id) !== group) {
                return;
            }

            const instance = buildNodeModelInstance(kind, asset, frame, fallback.userData.modelBottomY || 0);
            group.add(instance);
            if (fallback.parent === group) {
                group.remove(fallback);
                destroyObject3D(fallback);
            }

            state.scheduleRender(state);
        })
        .catch(() => {
            if (state.nodeObjects.get(node.id) === group) {
                state.scheduleRender(state);
            }
        });
}

function createRoleNodeVisual(state, node, colors, frame) {
    const hitMesh = createHitMesh(frame.width * 0.82, frame.height, frame.depth * 0.82, node.id);
    const pedestal = createRoleNodeBase(colors, frame);
    const fallback = createRoleNodeFallback(colors, frame);
    fallback.userData = {
        ...fallback.userData,
        modelBottomY: pedestal.modelBottomY
    };

    const objects = [hitMesh, pedestal.pedestal, pedestal.rim, fallback];
    return {
        mesh: hitMesh,
        objects,
        onAdded(group) {
            attachNodeModel(state, group, fallback, frame, node, "role");
        }
    };
}

function createStepNodeVisual(state, node, colors, frame) {
    const hitMesh = createHitMesh(frame.width * 0.72, frame.height * 0.82, frame.depth * 0.72, node.id);
    const base = createStepNodeBase(colors, frame);
    const fallback = createStepNodeFallback(colors, frame);
    fallback.userData = {
        ...fallback.userData,
        modelBottomY: base.modelBottomY
    };

    return {
        mesh: hitMesh,
        objects: [hitMesh, base.platform, base.rim, fallback],
        onAdded(group) {
            attachNodeModel(state, group, fallback, frame, node, "step");
        }
    };
}

function createBranchNodeVisual(state, node, colors, frame) {
    const hitMesh = createHitMesh(frame.width * 0.74, frame.height * 0.8, frame.depth * 0.74, node.id);
    const base = createBranchNodeBase(colors, frame);
    const fallback = createBranchNodeFallback(colors, frame);
    fallback.userData = {
        ...fallback.userData,
        modelBottomY: base.modelBottomY
    };

    return {
        mesh: hitMesh,
        objects: [hitMesh, base.pedestal, base.rim, fallback],
        onAdded(group) {
            attachNodeModel(state, group, fallback, frame, node, "branch");
        }
    };
}

function createFlowMarker(color, accentColor, position, radius) {
    const group = new THREE.Group();
    const halo = new THREE.Mesh(
        new THREE.SphereGeometry(radius * 1.7, 24, 24),
        new THREE.MeshBasicMaterial({
            color,
            transparent: true,
            opacity: 0.16,
            depthWrite: false
        }));
    const sphere = new THREE.Mesh(
        new THREE.SphereGeometry(radius, 26, 26),
        new THREE.MeshPhongMaterial({
            color,
            emissive: new THREE.Color(accentColor || color),
            emissiveIntensity: 0.3,
            shininess: 88,
            transparent: true,
            opacity: 0.96
        }));
    group.add(halo, sphere);
    group.position.copy(position);
    group.userData = {
        skipHitTest: true
    };
    return group;
}

function resolveNodePosition(node) {
    return new THREE.Vector3(
        Number(node?.x) || 0,
        toSceneY(node?.y),
        Number(node?.z) || 0);
}

function resolveTagValue(node, prefix) {
    const expectedPrefix = prefix.toLowerCase();
    for (const tag of node?.tags || []) {
        const text = String(tag || "");
        if (text.toLowerCase().startsWith(expectedPrefix)) {
            return text.slice(prefix.length);
        }
    }

    return "";
}

function registerGuideNodeObject(state, node, group, hitMesh) {
    group.userData = {
        nodeId: node.id
    };
    state.nodeObjects.set(node.id, group);
    if (hitMesh) {
        state.nodeMeshes.push(hitMesh);
    }

    state.scene.add(group);
}

function createGuideHitSphere(radius, nodeId) {
    const hitMesh = new THREE.Mesh(
        new THREE.SphereGeometry(Math.max(18, radius), 18, 18),
        new THREE.MeshBasicMaterial({
            transparent: true,
            opacity: 0,
            depthWrite: false
        }));
    hitMesh.userData = {
        nodeId
    };
    return hitMesh;
}

function createOriginGuideObject(state, node, colors, frame) {
    const radius = Math.max(14, Math.min(frame.width, frame.height, frame.depth) * 0.32);
    const position = resolveNodePosition(node);
    const group = new THREE.Group();
    const halo = new THREE.Mesh(
        new THREE.SphereGeometry(radius * 2.05, 26, 26),
        new THREE.MeshBasicMaterial({
            color: colors.accent,
            transparent: true,
            opacity: 0.14,
            depthWrite: false
        }));
    const core = new THREE.Mesh(
        new THREE.SphereGeometry(radius, 28, 28),
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(colors.accent),
            emissiveIntensity: frame.isSelected ? 0.35 : 0.16,
            shininess: 90,
            transparent: true,
            opacity: 0.96
        }));
    const ring = new THREE.Mesh(
        new THREE.TorusGeometry(radius * 1.45, 2.6, 14, 48),
        new THREE.MeshBasicMaterial({
            color: colors.accent,
            transparent: true,
            opacity: 0.72
        }));
    ring.rotation.x = Math.PI / 2;
    const hitMesh = createGuideHitSphere(radius * 2.2, node.id);
    group.add(halo, core, ring, hitMesh);
    group.position.copy(position);
    registerGuideNodeObject(state, node, group, hitMesh);
}

function createPlaneGuideObject(state, node, colors, frame) {
    const position = resolveNodePosition(node);
    const group = new THREE.Group();
    const geometry = new THREE.BoxGeometry(frame.width, frame.height, frame.depth);
    const cube = new THREE.Mesh(
        geometry,
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(colors.accent),
            emissiveIntensity: frame.isSelected ? 0.22 : 0.06,
            shininess: 70,
            transparent: true,
            opacity: 0.84
        }));
    const outline = new THREE.LineSegments(
        new THREE.EdgesGeometry(geometry),
        new THREE.LineBasicMaterial({
            color: colors.accent,
            transparent: true,
            opacity: 0.78
        }));
    const hitMesh = createHitMesh(frame.width * 1.35, frame.height * 1.35, frame.depth * 1.35, node.id);
    group.add(cube, outline, hitMesh);
    group.position.copy(position);
    registerGuideNodeObject(state, node, group, hitMesh);
}

function createPointerGuideObject(state, node, colors, frame) {
    const radius = Math.max(14, Math.min(frame.width, frame.height, frame.depth) * 0.36);
    const position = resolveNodePosition(node);
    const group = new THREE.Group();
    const halo = new THREE.Mesh(
        new THREE.SphereGeometry(radius * 1.9, 28, 28),
        new THREE.MeshBasicMaterial({
            color: colors.accent,
            transparent: true,
            opacity: frame.isSelected ? 0.22 : 0.12,
            depthWrite: false
        }));
    const core = new THREE.Mesh(
        new THREE.SphereGeometry(radius, 32, 32),
        new THREE.MeshPhongMaterial({
            color: colors.fill,
            emissive: new THREE.Color(colors.accent),
            emissiveIntensity: frame.isSelected ? 0.42 : 0.22,
            shininess: 95,
            transparent: true,
            opacity: 0.96
        }));
    const hitMesh = createGuideHitSphere(radius * 2.1, node.id);
    group.add(halo, core, hitMesh);
    group.position.copy(position);
    registerGuideNodeObject(state, node, group, hitMesh);
}

function createArrowGuideObject(state, node, colors, frame) {
    const startNodeId = resolveTagValue(node, "arrow-start:");
    const startNode = startNodeId
        ? state.nodeLookup.get(startNodeId)
        : null;
    const start = startNode
        ? resolveNodePosition(startNode)
        : new THREE.Vector3(0, 0, 0);
    const end = resolveNodePosition(node);
    const delta = end.clone().sub(start);
    const length = delta.length();
    if (length < 0.01) {
        createPointerGuideObject(state, node, colors, frame);
        return;
    }

    const direction = delta.clone().normalize();
    const group = new THREE.Group();
    const headLength = Math.min(Math.max(22, frame.height * 1.25), length * 0.36);
    const headRadius = Math.max(9, frame.width * 0.42);
    const shaftLength = Math.max(1, length - headLength);
    const shaftRadius = Math.max(3.6, Math.min(8.5, headRadius * 0.36));
    const rotation = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction);
    const shaft = new THREE.Mesh(
        new THREE.CylinderGeometry(shaftRadius, shaftRadius, shaftLength, 18),
        new THREE.MeshPhongMaterial({
            color: colors.accent,
            emissive: new THREE.Color(colors.accent),
            emissiveIntensity: frame.isSelected ? 0.26 : 0.12,
            shininess: 80,
            transparent: true,
            opacity: 0.9
        }));
    shaft.position.copy(start.clone().add(direction.clone().multiplyScalar(shaftLength / 2)));
    shaft.quaternion.copy(rotation);

    const head = new THREE.Mesh(
        new THREE.ConeGeometry(headRadius, headLength, 26),
        new THREE.MeshPhongMaterial({
            color: colors.accent,
            emissive: new THREE.Color(colors.accent),
            emissiveIntensity: frame.isSelected ? 0.36 : 0.18,
            shininess: 92,
            transparent: true,
            opacity: 0.96
        }));
    head.position.copy(start.clone().add(direction.clone().multiplyScalar(shaftLength + (headLength / 2))));
    head.quaternion.copy(rotation);

    const halo = new THREE.Mesh(
        new THREE.SphereGeometry(headRadius * 1.35, 20, 20),
        new THREE.MeshBasicMaterial({
            color: colors.accent,
            transparent: true,
            opacity: frame.isSelected ? 0.2 : 0.1,
            depthWrite: false
        }));
    halo.position.copy(end);

    const hitMesh = createGuideHitSphere(Math.max(headRadius * 1.6, 18), node.id);
    hitMesh.position.copy(end);
    group.add(shaft, head, halo, hitMesh);
    registerGuideNodeObject(state, node, group, hitMesh);
}

function tryCreateGuideNodeObject(state, node, colors, frame) {
    if (!isVisualGuideNode(node)) {
        return false;
    }

    const kind = (node?.kind || "").toLowerCase();
    if (kind.includes("arrow")) {
        createArrowGuideObject(state, node, colors, frame);
    } else if (kind.includes("plane")) {
        createPlaneGuideObject(state, node, colors, frame);
    } else if (kind.includes("pointer")) {
        createPointerGuideObject(state, node, colors, frame);
    } else {
        createOriginGuideObject(state, node, colors, frame);
    }

    return true;
}

function isStructuralFlowEdge(edge) {
    const categoryKey = (edge?.categoryKey || "").toLowerCase();
    return !!edge?.isPrimaryPath ||
        categoryKey.includes("structural") ||
        categoryKey.includes("branch");
}

function resolveStepEndpoints(state) {
    const processNodes = (state.surface.nodes || []).filter(isProcessStepNode);
    if (!processNodes.length) {
        return {
            firstNode: null,
            lastNode: null
        };
    }

    const inboundCounts = new Map(processNodes.map(node => [node.id, 0]));
    const outboundCounts = new Map(processNodes.map(node => [node.id, 0]));

    for (const edge of state.surface.edges || []) {
        if (!isStructuralFlowEdge(edge)) {
            continue;
        }

        const sourceNode = state.nodeLookup.get(edge.sourceNodeId);
        const targetNode = state.nodeLookup.get(edge.targetNodeId);
        if (!sourceNode || !targetNode || isRoleNode(sourceNode) || isRoleNode(targetNode)) {
            continue;
        }

        if (outboundCounts.has(edge.sourceNodeId)) {
            outboundCounts.set(edge.sourceNodeId, outboundCounts.get(edge.sourceNodeId) + 1);
        }

        if (inboundCounts.has(edge.targetNodeId)) {
            inboundCounts.set(edge.targetNodeId, inboundCounts.get(edge.targetNodeId) + 1);
        }
    }

    const firstNode = processNodes.find(node => (inboundCounts.get(node.id) || 0) === 0) || processNodes[0];
    const lastNode = [...processNodes].reverse().find(node => (outboundCounts.get(node.id) || 0) === 0) || processNodes[processNodes.length - 1];
    return {
        firstNode,
        lastNode
    };
}

function resolveMarkerDirection(state, nodeId, outbound) {
    const edges = (state.surface.edges || []).filter(edge => {
        if (!isStructuralFlowEdge(edge)) {
            return false;
        }

        return outbound
            ? edge.sourceNodeId === nodeId
            : edge.targetNodeId === nodeId;
    });
    const edge = edges[0];
    if (!edge) {
        return new THREE.Vector3(0, 0, -1);
    }

    const sourceNode = state.nodeLookup.get(edge.sourceNodeId);
    const targetNode = state.nodeLookup.get(edge.targetNodeId);
    if (!sourceNode || !targetNode) {
        return new THREE.Vector3(0, 0, -1);
    }

    const direction = outbound
        ? new THREE.Vector3(targetNode.x - sourceNode.x, 0, targetNode.z - sourceNode.z)
        : new THREE.Vector3(targetNode.x - sourceNode.x, 0, targetNode.z - sourceNode.z);
    if (direction.lengthSq() < 0.0001) {
        return new THREE.Vector3(0, 0, -1);
    }

    return direction.normalize();
}

function createFlowMarkers(state) {
    const markerObjects = [];
    const { firstNode, lastNode } = resolveStepEndpoints(state);
    if (!firstNode && !lastNode) {
        return markerObjects;
    }

    const firstDirection = firstNode
        ? resolveMarkerDirection(state, firstNode.id, true)
        : null;
    const lastDirection = lastNode
        ? resolveMarkerDirection(state, lastNode.id, false)
        : null;

    if (firstNode && firstDirection) {
        const offset = Math.max(118, (Number(firstNode.width) || 220) * 0.54);
        const position = new THREE.Vector3(
            firstNode.x - (firstDirection.x * offset),
            toSceneY(firstNode.y) - ((Number(firstNode.height) || 128) * 0.18),
            firstNode.z - (firstDirection.z * offset));
        markerObjects.push(createFlowMarker("#22c55e", "#86efac", position, 18));
    }

    if (lastNode && lastDirection) {
        const offset = Math.max(118, (Number(lastNode.width) || 220) * 0.54);
        const position = new THREE.Vector3(
            lastNode.x + (lastDirection.x * offset),
            toSceneY(lastNode.y) - ((Number(lastNode.height) || 128) * 0.18),
            lastNode.z + (lastDirection.z * offset));
        markerObjects.push(createFlowMarker("#ef4444", "#fca5a5", position, 18));
    }

    return markerObjects;
}

function createNodeObject(state, node) {
    const colors = resolveNodeColors(node);
    const frame = resolveNodeFrame(node, state);
    if (tryCreateGuideNodeObject(state, node, colors, frame)) {
        return;
    }

    const group = new THREE.Group();
    const visualKind = resolveNodeVisualKind(node);
    const visual = visualKind === "role"
        ? createRoleNodeVisual(state, node, colors, frame)
        : visualKind === "branch"
            ? createBranchNodeVisual(state, node, colors, frame)
            : createStepNodeVisual(state, node, colors, frame);

    group.add(...visual.objects);
    group.position.set(node.x || 0, toSceneY(node.y), node.z || 0);
    group.userData = {
        nodeId: node.id
    };

    state.nodeObjects.set(node.id, group);
    state.nodeMeshes.push(visual.mesh);
    state.scene.add(group);
    visual.onAdded?.(group);
}

function createEdgeObject(state, edge) {
    const sourceNode = state.nodeLookup.get(edge.sourceNodeId);
    const targetNode = state.nodeLookup.get(edge.targetNodeId);
    if (!sourceNode || !targetNode) {
        return;
    }

    const sourceAnchor = state.anchorLookup.get(edge.sourceAnchorId);
    const targetAnchor = state.anchorLookup.get(edge.targetAnchorId);
    if (!sourceAnchor || !targetAnchor) {
        return;
    }

    const sourcePoint = resolveAnchorPosition(sourceNode, sourceAnchor);
    const targetPoint = resolveAnchorPosition(targetNode, targetAnchor);
    const depth = resolveEdgeDepth(edge);
    const control = new THREE.Vector3(
        (sourcePoint.x + targetPoint.x) / 2,
        (sourcePoint.y + targetPoint.y) / 2,
        Math.max(sourcePoint.z, targetPoint.z) + depth);
    const curve = new THREE.QuadraticBezierCurve3(sourcePoint, control, targetPoint);
    const points = curve.getPoints(32);
    const isSelected = state.chromeState.selectedEdgeId === edge.id || state.chromeState.reconnectEdgeId === edge.id;
    const emphasis = isSelected
        ? Math.max(clamp(resolveFiniteNumber(edge?.emphasis, 1), 0.55, 2.4), 1.9)
        : clamp(resolveFiniteNumber(edge?.emphasis, edge?.isPrimaryPath ? 1.7 : 0.82), 0.55, 2.4);
    const opacity = isSelected
        ? 1
        : resolveEdgeOpacity(edge);
    const group = new THREE.Group();

    if (edge.isPrimaryPath || emphasis > 1.05 || isSelected) {
        const halo = new THREE.Mesh(
            new THREE.TubeGeometry(curve, 42, 5.8 * emphasis, 12, false),
            new THREE.MeshBasicMaterial({
                color: edge.accentColor || "#2563eb",
                transparent: true,
                opacity: isSelected
                    ? 0.32
                    : Math.min(0.16 + (opacity * 0.18), 0.38)
            }));
        const tube = new THREE.Mesh(
            new THREE.TubeGeometry(curve, 42, 2.2 * emphasis, 12, false),
            new THREE.MeshPhongMaterial({
                color: edge.accentColor || "#2563eb",
                emissive: new THREE.Color(edge.accentColor || "#2563eb"),
                emissiveIntensity: isSelected
                    ? 0.24
                    : edge.isPrimaryPath ? 0.18 : 0.08,
                shininess: 85,
                transparent: true,
                opacity: isSelected
                    ? 0.96
                    : edge.isPrimaryPath
                        ? Math.min(0.76 + (opacity * 0.18), 0.94)
                        : Math.min(0.34 + (opacity * 0.2), 0.66)
            }));
        group.add(halo, tube);
    }

    const geometry = new THREE.BufferGeometry().setFromPoints(points);
    const material = new THREE.LineBasicMaterial({
        color: edge.accentColor || "#2563eb",
        transparent: true,
        opacity
    });
    const line = new THREE.Line(geometry, material);
    group.add(line);

    const hitMesh = new THREE.Mesh(
        new THREE.TubeGeometry(curve, 28, Math.max(12, 8 * emphasis), 10, false),
        new THREE.MeshBasicMaterial({
            transparent: true,
            opacity: 0,
            depthWrite: false
        }));
    hitMesh.userData = {
        edgeId: edge.id
    };
    group.add(hitMesh);

    group.userData = {
        edgeId: edge.id,
        sourceNodeId: edge.sourceNodeId,
        targetNodeId: edge.targetNodeId
    };

    state.edgeObjects.set(edge.id, group);
    state.edgeHitMeshes.push(hitMesh);
    state.scene.add(group);
}

function createNodeAndAnchorLookups(state) {
    state.nodeLookup = new Map();
    state.anchorLookup = new Map();

    for (const node of state.surface.nodes || []) {
        state.nodeLookup.set(node.id, node);
        for (const anchor of node.anchors || []) {
            state.anchorLookup.set(anchor.id, anchor);
        }
    }
}

export function clearScene(state) {
    for (const nodeGroup of state.nodeObjects.values()) {
        state.scene.remove(nodeGroup);
        destroyObject3D(nodeGroup);
    }

    for (const edgeObject of state.edgeObjects.values()) {
        state.scene.remove(edgeObject);
        destroyObject3D(edgeObject);
    }

    for (const markerObject of state.markerObjects || []) {
        state.scene.remove(markerObject);
        destroyObject3D(markerObject);
    }

    state.nodeObjects.clear();
    state.edgeObjects.clear();
    state.nodeMeshes.length = 0;
    state.edgeHitMeshes.length = 0;
    if (state.markerObjects) {
        state.markerObjects.length = 0;
    }
    state.projectedNodes.clear();
    state.projectedEdges.clear();
    state.projectedAnchors.clear();
}

export function rebuildScene(state) {
    clearScene(state);
    createNodeAndAnchorLookups(state);

    for (const node of state.surface.nodes || []) {
        createNodeObject(state, node);
    }

    for (const edge of state.surface.edges || []) {
        createEdgeObject(state, edge);
    }

    for (const marker of createFlowMarkers(state)) {
        state.markerObjects.push(marker);
        state.scene.add(marker);
    }

    state.diagnostics.nodeCount = state.surface.nodes?.length || 0;
    state.diagnostics.edgeCount = state.surface.edges?.length || 0;
}
