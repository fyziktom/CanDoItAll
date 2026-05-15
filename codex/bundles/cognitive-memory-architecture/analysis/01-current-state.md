# Current State

## Inspection Summary

The first sketch was refreshed against the live repositories on 2026-05-15. The main CodeAnalytics snapshot was `snap-20260515230800-1b0ae250`, scoped to 13 relevant CanDoItAll projects. The snapshot loaded 573 documents and included DI, EF persistence, dependency, and risk facts. Separate source inspections were performed for the RAG driver, Qdrant driver, and SemanticCompletion driver.

## CanDoItAll Main Repository

Relevant roots:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf`

Confirmed reusable foundations:

- `ModuleAssemblies.All` and `AddCanDoItAllRuntimeModules(...)` are the correct runtime registration points.
- `AppDbContextModelRegistry` discovers `IEntityTypeConfiguration<T>` types from module assemblies, so the Cognitive Memory module should not require direct `DbSet<T>` additions.
- `IStoragePlacementService` is already available for large immutable artifacts, source snapshots, reports, and context packs.
- `ISearchIndexService` is a usable lexical recall channel, but it is intentionally simple and cannot replace a cognitive recall orchestrator.
- Workbench stores `ProjectObjectRecord`, `ProjectObjectLinkRecord`, node references, bindings, view states, lifecycle events, and cross-module mutations.
- Process runtime stores rich episodic inputs: runs, step runs, assignments, work briefs, decisions, artifacts, journals, conformance observations, improvements, and workflow links.
- Workflow runtime has `IWorkflowExecutor`, `IWorkflowExecutorInvoker`, `IWorkflowRunStore`, events, artifacts, external requests, and persistent stores.

## Architectural Friction Found

- `CanDoItAll.AgentFramework.Maf` directly references multiple domain modules, including Workbench and Processes, and contains private context provider composition. This makes Cognitive Memory integration tempting to hardwire into MAF internals. That would be a bad long-term boundary.
- `WorkspaceMemoryContextProvider` is private and keyword-scored. It is a useful compatibility fallback, not a foundation for cognitive memory.
- `IProjectStructureRuntimeGateway` is agent-oriented. It can read node layout, metadata, notes, links, and assets, but it lacks a stable high-volume source snapshot contract with source hashes, layout version, storage reference, and update cursor semantics.
- Workbench schema compatibility code creates and patches SQLite tables imperatively. Cognitive Memory should not copy this pattern blindly; EF migrations/configurations should own normal module schema, with explicit bootstrap only where legacy SQLite compatibility truly requires it.
- MAF context capability handling supports local file text search, static messages, and Mem0 provider attachment, but has no general `IAgentContextContributor` or similar extension boundary.

## RAG Repository

Relevant files:

- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Abstractions\IRagDriver.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagSearchRequest.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Driver\Models\RagKnowledgeEntry.cs`
- `C:\repositories\CanDoItAll.AgentFramework.Rag\src\CanDoItAll.AgentFramework.Rag.Qdrant\QdrantRagDriver.cs`

Confirmed state:

- `IRagDriver` supports collection creation, upsert, delete by ids, and search.
- `RagSearchRequest` has query text, optional vector, limit, and min score.
- `RagKnowledgeEntry` carries text, metadata, tags, and optional vector.
- `RagDriverCapabilities` only advertises tag support.
- `QdrantRagDriver.SearchAsync` does not pass a payload filter to Qdrant.

Implication:

- V1 can use multi-collection projection and post-filtering for small slices.
- Production-scale recall needs typed filters, payload indexes, projection lifecycle, delete-by-source, and schema/version metadata in the RAG abstraction or in a Cognitive Memory adapter over the current driver.

## SemanticCompletion Repository

Relevant files:

- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Embeddings\IAgentTextEmbeddingGenerator.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Semantics\SemanticTextRanker.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Semantics\SemanticClassifier.cs`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Vectors\VectorSimilarity.cs`

Confirmed state:

- The driver provides local ONNX and hashing embedding implementations.
- The ranker embeds fixed candidate sets and ranks with cosine similarity.
- The classifier supports semantic intent classification with thresholds, margins, and guard hits.
- It does not own source manifests, canonical memory, graph relations, consolidation, projection lifecycle, or access policy.

Implication:

- SemanticCompletion should be wrapped as `ICognitiveEmbeddingProvider`, `IRecallIntentClassifier`, and optional semantic ranker/classifier utilities.
- It must not become the source of truth for memory.

## Source Evidence

- Main snapshot: `snap-20260515230800-1b0ae250`.
- RAG driver snapshot: `snap-20260515230825-87357106`.
- Qdrant driver snapshot: `snap-20260515230823-eef5104c`.
- SemanticCompletion snapshot: `snap-20260515230827-e2e62ab1`.
- Existing bundle validation initially failed because `traceability`, `shared-prompts`, `subbundles`, `reviews`, initiative directories, required inputs, phase plan, target solution, and execution report were missing.
