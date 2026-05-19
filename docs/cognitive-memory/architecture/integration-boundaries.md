# Integration Boundaries

## Source Of Truth

The authoritative Cognitive Memory state is stored in the active `AppDbContext` profile. The durable truth surfaces are source manifests/items, evidence anchors, canonical memory records, claims, source links, claim/evidence links, mutation commands, review decisions, recall traces, score traces, and advanced control records.

RAG, Qdrant, SemanticCompletion/local hashing embeddings, UI cards, context packs, and MAF prompt messages are projections or consumers. They must not be treated as the memory itself.

## Boundary Rules

| Boundary | Current implementation | Rule |
| --- | --- | --- |
| Workbench/project structure | `IProjectStructureSourceSnapshotProvider` from Workbench | Read source snapshots only. Do not let Cognitive Memory mutate project structure. |
| Process runtime | `IProcessRuntimeEvidenceSourceProvider` from Processes | Read process evidence. Do not make process runtime depend on memory persistence internals. |
| Workflow runtime | `IWorkflowRuntimeEvidenceSourceProvider` from AgentFramework module | Read workflow evidence. Do not make workflow state a private memory schema. |
| External files/web links | `ICognitiveMemoryExternalSourceIngestionService` | Store extracted text as source material with provenance and evidence. Do not treat upload text as approved memory. |
| SemanticCompletion | `ICognitiveMemoryEmbeddingProvider`, `ICognitiveMemorySemanticRanker`, `ICognitiveMemorySemanticClassifier<T>` | Use as semantic utilities only. They do not own canonicalization or truth. |
| RAG/Qdrant | `ICognitiveMemoryProjectionAdapter`, `ICognitiveMemoryProjectionRebuildService` | Rebuildable projection. Deleting or rebuilding projection points must not delete durable memory. Projection rebuild must load durable source/evidence/claim inputs rather than invent payloads. Missing durable records can be projected only when explicit collection/profile/embedding/provider settings are supplied. |
| AgentFramework MAF | `IAgentContextContributor`, `CognitiveMemoryAgentContextPackage` | Read rendered agent-facing context into agent requests. MAF does not own durable memory or diagnostic recall payloads. |
| Operational automation | `ICognitiveMemoryScheduledAutomationRunner` | Explicitly runs configured ingestion/consolidation when schedule mode permits. It is not a hidden background mutation path. |
| Review UI | `ICognitiveMemoryReviewUiService` | Applies explicit operator decisions. It should remain orchestration over services, not a second memory implementation. |

## Mutability Policy

Canonical memory should change only through governed paths:

1. Source ingestion persists source truth and evidence.
2. Consolidation creates candidates and mutation commands.
3. The candidate applicator materializes memory records and claims when the candidate is accepted or approved.
4. Review decisions can approve/reject/defer/request changes.
5. Probe feedback, answer gates, professor reviews, prediction errors, learning proposals, and distributed worker results create reviewable signals/control records. They should not directly rewrite canonical truth.
6. Projection rebuild updates projection rows and provider points only; it must not create canonical memory.
7. Scheduled automation runs source ingestion and consolidation through existing governed services; it must not bypass source truth, mutation authority, or review policy.

## Provider Behavior

The module registers unavailable adapter implementations when embedding, semantic ranking, or RAG services are not present. When Qdrant is enabled, composition also registers deterministic local hashing embeddings and Cognitive Memory projection defaults for local Docker validation. Current recall behavior records skipped/unavailable vector stages and continues through lexical/source/workspace/signal/graph channels when vector settings or providers are absent.

MAF contribution now has an explicit policy boundary: governed process automation, auto-approved non-interactive runs, and A2A endpoint mode fail when required memory context is unavailable. Interactive chat still skips unavailable optional memory context with trace metadata.

## Database Profiles

Cognitive Memory is provider-aware:

- SQLite migrations exist for local profile smoke work.
- PostgreSQL migrations exist for multi-cycle validation and expected heavier agent/process workloads.
- In-memory providers are used by tests.

PostgreSQL should be the default for realistic multi-cycle memory validation because source items, traces, candidates, review records, and agent/process activity can grow quickly.

