# Requirement Traceability

| Requirement | Primary subbundle | Validation proof |
|---|---|---|
| FR-001 Source Ingestion | `02-workbench-and-source-ingestion` | Source manifest tests and Workbench ingestion fixture. |
| FR-002 Raw Source Provenance | `01-module-foundation` | Source item hash and provenance persistence tests. |
| FR-003 Canonicalization | `04-memory-taxonomy-and-projections` | Canonical item creation with source references and confidence. |
| FR-004 Memory Taxonomy | `04-memory-taxonomy-and-projections` | Typed memory records and relation tests. |
| FR-005 Mindmap Spatial Processing | `02-workbench-and-source-ingestion` | Layout metadata and relation extraction tests. |
| FR-006 Multi-View Similarity | `01b-score-geometry-driver` + `05-recall-orchestrator` | Score geometry and recall traces cover semantic, spatial, graph, lexical, metadata, temporal, activation, source, and context dimensions. |
| FR-007 Context-Separated Relatedness | `02-workbench-and-source-ingestion` + `04-memory-taxonomy-and-projections` + `05-recall-orchestrator` + `10-cross-project-memory` | Docker production/test/local/CI fixture proves separation at source layout, relation, projection, recall, and later cross-project promotion gates. |
| FR-008 Qdrant Projection | `03-semantic-and-rag-adapters` | Projection adapter integration tests. |
| FR-009 Rebuildable Projection | `04-memory-taxonomy-and-projections` | Projection rebuild test from durable records. |
| FR-010 Recall Orchestration | `05-recall-orchestrator` | Context-pack and trace tests. |
| FR-011 Working Memory | `15-cognitive-workspace-attention-router` + `07-maf-workflow-integration` | Workspace frame, focus/inhibition, expiry, and MAF context isolation tests. |
| FR-012 Episodic Memory | `06-consolidation-engine` + `17-temporal-replay-scheduler` | Process/workflow episode extraction plus ordered episode/replay tests. |
| FR-013 Procedural Memory | `18-procedural-skill-memory-simulation` + `06-consolidation-engine` | Procedure skill graph, maturity, failure-mode, review, and consolidation handoff tests. |
| FR-014 Reflection | `06-consolidation-engine` + `17-temporal-replay-scheduler` | Reflection evidence, episode linkage, replay safety, and source-backed revision tests. |
| FR-015 Consolidation | `06-consolidation-engine` | Idempotent consolidation run and cursor tests. |
| FR-016 Human Review | `08-human-review-ui` | Review decision persistence and browser evidence. |
| FR-017 MAF Integration | `07-maf-workflow-integration` | MAF context contributor contract test. |
| FR-018 Workflow Executors | `07-maf-workflow-integration` | Executor registration and authorization tests. |
| FR-019 Distributed Idle Compute | `09-distributed-idle-compute` | Lease/hash/version acceptance and rejection tests. |
| FR-020 Auditability | `05-recall-orchestrator` | Recall, consolidation, projection, and review trace inspection. |
| FR-021 Explicit Operating Modes | `01-module-foundation` + `01a-common-drivers-helpers-and-ef-guardrails` | Strongly typed modes persisted in scans, runs, projections, traces, probing, learning, and distributed jobs. |
| FR-022 High-Volume Operations | `01a-common-drivers-helpers-and-ef-guardrails` + `02-workbench-and-source-ingestion` + `06-consolidation-engine` | Cursor, batch, byte budget, idempotency, resumability, no-tracking read queries, and bounded scan/consolidation tests. |
| FR-023 Prerequisite Boundaries | `00-prerequisite-boundary-gate` | Separate prerequisite-boundaries bundle and source-backed review. |
| FR-024 Knowledge Coverage Modeling | `12-epistemic-drive-engine` | Coverage map persistence, refresh, UI inspection, and Docker fixture. |
| FR-025 Knowledge Gap Detection | `12-epistemic-drive-engine` | Gap detection tests from recall traces, failures, stale records, contradictions, probing, and corrections. |
| FR-026 Multi-Dimensional Knowledge Need Modeling | `12-epistemic-drive-engine` | `KnowledgeNeedVector` storage and scalar-only rejection tests. |
| FR-027 Explainable Learning Proposal Generation | `12-epistemic-drive-engine` | Proposal fixture with evidence refs, coverage map, project directions, sources, risks, outputs, and acceptance criteria. |
| FR-028 Human Approval For Learning | `12-epistemic-drive-engine` | Approval/reject/snooze/scope/probing actions persisted and audited. |
| FR-029 Learning Workflow Orchestration | `12-epistemic-drive-engine` | MAF learning task executor contract and approval gate tests. |
| FR-030 Knowledge Probing Integration | `13a-probing-core-regression-calibration` + `13-interactive-memory-probing-workbench` + `12-epistemic-drive-engine` | Probing question generation, probe outcome evidence publication, regression replay, calibration, and Epistemic evidence consumption tests. |
| FR-031 Learning Outcomes | `12-epistemic-drive-engine` | Learning outcome report, draft memory/procedure records, QA findings, and source refs. |
| NFR-001 Deterministic Core | `01a-common-drivers-helpers-and-ef-guardrails` + `01b-score-geometry-driver` + `04-memory-taxonomy-and-projections` | Deterministic hashing, paging, fake providers, score geometry, and projection payload tests. |
| NFR-002 Provenance First | `01-module-foundation` | Persistence rejects memory without source evidence or explicit generated reason. |
| NFR-003 Provider Independence | `01a-common-drivers-helpers-and-ef-guardrails` + `03-semantic-and-rag-adapters` | Shared fake embedding/vector/source providers plus adapter contract tests. |
| NFR-004 Offline Capability | `03-semantic-and-rag-adapters` | Local SemanticCompletion path and no mandatory external API. |
| NFR-005 Incremental Processing | `02-workbench-and-source-ingestion` | Source cursor and content hash diff tests. |
| NFR-006 Safe Degradation | `05-recall-orchestrator` | Qdrant unavailable trace and fallback test. |
| NFR-007 Explainability | `01b-score-geometry-driver` + `05-recall-orchestrator` | Trace explains score vectors, matched shapes, scalar projections, exclusion, source, and budget decisions. |
| NFR-008 Secret Safety | `01-module-foundation` | Redaction/access-policy tests and review gate for high-risk memory. |
| NFR-009 Versioning | `04-memory-taxonomy-and-projections` | Algorithm, projection, and embedding profile version checks. |
| NFR-010 Performance | `01a-common-drivers-helpers-and-ef-guardrails` + `05-recall-orchestrator` + `06-consolidation-engine` | Bounded recall budgets, EF query-shape checks, vector allocation review, and background consolidation separation. |
| NFR-011 No Silent Truncation | `01a-common-drivers-helpers-and-ef-guardrails` + `05-recall-orchestrator` | Budget helper tests and budget exclusion trace assertions. |
| NFR-012 Idempotent Mutations | `01a-common-drivers-helpers-and-ef-guardrails` + `06-consolidation-engine` | Shared idempotency keys, leases, duplicate job, retry, and safe cursor advancement tests. |
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
| `plan/subbundles/12-epistemic-drive-engine/README.md` | Implementation-ready plan subbundle. |
| `subbundles/12-epistemic-drive-engine/README.md` | Root execution mirror with numbering note. |
| `validation/test-and-quality-plan.md` | Unit, integration, UI, and negative tests for Epistemic Drive. |

