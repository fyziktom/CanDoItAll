# Integration Boundaries

## Source Of Truth

The authoritative Cognitive Memory state is stored in the active `AppDbContext` profile. The durable truth surfaces are source manifests/items, evidence anchors, canonical memory records, claims, source links, claim/evidence links, mutation commands, review decisions, recall traces, score traces, and advanced control records.

RAG, Qdrant, SemanticCompletion, UI cards, context packs, and MAF prompt messages are projections or consumers. They must not be treated as the memory itself.

## Boundary Rules

| Boundary | Current implementation | Rule |
| --- | --- | --- |
| Workbench/project structure | `IProjectStructureSourceSnapshotProvider` from Workbench | Read source snapshots only. Do not let Cognitive Memory mutate project structure. |
| Process runtime | `IProcessRuntimeEvidenceSourceProvider` from Processes | Read process evidence. Do not make process runtime depend on memory persistence internals. |
| Workflow runtime | `IWorkflowRuntimeEvidenceSourceProvider` from AgentFramework module | Read workflow evidence. Do not make workflow state a private memory schema. |
| External files/web links | `ICognitiveMemoryExternalSourceIngestionService` | Store extracted text as source material with provenance and evidence. Do not treat upload text as approved memory. |
| SemanticCompletion | `ICognitiveMemoryEmbeddingProvider`, `ICognitiveMemorySemanticRanker`, `ICognitiveMemorySemanticClassifier<T>` | Use as semantic utilities only. They do not own canonicalization or truth. |
| RAG/Qdrant | `ICognitiveMemoryProjectionAdapter` | Rebuildable projection. Deleting or rebuilding projection points must not delete durable memory. |
| AgentFramework MAF | `IAgentContextContributor` | Read rendered context packs into agent requests. MAF does not own durable memory. |
| Review UI | `ICognitiveMemoryReviewUiService` | Applies explicit operator decisions. It should remain orchestration over services, not a second memory implementation. |

## Mutability Policy

Canonical memory should change only through governed paths:

1. Source ingestion persists source truth and evidence.
2. Consolidation creates candidates and mutation commands.
3. The candidate applicator materializes memory records and claims when the candidate is accepted or approved.
4. Review decisions can approve/reject/defer/request changes.
5. Probe feedback, answer gates, professor reviews, prediction errors, learning proposals, and distributed worker results create reviewable signals/control records. They should not directly rewrite canonical truth.

## Provider Behavior

The module registers unavailable adapter implementations when embedding, semantic ranking, or RAG services are not present. Current recall behavior records skipped/unavailable vector stages and continues through lexical/source/workspace/signal/graph channels. That is acceptable for alpha development and local smoke work, but beta should make fail/skip policy explicit for process-critical agent runs.

## Database Profiles

Cognitive Memory is provider-aware:

- SQLite migrations exist for local profile smoke work.
- PostgreSQL migrations exist for multi-cycle validation and expected heavier agent/process workloads.
- In-memory providers are used by tests.

PostgreSQL should be the default for realistic multi-cycle memory validation because source items, traces, candidates, review records, and agent/process activity can grow quickly.

