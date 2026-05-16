# Gap Analysis and Corrections

## Correction: Do Not Treat Embeddings as Memory

Earlier architecture language can easily drift into treating Qdrant records as the knowledge base. This is unsafe. Qdrant records are only projections and can be rebuilt. The source of truth must be relational/storage/IPFS-backed source and canonical records.

## Correction: Do Not Merge Semantically Similar but Context-Separated Topics

Example: production Docker deployment and test/simulation Docker deployment.

These may have high semantic similarity but low spatial/graph proximity. The system must model this as a relation, not collapse both records into one fact.

Recommended relation:

```json
{
  "relationKind": "SemanticallyRelatedContextSeparated",
  "evidence": ["semanticSimilarityHigh", "spatialDistanceHigh", "differentParentBranch"],
  "reason": "Both discuss Docker deployment, but one belongs to production and the other belongs to testing/simulation."
}
```

## Correction: Mindmap Coordinates Are Curatorial Signals

The user intentionally places related-but-different areas apart. Therefore:

- coordinates must not be treated as cosmetic UI only,
- moving a node is a knowledge event,
- large spatial distance with high semantic similarity is a meaningful signal,
- layout version should be stored and used in consolidation.

## Correction: ML.NET KMeans Is Only One Tool

KMeans can be used for numeric/spatial clustering, but memory-like recall needs:

- graph proximity,
- semantic similarity,
- explicit typed relations,
- activation state,
- source confidence,
- role/goal relevance,
- human validation.

A single clustering algorithm would destroy too much context.

## Correction: MAF Is Executive Control, Not Memory Storage

Microsoft Agent Framework should orchestrate cognitive workflows and inject context. It should not own the durable memory model. The durable memory model belongs in the CanDoItAll module layer.

## Correction: SemanticCompletion Driver Is Useful but Narrow

The existing semantic module is excellent for local embeddings and classification/ranking. It should be wrapped behind a broader `ISemanticEmbeddingService` and used by:

- projection engine,
- recall intent classifier,
- relation detector,
- semantic ranking fallback.

It should not be forced to own canonicalization, consolidation, or memory graph semantics.

## Correction: Do Not Auto-Rewrite Truth During Sleep Cycle

Idle consolidation should create draft canonical updates, supersession candidates, contradiction records, and human-review tasks when confidence is not high enough. Raw source data must remain intact.

## Correction: Existing Workbench Is 2D Today

The uploaded workbench code stores `PositionX` and `PositionY`. The requested X/Y/Z memory model should support Z now, but implementation should be staged:

1. V1 reads/writes `z` through node `MetadataJson` when needed.
2. V1.1 adds a migration with `PositionZ` if 3D/mindmap layout becomes a first-class workbench feature.
3. Qdrant payload stores `x`, `y`, `z`, `layoutId`, `layoutVersion`, and `coordinateSpace`.

## Correction: Distributed Idle Compute Needs Deterministic Jobs

Mobile/tablet/PC idle workers must not independently mutate memory. They should execute deterministic job packets:

- source hash,
- algorithm version,
- model version,
- input refs,
- expected output schema,
- lease token,
- output hash,
- verification status.

The main node remains the authority that accepts or rejects outputs.