## Score Geometry Traceability Addendum

| Requirement | Primary subbundle | Validation proof |
|---|---|---|
| FR-053 Generic Score Geometry Driver | `01b-score-geometry-driver` | Score-space registry, vector snapshot, shape evaluation, scalar projection, evaluation trace, fake driver, and EF/index tests. |
| FR-054 Score Geometry Consumption | `01b-score-geometry-driver` + `05-recall-orchestrator` + `15-cognitive-workspace-attention-router` + `16-prediction-error-salience-signals` + `17-temporal-replay-scheduler` + `19-metamemory-abstention-calibration` + `12-epistemic-drive-engine` + `10-cross-project-memory` | Contract/model tests reject local scalar scoring and downstream tests prove each consumer references declared score spaces and evaluation traces. |
| NFR-034 No Scalar-Only Behavior Scoring | `01b-score-geometry-driver` | Analyzer/grep and contract tests reject behavior-affecting `FinalScore`, untyped `ScoreBreakdown`, scalar-only replay priority, and scalar-only answer confidence. |
| NFR-035 Versioned Score Spaces | `01b-score-geometry-driver` | Score vector/shape/evaluation records store score space kind, schema version, normalization profile, algorithm version, evidence refs, and missing-dimension policy. |
| NFR-036 Score Geometry Queryability And Performance | `01b-score-geometry-driver` | EF query/index proof for score components/evaluation traces and performance scan for hot score evaluation paths. |

