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
| FR-024 Knowledge Coverage Modeling | `12-epistemic-drive-engine` | Coverage map persistence, refresh, UI inspection, and Docker fixture. |
| FR-025 Knowledge Gap Detection | `12-epistemic-drive-engine` | Gap detection tests from recall traces, failures, stale records, contradictions, probing, and corrections. |
| FR-026 Multi-Dimensional Knowledge Need Modeling | `12-epistemic-drive-engine` | `KnowledgeNeedVector` storage and scalar-only rejection tests. |
| FR-027 Explainable Learning Proposal Generation | `12-epistemic-drive-engine` | Proposal fixture with evidence refs, coverage map, project directions, sources, risks, outputs, and acceptance criteria. |
| FR-028 Human Approval For Learning | `12-epistemic-drive-engine` | Approval/reject/snooze/scope/probing actions persisted and audited. |
| FR-029 Learning Workflow Orchestration | `12-epistemic-drive-engine` | MAF learning task executor contract and approval gate tests. |
| FR-030 Knowledge Probing Integration | `12-epistemic-drive-engine` | Probing question generation and probing outcome evidence tests. |
| FR-031 Learning Outcomes | `12-epistemic-drive-engine` | Learning outcome report, draft memory/procedure records, QA findings, and source refs. |
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
| NFR-014 No Scalar-Only Epistemic Scoring | `12-epistemic-drive-engine` | Tests and review checklist require preserved vector dimensions and evidence. |
| NFR-015 Learning Approval Safety | `12-epistemic-drive-engine` | External study and high-impact update approval-gate tests. |
| NFR-016 Source-Grounded Learning Outputs | `12-epistemic-drive-engine` | Persistence rejects learning-derived canonical/procedure records without source refs. |
| NFR-017 Auditable Learning Decisions | `12-epistemic-drive-engine` | Audit events for proposal decisions, learning task lifecycle, and outcome promotion. |
| NFR-018 Idempotent And Resumable Learning Processing | `12-epistemic-drive-engine` | Input hash, retry, duplicate proposal, and resume tests. |
| NFR-019 Projection Boundary Preservation | `12-epistemic-drive-engine` | Qdrant outage and rebuild tests prove proposals/outcomes are durable outside projections. |

## Epistemic Drive Artifact Map

| Artifact | Covers |
|---|---|
| `architecture/14-epistemic-drive-and-learning-orchestration.md` | FR-024 through FR-031 and NFR-014 through NFR-019. |
| `contracts/csharp/EpistemicDriveContracts.cs` | Architecture-level interfaces/models for coverage, gaps, vectors, proposals, tasks, and outcomes. |
| `diagrams/10-epistemic-drive-flow.mmd` | Evidence-to-learning lifecycle. |
| `plan/subbundles/11-epistemic-drive-engine/README.md` | Implementation-ready plan subbundle. |
| `subbundles/12-epistemic-drive-engine/README.md` | Root execution mirror with numbering note. |
| `validation/test-and-quality-plan.md` | Unit, integration, UI, and negative tests for Epistemic Drive. |

## Interactive Memory Probing Traceability Addendum

| Requirement | Primary subbundle | Validation proof |
|---|---|---|
| FR-032 Interactive Memory Probe Sessions | `13-interactive-memory-probing-workbench` | Session CRUD/resume tests and browser evidence. |
| FR-033 Probe Turns With Recall Traces | `13-interactive-memory-probing-workbench` | Probe turn stores recall trace/context pack and UI trace panel proof. |
| FR-034 Probe Feedback And User Corrections | `13-interactive-memory-probing-workbench` | Feedback action tests and correction review item proof. |
| FR-035 Probe Question Generation | `13-interactive-memory-probing-workbench` + `12-epistemic-drive-engine` | Question queue generated from gaps/stale/contradiction/context separation evidence. |
| FR-036 Probe Evidence Integration | `13-interactive-memory-probing-workbench` + `12-epistemic-drive-engine` | Probe-derived `KnowledgeGapEvidenceRef` consumed by coverage/gap scan. |
| FR-037 Memory Regression Tests From Probe Failures | `13-interactive-memory-probing-workbench` | Draft/active regression test replay with recall trace link. |
| FR-038 Confidence Calibration From Probing | `13-interactive-memory-probing-workbench` | Calibration records for overconfidence/wrong-scope/missing-source cases. |
| NFR-020 No Direct Truth Mutation From Probing | `13-interactive-memory-probing-workbench` | Negative tests prove correction cannot update active memory directly. |
| NFR-021 Probe Traceability | `13-interactive-memory-probing-workbench` | End-to-end IDs link session, turn, recall trace, feedback, review, regression, and evidence. |
| NFR-022 Probe Privacy And Redaction | `13-interactive-memory-probing-workbench` | Secret/access/redaction tests. |
| NFR-023 Probe Replayability | `13-interactive-memory-probing-workbench` | Regression replay stores input, access context, evaluator profile, result, and recall trace. |
| NFR-024 Probe Diversity With Control | `13-interactive-memory-probing-workbench` | Question generator preserves reason/evidence/random seed metadata. |

## Interactive Probing Artifact Map

| Artifact | Covers |
|---|---|
| `architecture/15-interactive-memory-probing.md` | Probe lifecycle, modes, feedback, UI, safety, MAF/workflow integration. |
| `architecture/16-probing-regression-and-calibration-loop.md` | Regression tests, replay, evaluator modes, calibration records. |
| `contracts/csharp/InteractiveMemoryProbingContracts.cs` | Architecture-level probing interfaces and DTOs. |
| `diagrams/11-interactive-memory-probing-flow.mmd` | Evidence-preserving dialogue loop. |
| `diagrams/12-probing-session-sequence.mmd` | User/UI/service/recall/review/Epistemic sequence. |
| `diagrams/13-probing-to-epistemic-drive-loop.mmd` | Probe evidence feedback into Epistemic Drive. |
| `subbundles/13-interactive-memory-probing-workbench/README.md` | Root execution plan. |
| `validation/probing-test-matrix.md` | Functional, negative, and browser validation matrix. |
