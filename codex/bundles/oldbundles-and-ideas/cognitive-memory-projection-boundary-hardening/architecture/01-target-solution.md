# Target Solution

## Boundary Goal

Projection-backed Cognitive Memory should depend on generic projection and embedding capabilities, not direct Qdrant calls and not Cognitive Memory-specific fields inside reusable driver repos.

```text
Cognitive Memory canonical store
    -> projection planner
    -> CognitiveMemory.Rag adapter
    -> IRagDriver typed filter/lifecycle contracts
    -> Qdrant or another provider

Cognitive Memory projection planner
    -> CognitiveMemory.Semantics adapter
    -> IAgentTextEmbeddingGenerator
    -> AgentTextEmbedding with stable profile metadata
```

## RAG Driver Target

Add generic contracts such as the following, with exact naming left to the implementation agent:

- `RagFilter`
- `RagFilterCondition`
- `RagFilterOperator`
- `RagFilterValue`
- `RagFilterGroup`
- `RagPayloadIndexRequest`
- `RagPayloadIndexResult`
- `RagDeleteByFilterRequest` or an equivalent extension to delete requests
- capability flags for filters, payload indexes, delete-by-filter, and named vectors if supported

The contracts must be generic. Cognitive Memory can later put payload fields such as `projectId`, `sourceKind`, `sourceScopeId`, `sourceItemId`, `projectionVersion`, and `embeddingProfile` into metadata, but the RAG repo should only know how to filter/index/delete by generic payload fields.

## RAG Search Safety

Projection-backed recall should never require this unsafe shape:

```text
Qdrant search globally
    -> return top N across all projects/users
    -> Cognitive Memory post-filters out-of-scope hits
```

The safe shape is:

```text
Cognitive Memory builds typed scope filter
    -> IRagDriver.SearchAsync(filter: ...)
    -> provider filters before scoring/result cutoff where supported
    -> recall trace records filter, channel, and capability outcome
```

If a provider cannot support filters, the driver must report unsupported capability and fail predictably for strict modes.

## Projection Lifecycle Target

Cognitive Memory projection rebuilds need generic driver support for:

- create or ensure payload indexes,
- upsert batches with metadata,
- delete by source-like metadata filter,
- delete by projection version or embedding profile filter,
- trace provider capability and unsupported operations.

This avoids keeping every Qdrant point id in Cognitive Memory just to delete stale projections.

## SemanticCompletion Target

Embedding results should carry stable profile metadata:

- provider name,
- model id or model family,
- model/version/profile id,
- dimension,
- normalization behavior,
- optional tokenizer or max-token profile where useful.

The profile must be stable enough for projection hashes and rebuild checks. It should not depend entirely on machine-local absolute paths.

## Architecture Impact

After this bundle is implemented:

- `cognitive-memory-architecture/subbundles/03-semantic-and-rag-adapters` can implement adapters against hardened projection contracts.
- `subbundles/04-memory-taxonomy-and-projections` can persist projection profile/version metadata without guessing.
- `subbundles/05-recall-orchestrator` can enforce scoped projection recall without unsafe post-filtering.
- `subbundles/07-maf-workflow-integration` can use recall traces that include projection filter and embedding profile information.