## Execution Control Traceability Addendum

| Requirement | Primary artifact | Validation proof |
|---|---|---|
| EXC-001 Durable Phase Ledger | `checklists/cognitive-memory-implementation-control.xlsx` | `Phase Gates` contains all 24 phases with status, prerequisites, gate evidence, and downstream dependency notes. |
| EXC-002 Phase Checklist Tracking | `checklists/cognitive-memory-implementation-control.xlsx` | `Phase Acceptance Checklist` maps every phase to concrete checklist items and required proof. |
| EXC-003 Proof Path Tracking | `checklists/cognitive-memory-implementation-control.xlsx` + `reviews/01-execution-report.md` | `Validation Evidence` rows and execution-report rows agree before phase closure. |
| EXC-004 Handoff Safety | `checklists/cognitive-memory-implementation-control.xlsx` + `plan/01-phase-plan.md` | `Handoff Log` records current phase, branch/commit, blockers, downstream permission, and reopened prerequisites. |
| EXC-005 Subbundle Mirror Consistency | `subbundles/` + `plan/subbundles/` | Mirror folders remain byte-equivalent after edits; divergence blocks implementation. |
| EXC-006 Reopen Discipline | `analysis/02-assumptions-and-risks.md` + `checklists/cognitive-memory-implementation-control.xlsx` | Reopen triggers mark upstream rows `Reopened` or downstream rows `Blocked` instead of pushing weak proof downstream. |

## Score Geometry Artifact Map

| Artifact | Covers |
|---|---|
| `inputs/06-score-geometry-review-request.md` | Raw scoring review request and required outcome. |
| `analysis/08-score-geometry-architecture-review.md` | Scalar leakage findings and required repairs. |
| `architecture/26-score-geometry-driver.md` | Generic score-space/vector/shape/evaluation architecture. |
| `contracts/csharp/CognitiveMemory.ScoringContracts.cs` | Architecture-level scoring contracts. |
| `subbundles/01b-score-geometry-driver/README.md` | Implementation-ready score geometry foundation plan. |
| `validation/test-and-quality-plan.md` | Score geometry tests, EF proof, and scalar-only rejection checks. |

## Interactive Memory Probing Traceability Addendum

