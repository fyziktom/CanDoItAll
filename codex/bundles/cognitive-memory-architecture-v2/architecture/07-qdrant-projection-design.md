# Qdrant Projection Design

## Principle

Qdrant is a projection store, not the source of truth. It should hold search-optimized vectors and metadata payloads for fast recall.

## Existing RAG Driver State

Current `QdrantRagDriver` supports:

- collection creation,
- single vector per point,
- upsert/delete/search,
- typed payload filters on search,
- payload index creation through generic contracts,
- delete by generic payload filter,
- capability discovery,
- metadata payload,
- tags.

Remaining optional or adapter-owned features for Cognitive Memory:

- named vectors or multi-vector model,
- Cognitive Memory-owned structured payload schema,
- Cognitive Memory-owned projection rebuild state and version metadata,
- collection health/diagnostics.

## Recommended V1 Compatible Design

Use typed filters and payload indexes for scoped recall. Use multiple collections instead of named vectors in V1 because named vectors remain optional:

```text
cm_project_{projectId}_semantic
cm_project_{projectId}_procedures
cm_project_{projectId}_episodes
cm_project_{projectId}_decisions
cm_global_topics
cm_cross_project_procedures
```

Each point has one semantic vector, plus payloads:

```json
{
  "projectId": "...",
  "memoryItemId": "...",
  "memoryKind": "Semantic",
  "projectionType": "ProjectSummary",
  "sourceType": "mindmap",
  "sourceId": "...",
  "sourceItemKey": "...",
  "sourceHash": "...",
  "canonicalVersion": 1,
  "projectionVersion": 1,
  "embeddingProvider": "onnx",
  "embeddingModel": "Xenova/all-MiniLM-L6-v2",
  "embeddingDimensions": 384,
  "scope": "testing",
  "topics": ["docker", "deployment", "simulation"],
  "x": 120.0,
  "y": 300.0,
  "z": 0.0,
  "spatialClusterId": "...",
  "semanticClusterId": "...",
  "graphCommunityId": "...",
  "confidence": 0.87,
  "humanValidationStatus": "Unreviewed",
  "activationScore": 0.74,
  "updatedAtUtc": "..."
}
```

## Recommended V1.1 Driver Extensions

Closed by `codex/bundles/cognitive-memory-projection-boundary-hardening`:

- `RagFilter`
- `RagFilterCondition`
- `RagFilterGroup`
- `RagFilterValue`
- `RagPayloadIndexRequest`
- `RagPayloadIndexResult`
- `RagDeleteByFilterRequest`
- capability flags for filters, payload indexes, delete-by-filter, and optional named vectors

The Cognitive Memory module should build typed filters over its own payload fields, such as:

```text
projectId = currentProject
memoryKind in [Procedure, Decision]
scope in [testing, deployment]
humanValidationStatus != HumanRejected
```

## Named Vectors Future

If named vectors are supported:

- `semantic` vector: embedding of canonical text,
- `spatial` vector: normalized X/Y/Z and spatial features,
- `graph` vector: graph/community embedding,
- `activation` pseudo-vector is not recommended; keep activation in payload/DB.

Search can run multiple channels and combine scores in the recall orchestrator.

## Payload Indexes

Create indexes for frequently filtered fields:

- `projectId`
- `memoryKind`
- `projectionType`
- `sourceType`
- `sourceId`
- `sourceItemKey`
- `scope`
- `topics`
- `humanValidationStatus`
- `projectionVersion`
- `embeddingModel`
- `updatedAtUtc`

## Projection Rebuild

Projection rebuild is required when:

- embedding model changes,
- projection text template changes,
- payload schema changes,
- source hash changes,
- canonical item changes,
- relation/context changes enough to update summary text.

Recommended algorithm:

```text
ensure payload indexes for scoped/filter-heavy fields
delete stale projections by generic metadata filter
for each current projection:
  build projection text
  embed text and persist embedding profile metadata
  upsert Qdrant point with projection payload
  update MemoryProjectionRecord hash/version/profile
```

## Search Strategy

Recall should not rely on Qdrant alone. Use Qdrant as one candidate channel:

```text
candidateSet =
  exactSourceMatches
  + lexicalMatches
  + qdrantVectorMatches
  + graphNeighbors
  + recentWorkingMemory
  + highActivationProcedures
```

Then merge, score, and focus in `IRecallOrchestrator`.
