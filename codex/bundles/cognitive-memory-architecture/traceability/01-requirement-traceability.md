# Requirement Traceability

| Requirement | Primary subbundle | Validation proof |
|---|---|---|
| FR-001 Source Ingestion | `02-workbench-and-source-ingestion` | Source manifest tests and Workbench ingestion fixture. |
| FR-002 Raw Source Provenance | `01-module-foundation` | Source item hash and provenance persistence tests. |
| FR-003 Canonicalization | `04-memory-taxonomy-and-projections` | Canonical item creation with source references and confidence. |
| FR-004 Memory Taxonomy | `04-memory-taxonomy-and-projections` | Typed memory records and relation tests. |
| FR-005 Mindmap Spatial Processing | `02-workbench-and-source-ingestion` | Layout metadata and relation extraction tests. |
| FR-006 Multi-View Similarity | `05-recall-orchestrator` | Recall scoring trace covers semantic, spatial, graph, lexical, metadata, temporal, and activation signals. |
| FR-007 Context-Separated Relatedness | `10-cross-project-memory` | Golden dataset with similar but intentionally separated project records. |
| FR-008 Qdrant Projection | `03-semantic-and-rag-adapters` | Projection adapter integration tests. |
| FR-009 Rebuildable Projection | `04-memory-taxonomy-and-projections` | Projection rebuild test from durable records. |
| FR-010 Recall Orchestration | `05-recall-orchestrator` | Context-pack and trace tests. |
| FR-011 Working Memory | `07-maf-workflow-integration` | Workflow/agent run context isolation test. |
| FR-012 Episodic Memory | `06-consolidation-engine` | Process/workflow episode extraction test. |
| FR-013 Procedural Memory | `06-consolidation-engine` | Procedure mining and review handoff test. |
| FR-014 Reflection | `06-consolidation-engine` | Reflection record creation with evidence. |
| FR-015 Consolidation | `06-consolidation-engine` | Idempotent consolidation run and cursor tests. |
| FR-016 Human Review | `08-human-review-ui` | Review decision persistence and browser evidence. |
| FR-017 MAF Integration | `07-maf-workflow-integration` | MAF context contributor contract test. |
| FR-018 Workflow Executors | `07-maf-workflow-integration` | Executor registration and authorization tests. |
| FR-019 Distributed Idle Compute | `09-distributed-idle-compute` | Lease/hash/version acceptance and rejection tests. |
| FR-020 Auditability | `05-recall-orchestrator` | Recall, consolidation, projection, and review trace inspection. |
| FR-021 Explicit Operating Modes | `01-module-foundation` | Strongly typed modes persisted in scans, runs, projections, and traces. |
| FR-022 High-Volume Operations | `02-workbench-and-source-ingestion` | Cursor, batch, idempotency, and resumability tests. |
| FR-023 Prerequisite Boundaries | `00-prerequisite-boundary-gate` | Separate prerequisite-boundaries bundle and source-backed review. |
| NFR-001 Deterministic Core | `04-memory-taxonomy-and-projections` | Deterministic hashing/scoring tests. |
| NFR-002 Provenance First | `01-module-foundation` | Persistence rejects memory without source evidence or explicit generated reason. |
| NFR-003 Provider Independence | `03-semantic-and-rag-adapters` | Fake embedding and fake RAG driver tests. |
| NFR-004 Offline Capability | `03-semantic-and-rag-adapters` | Local SemanticCompletion path and no mandatory external API. |
| NFR-005 Incremental Processing | `02-workbench-and-source-ingestion` | Source cursor and content hash diff tests. |
| NFR-006 Safe Degradation | `05-recall-orchestrator` | Qdrant unavailable trace and fallback test. |
| NFR-007 Explainability | `05-recall-orchestrator` | Trace explains score, exclusion, source, and budget decisions. |
| NFR-008 Secret Safety | `01-module-foundation` | Redaction/access-policy tests and review gate for high-risk memory. |
| NFR-009 Versioning | `04-memory-taxonomy-and-projections` | Algorithm, projection, and embedding profile version checks. |
| NFR-010 Performance | `05-recall-orchestrator` | Bounded recall budgets and background consolidation separation. |
| NFR-011 No Silent Truncation | `05-recall-orchestrator` | Budget exclusion trace assertions. |
| NFR-012 Idempotent Mutations | `06-consolidation-engine` | Duplicate job and retry tests. |
| NFR-013 Boundary Stability | `00-prerequisite-boundary-gate` | Dependency review against MAF, Workbench, Process, Workflow, RAG, and SemanticCompletion boundaries. |