| Requirement | Primary subbundle | Validation proof |
|---|---|---|
| FR-032 Interactive Memory Probe Sessions | `13a-probing-core-regression-calibration` + `13-interactive-memory-probing-workbench` | Backend session CRUD/resume tests and browser evidence. |
| FR-033 Probe Turns With Recall Traces | `13a-probing-core-regression-calibration` + `13-interactive-memory-probing-workbench` | Probe turn stores recall trace/context pack and UI trace panel proof. |
| FR-034 Probe Feedback And User Corrections | `13a-probing-core-regression-calibration` + `13-interactive-memory-probing-workbench` | Feedback action tests, correction risk classification, and correction review item proof. |
| FR-035 Probe Question Generation | `13-interactive-memory-probing-workbench` + `12-epistemic-drive-engine` | Question queue generated from gaps/stale/contradiction/context separation evidence. |
| FR-036 Probe Evidence Integration | `13a-probing-core-regression-calibration` + `12-epistemic-drive-engine` | Probe-derived `KnowledgeGapEvidenceRef` consumed by coverage/gap scan. |
| FR-037 Memory Regression Tests From Probe Failures | `13a-probing-core-regression-calibration` | Draft/active regression test replay with recall trace link. |
| FR-038 Confidence Calibration From Probing | `13a-probing-core-regression-calibration` | Calibration records for overconfidence/wrong-scope/missing-source cases. |
| NFR-020 No Direct Truth Mutation From Probing | `13a-probing-core-regression-calibration` | Negative tests prove correction cannot update active memory directly. |
| NFR-021 Probe Traceability | `13a-probing-core-regression-calibration` + `13-interactive-memory-probing-workbench` | End-to-end IDs link session, turn, recall trace, feedback, review, regression, evidence, and UI actions. |
| NFR-022 Probe Privacy And Redaction | `13a-probing-core-regression-calibration` + `13-interactive-memory-probing-workbench` | Secret/access/redaction tests and browser proof of redaction warnings. |
| NFR-023 Probe Replayability | `13a-probing-core-regression-calibration` | Regression replay stores input, access context, evaluator profile, result, and recall trace. |
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
| `subbundles/13a-probing-core-regression-calibration/README.md` | Backend probe sessions, feedback evidence, regression replay, and confidence calibration. |
| `subbundles/13-interactive-memory-probing-workbench/README.md` | Root execution plan. |
| `validation/probing-test-matrix.md` | Functional, negative, and browser validation matrix. |

## Neuro-Cognitive Patch Traceability Addendum

