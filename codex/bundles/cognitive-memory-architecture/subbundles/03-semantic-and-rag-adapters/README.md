# 03 Semantic And RAG Adapters

## Status

- Ready after module foundation.

## Objective

- Wrap the existing SemanticCompletion and RAG repositories behind Cognitive Memory projection and semantic utility contracts.

## Covered Inputs

- Requirements FR-006, FR-008, FR-009, NFR-003, NFR-004, NFR-006, and NFR-009.
- Source audit of RAG and SemanticCompletion capabilities.

## Prerequisites

- `01-module-foundation` must define provider-neutral projection and embedding abstractions.
- `02-workbench-and-source-ingestion` should supply source items for end-to-end projection tests.

## Exact Source References

- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Abstractions\IRagDriver.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagSearchRequest.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagKnowledgeEntry.cs
- C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Qdrant\QdrantRagDriver.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\IAgentTextEmbeddingGenerator.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Semantics\SemanticTextRanker.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Semantics\SemanticClassifier.cs

## Deliverables

- Cognitive embedding adapter over SemanticCompletion.
- Recall intent classifier/ranker adapter.
- Projection adapter over `IRagDriver`.
- RAG extension plan for typed filters, payload indexes, delete-by-source, and projection metadata.

## Dependency Impact

- Cognitive Memory depends on generic driver contracts.
- Qdrant remains a replaceable projection backend.
- Filter and lifecycle extensions may require changes in the RAG repo, but they must remain generic.

## Validation Depth

- Unit tests with deterministic fake embeddings.
- Integration tests with Qdrant where available.
- Failure tests for Qdrant and embedding provider unavailability.

## Implementation Steps

- Add provider-neutral adapter contracts.
- Implement SemanticCompletion adapter.
- Implement RAG projection adapter.
- Define typed filter and delete-by-source requirements for RAG driver evolution.

## Do Not Do

- Do not store canonical memory only in Qdrant.
- Do not put Cognitive Memory-specific semantics into the generic RAG driver.
- Do not silently fall back to a lower-quality embedding provider unless the active mode permits it and the trace records it.

## Acceptance Checklist

- Projections are rebuildable from durable memory.
- Search failures are visible in recall/projection traces.
- Adapter tests do not require external API access.

## Proof Required

- Adapter unit tests.
- Optional Qdrant integration test evidence.
- Projection failure and rebuild test evidence.

## Browser Validation Logging

- No browser proof is required for adapter-only work.
- Projection health browser proof belongs to UI subbundles.

## Progression Gate

- Proceed to taxonomy/projection modeling only after adapter boundaries and failure behavior are clear.

## Suggested Agent Prompt

- Implement Cognitive Memory adapters over the existing SemanticCompletion and RAG contracts without making either repository the memory source of truth.
