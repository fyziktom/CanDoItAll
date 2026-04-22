import {
    THREE,
    clamp,
    isRoleNode,
    resolveAnchorPosition,
    resolveFiniteNumber,
    toSceneY
} from "./02-webgl-workbench-core.js";
import { GLTFLoader } from "../../../vendor/GLTFLoader.js";
import { clone as cloneSkeleton } from "../../../vendor/utils/SkeletonUtils.js";

const roleModelLoader = new GLTFLoader();
const roleModelAssetUrl = new URL("../../../assets/model/lowpoly_person_boxing.glb", import.meta.url).href;
let roleModelAssetPromise = null;

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

function loadRoleModelAsset() {
    if (roleModelAssetPromise) {
        return roleModelAssetPromise;
    }

    roleModelAssetPromise = roleModelLoader.loadAsync(roleModelAssetUrl)
        .then(gltf => {
            const template = gltf.scene || gltf.scenes?.[0];
            if (!template) {
                throw new Error("Role node GLB did not contain a scene.");
            }

            const bounds = new THREE.Box3().setFromObject(template);
            const size = bounds.getSize(new THREE.Vector3());
            const center = bounds.getCenter(new THREE.Vector3());
            return {
                template,
                min: bounds.min.clone(),
                center,
                size
            };
        })
        .catch(error => {
            roleModelAssetPromise = null;
            console.error("CanDoItAll WebGL role model failed to load.", error);
            throw error;
        });

    return roleModelAssetPromise;
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

function createRoleNodePedestal(colors, frame) {
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

function markRoleModelInstance(instance) {
    instance.traverse(child => {
        child.userData = {
            ...child.userData,
            skipDispose: true
        };
        child.frustumCulled = false;
    });
}

function buildRoleModelInstance(asset, frame, modelBottomY) {
    const instance = cloneSkeleton(asset.template);
    markRoleModelInstance(instance);

    const availableWidth = Math.max(46, frame.width * 0.56);
    const availableHeight = Math.max(64, frame.height * 0.92);
    const availableDepth = Math.max(42, frame.depth * 0.56);
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

function attachRoleModel(state, group, fallback, frame, node) {
    loadRoleModelAsset()
        .then(asset => {
            if (state.nodeObjects.get(node.id) !== group) {
                return;
            }

            const instance = buildRoleModelInstance(asset, frame, fallback.userData.modelBottomY || 0);
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
    const pedestal = createRoleNodePedestal(colors, frame);
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
            attachRoleModel(state, group, fallback, frame, node);
        }
    };
}

function createNodeObject(state, node) {
    const colors = resolveNodeColors(node);
    const group = new THREE.Group();
    const frame = resolveNodeFrame(node, state);
    const visual = isRoleNode(node)
        ? createRoleNodeVisual(state, node, colors, frame)
        : createStandardNodeVisual(node, colors, frame);

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

    state.nodeObjects.clear();
    state.edgeObjects.clear();
    state.nodeMeshes.length = 0;
    state.edgeHitMeshes.length = 0;
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

    state.diagnostics.nodeCount = state.surface.nodes?.length || 0;
    state.diagnostics.edgeCount = state.surface.edges?.length || 0;
}