| Requirement | Primary subbundle | Validation proof |
|---|---|---|
| FR-039 Cognitive Workspace Frames | `15-cognitive-workspace-attention-router` | Workspace lifecycle, focus slot, inhibition, context budget, expiry, and trace-link tests. |
| FR-040 Attention Router | `15-cognitive-workspace-attention-router` | Route decision tests for recall, answer, clarification, source audit, probe, review, learning proposal, replay, and abstention. |
| FR-041 Claim/Evidence/Belief Ledger | `14-neuro-foundation-claim-evidence-ledger` | Claim support/attack, belief-state, contested claim, and contradiction visibility tests. |
| FR-042 Evidence Anchors | `14-neuro-foundation-claim-evidence-ledger` | Source item, structured path, text span, quote hash, trust, redaction, and source hash/version tests. |
| FR-043 Memory Mutation Authority | `14-neuro-foundation-claim-evidence-ledger` | Idempotency, stale version, review-required, audit, and projection-invalidation tests. |
| FR-044 Schema, Entity, And Context Binding | `14-neuro-foundation-claim-evidence-ledger` + `15-cognitive-workspace-attention-router` | Entity alias and Docker context-boundary tests before merge/recall/procedure execution. |
| FR-045 Prediction Expectations And Prediction Errors | `16-prediction-error-salience-signals` | Expected-vs-observed tests from probe, workflow, QA, stale source, and procedure execution fixtures. |
| FR-046 Salience Signal Ledger | `16-prediction-error-salience-signals` | Signal vector preservation, policy safety, and auditability tests. |
| FR-047 Temporal Episodic Memory | `17-temporal-replay-scheduler` | Episode order, actor, decision, artifact, outcome, prediction-error, claim, and procedure link tests. |
| FR-048 Replay/Rehearsal Scheduler | `17-temporal-replay-scheduler` | Replay priority, replay safety, context-boundary replay, and distributed replay rejection tests. |
| FR-049 Procedural Skill Memory | `18-procedural-skill-memory-simulation` | Procedure skill precondition/step/postcondition/failure-mode/maturity/validation evidence tests. |
| FR-050 Simulation Sandbox | `18-procedural-skill-memory-simulation` | Speculation labeling, source/review gating, and cross-project analogy access-policy tests. |
| FR-051 Metamemory Answer Gate | `19-metamemory-abstention-calibration` | Answer/warn/clarify/source-audit/probe/review/learning/abstain decision tests. |
| FR-052 Workspace-Aware Probing | `15-cognitive-workspace-attention-router` + `16-prediction-error-salience-signals` + `13a-probing-core-regression-calibration` + `19-metamemory-abstention-calibration` | Probe workspace, prediction-error/signal publication, claim correction, and answer-gate trace tests. |
| NFR-025 No Direct Public Upsert For Authoritative Memory | `14-neuro-foundation-claim-evidence-ledger` | Public contract review and mutation-authority negative tests. |
| NFR-026 No Silent Claim Merge | `14-neuro-foundation-claim-evidence-ledger` | Claim/context/evidence mismatch tests prevent automatic merge. |
| NFR-027 No Scalar-Only Salience | `01b-score-geometry-driver` + `16-prediction-error-salience-signals` | Signal dimensions, score vector metadata, and scalar projection tests. |
| NFR-028 Explainable Attention | `01b-score-geometry-driver` + `15-cognitive-workspace-attention-router` | Attention score vector, matched shape, scalar projection, and reason trace tests. |
| NFR-029 Replay Safety | `17-temporal-replay-scheduler` | Replay cannot directly promote truth or bypass mutation authority. |
| NFR-030 Speculation Labeling | `18-procedural-skill-memory-simulation` | Simulation output cannot become active procedure without review and validation. |
| NFR-031 Answer Abstention Safety | `19-metamemory-abstention-calibration` | Abstention/clarification/source-audit tests for unsafe fluent answers. |
| NFR-032 Context Boundary Safety | `14-neuro-foundation-claim-evidence-ledger` + `15-cognitive-workspace-attention-router` + `19-metamemory-abstention-calibration` | Context boundaries evaluated before answer rendering and procedure execution. |
| NFR-033 Auditability Of Cognitive Signals | `16-prediction-error-salience-signals` + `19-metamemory-abstention-calibration` | Signals, prediction errors, attention decisions, and answer-gate decisions cite evidence, actor, time, and algorithm/profile version. |

## Neuro-Cognitive Artifact Map

| Artifact | Covers |
|---|---|
| `architecture/17-neuro-cognitive-integration-layer.md` | Overall cognitive control layer and integration responsibilities. |
| `architecture/18-cognitive-workspace-and-attention-router.md` | Workspace frames, focus slots, inhibited candidates, and attention routing. |
| `architecture/19-prediction-error-salience-signal-ledger.md` | Prediction errors and signal vectors. |
| `architecture/20-claim-evidence-belief-ledger.md` | Atomic claims, evidence anchors, belief state, and mutation authority. |
| `architecture/21-schema-entity-context-binding.md` | Entity registry, aliases, context frames, and context boundaries. |
| `architecture/22-temporal-episodic-memory-and-replay.md` | Episodes, steps, causality, and replay scheduler. |
| `architecture/23-procedural-skill-memory-and-simulation.md` | Procedure skills, failure modes, maturity, and simulation sandbox. |
| `architecture/24-metamemory-confidence-and-abstention.md` | Answer gate and abstention policy. |
| `contracts/csharp/CognitiveMemory.NeuroPatchContracts.cs` | Architecture-level contracts for neuro-cognitive additions. |
| `diagrams/14-neuro-cognitive-overview.mmd` through `diagrams/17-replay-and-procedural-memory-flow.mmd` | Neuro-cognitive flow and relationship diagrams. |
| `subbundles/14-neuro-foundation-claim-evidence-ledger/README.md` through `subbundles/20-architecture-integration-closure/README.md` | Execution workstreams and closure gate. |
| `validation/neuro-patch-test-plan.md` | Patch-specific test groups and Docker golden scenario. |
