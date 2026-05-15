# Qdrant Projection Design

## Principle

Qdrant is a projection store, not the source of truth. It should hold search-optimized vectors and metadata payloads for fast recall.

## Existing RAG Driver State

Current `QdrantRagDriver` supports:

- collection creation,
- single vector per point,
- upsert/delete/search,
- metadata payload,
- tags.

Current missing features for Cognitive Memory:

- payload filtering,
- payload index creation,
- named vectors or multi-vector model,
- projection rebuild lifecycle,
- source-based deletion/replacement,
- structured payload schema,
- collection health/diagnostics.

## Recommended V1 Compatible Design

Use multiple collections instead of named vectors if driver extension is not ready:

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

Add to RAG abstractions:

- `RagFilter`
- `RagFilterCondition`
- `RagPayloadIndexRequest`
- `RagProjectionState`
- optional `RagNamedVectorCollectionOptions`
- optional `RagNamedVectorSearchRequest`

The Cognitive Memory module can then perform filtered searches such as:

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
for each stale projection:
  build projection text
  embed text
  upsert Qdrant point
  update MemoryProjectionRecord hash/version
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
