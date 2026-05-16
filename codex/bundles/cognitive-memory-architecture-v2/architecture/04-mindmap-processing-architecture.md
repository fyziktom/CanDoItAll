# Mindmap Processing Architecture

## Purpose

Mindmaps/project structures are not just documents. They are curated spatial-graph knowledge maps. The system should preserve and exploit:

- node text,
- node title/subtitle/notes,
- parent/child structure,
- typed links,
- coordinates,
- local density,
- branch membership,
- intentional separation between topics,
- metadata and source references.

## Current Code Mapping

Existing model:

- `ProjectObjectRecord.PositionX`
- `ProjectObjectRecord.PositionY`
- `ProjectObjectRecord.ParentNodeKey`
- `ProjectObjectRecord.MetadataJson`
- `ProjectObjectLinkRecord.SourceNodeKey`
- `ProjectObjectLinkRecord.TargetNodeKey`
- `ProjectObjectLinkRecord.LinkKind`

Target extension:

- `PositionZ` migration or `MetadataJson.z` fallback.
- `LayoutId`, `LayoutVersion`, `CoordinateSpace`, `CoordinateConfidence`.

## Ingestion Flow

```text
ProjectStructureSurface / ProjectObjectRecord
  -> MindMapSourceAdapter
  -> MindMapNodeSnapshot
  -> SourceItemRecord
  -> FeatureExtractor
       -> SemanticFeatures
       -> SpatialFeatures
       -> GraphFeatures
       -> MetadataFeatures
  -> Clustering
       -> Spatial clusters
       -> Semantic clusters
       -> Graph communities
       -> Hybrid relation candidates
  -> ProjectionBuilder
       -> Atomic source records
       -> local cluster summaries
       -> semantic topics
       -> project canonical summaries
       -> cross-project candidates
```

## Node Feature Vector

### Text Feature Input

Build context-enriched text:

```text
Title: {Title}
Subtitle: {Subtitle}
ObjectType: {ObjectType}
Path: {root > parent > node}
Notes: {Notes}
Tags: {tags}
Linked nodes: {small linked node summaries}
```

Do not embed raw metadata JSON blindly. Extract meaningful keys first.

### Spatial Features

- normalized X/Y/Z,
- distance from root,
- distance from parent,
- sibling index/angle if available,
- local node density,
- branch centroid distance,
- edge crossing density if later available,
- coordinate-space version.

### Graph Features

- parent id,
- ancestor path,
- child count,
- graph degree,
- typed incoming/outgoing links,
- shortest path to project root,
- shortest path to active process/workflow/repository nodes,
- link-kind weights.

### Semantic Features

- embedding vector,
- extracted entities,
- normalized topic key,
- scope labels such as production/testing/simulation/development/security/deployment,
- intent labels such as fact/procedure/decision/evidence/problem.

## Multi-View Similarity

Do not compute one universal cluster. Keep separate views:

```text
semanticSimilarity
spatialSimilarity
structuredGraphSimilarity
metadataSimilarity
temporalSimilarity
```

Use the Score Geometry Driver for hybrid decisions. Mindmap processing should produce a `MindMapSimilarity` score vector and compare it to cluster, separation, or promotion shapes:

```text
node features
  -> MindMapSimilarity score vector
  -> semantic cluster shape / spatial cluster shape / context-separation boundary
  -> cluster assignment trace or separated-relation trace
  -> optional display confidence
```

Task-specific profiles may define different shapes or scalar display projections, but they must not erase the individual semantic, graph, spatial, metadata, temporal, activation, and source-confidence dimensions.

## Context-Separation Detection

The most important special case:

```text
semanticSimilarity = high
spatialSimilarity = low
graphSimilarity = low or medium
scope labels differ
```

This should produce:

```text
RelationKind.SemanticallyRelatedContextSeparated
```

not a forced merge.

## Projection Records From One Node

One source node can produce multiple records:

| Record | Example |
|---|---|
| Atomic source | exact test Docker simulation node |
| Local cluster | testing/simulation infrastructure cluster |
| Semantic topic | Docker deployment |
| Project summary | deployment options in this project |
| Cross-project topic | Docker usage across CanDoItAll/Zyphonote/etc. |
| Procedure | how to run the test Docker simulation |
| Decision | why testing Docker is separate from production Docker |

## Recommended Staging

### V1

- Extract source items from `ProjectObjectRecord` and `ProjectObjectLinkRecord`.
- Use X/Y and optional Z from metadata.
- Store atomic and canonical records.
- Store explicit graph relations.
- Project semantic vectors to Qdrant.

### V1.1

- Add spatial clustering.
- Add context-separated relation detection.
- Add local cluster summaries.

### V1.2

- Add cross-project topic consolidation.
- Add UI projection inspector.
- Add distributed clustering jobs.
