# Acceptance Criteria

## V1 Foundation

- [ ] A new Cognitive Memory module can be registered in the main solution.
- [ ] Shared deterministic drivers, helpers, builders, clocks, id providers, policy fakes, source-snapshot fakes, embedding fakes, and vector/search fakes are available before downstream implementation starts.
- [ ] EF migrations/models exist for source manifests, source items, memory items, relations, projections, recall traces, consolidation runs, and review items.
- [ ] Query-relevant references, statuses, relationships, review state, recall candidates, projection state, probe state, and regression-test links are relational and indexed; JSON payloads are supplemental, versioned, bounded, and non-authoritative.
- [ ] Growing list/query contracts use paging or cursor semantics and return DTO projections for read-only paths.
- [ ] Workbench project objects can be ingested as source items.
- [ ] Process/workflow run records can be ingested as episodic sources.
- [ ] Every created memory item has source references and content hash.
- [ ] Memory records can be listed and inspected in UI/API.

## Mindmap Processing

- [ ] Ingestion captures title, subtitle, notes, object type, parent, metadata, links, X/Y, and optional Z.
- [ ] The system can compute spatial features.
- [ ] The system can compute graph features.
- [ ] The system can compute semantic embeddings using the existing semantic provider.
- [ ] The system can detect semantically similar but spatially/graph-separated records.
- [ ] Atomic node projections are generated.
- [ ] Local cluster projections are generated.
- [ ] Project-level canonical topic projections are generated.

## Recall

- [ ] Recall can run without Qdrant using lexical/graph fallback.
- [ ] Recall can run with Qdrant when available.
- [ ] Recall returns a context pack, not only raw chunks.
- [ ] Recall records a trace with candidate score vectors, matched/inhibited shapes, scalar projections, and selected items.
- [ ] Recall supports intent/scope such as implementation, architecture, test, procedure, decision, history.
- [ ] Recall can distinguish production deployment from test/simulation deployment when both use Docker.

## Consolidation

- [ ] Consolidation run detects changed sources by hash.
- [ ] Consolidation generates or updates canonical records.
- [ ] Consolidation proposes relations and review items.
- [ ] Consolidation can update Qdrant projections.
- [ ] Consolidation writes a run report.
- [ ] Consolidation is schedulable through existing automation/Quartz infrastructure.

## MAF and Workflow Integration

- [ ] A MAF context provider can inject a recall context pack into an agent run.
- [ ] Memory recall is available as a workflow executor.
- [ ] Memory consolidation is available as a workflow executor.
- [ ] Memory reflection can be triggered after workflow/process completion.
- [ ] Recall context respects access and redaction policy.

## Qdrant Projection

- [ ] Projection payload contains project id, source refs, memory type, projection type, validation state, algorithm versions, and tags.
- [ ] Projections can be deleted/rebuilt by source hash or memory item id.
- [ ] Searches can be filtered by project, memory type, scope, validation state, and tags.
- [ ] Projection status is visible in the memory item detail.

## Human Review

- [ ] Proposed merges can be approved/rejected.
- [ ] Contradictions can be resolved or marked as accepted ambiguity.
- [ ] High-risk procedures can be human-approved before becoming active.
- [ ] Review decisions create audit records.

## Distributed Idle Compute

- [ ] A coordinator can issue deterministic job packets.
- [ ] A worker can return output with job/input/output hashes.
- [ ] The coordinator validates and accepts/rejects worker output.
- [ ] Workers cannot directly mutate memory tables or Qdrant.

## Epistemic Drive And Learning

- [ ] Knowledge regions and subregions can be stored and inspected.
- [ ] Knowledge coverage maps preserve coverage, confidence, staleness, risk, source count, open question count, and contradiction pressure.
- [ ] Knowledge gaps can be created from recall traces, workflow/process failures, user corrections, stale records, contradictions, weak source coverage, probing failures, and project direction relevance.
- [ ] `KnowledgeNeedVector` persists all required dimensions and is not replaced by a scalar priority.
- [ ] Candidate selection records Pareto/category/ROI metadata and evidence refs.
- [ ] Learning proposals explain why this topic, why now, weak subareas, project direction intersections, suggested sources, source trust, expected outputs, risks, and approval needs.
- [ ] Human review supports approve, reject, snooze, narrow scope, expand scope, add source, request probing, convert to bundle, and assign actions.
- [ ] Approved learning tasks use only approved source scope.
- [ ] Learning outputs remain draft until QA/human validation where required.
- [ ] Every learning-derived canonical/procedure record has source refs.
- [ ] Probing can be requested before learning and used after learning to validate improvement.
- [ ] Qdrant/search projections refresh only after durable records exist.

## Quality

- [ ] Unit tests cover score geometry, activation, source hashing, relation detection, and projection mapping.
- [ ] Unit and integration tests use the shared deterministic drivers/helpers rather than ad hoc provider stubs.
- [ ] Integration tests cover ingestion -> canonicalization -> projection -> recall.
- [ ] Persistence tests prove no-tracking read paths, DTO projections, expected index coverage, no client-side evaluation, and bounded command counts for recall, projection, source, review, probing, and consolidation queries.
- [ ] Negative tests cover secret redaction, stale data, contradictions, rejected merges, and Qdrant outage fallback.
- [ ] UI tests cover review queue and recall trace inspection.
- [ ] Hot-path code introduced by each phase passes the .NET performance scan for sync-over-async, unbounded materialization, avoidable vector copies, uncached serialization options, missing `StringComparison`, accidental regex allocation, and LINQ-heavy loops.
- [ ] Tests reject scalar-only Epistemic Drive scoring.
- [ ] Tests reject scalar-only recall ranking, attention routing, belief state, replay priority, probing assessment, answer confidence, and cross-project promotion.
- [ ] Negative tests cover unapproved external study, missing learning source refs, high-risk draft promotion, duplicate proposal creation, probing failure handling, and Qdrant outage during proposal generation.

## Score Geometry

- [ ] Score spaces are strongly typed and versioned for recall, attention, belief, salience, replay, probe assessment, answer gate, Epistemic need, mindmap similarity, activation, procedure maturity, and cross-project promotion.
- [ ] Score vector snapshots preserve normalized components, confidence, evidence refs, schema version, normalization profile, algorithm version, calculated time, and input hash.
- [ ] Score shapes support focus regions, context-boundary inhibition, abstention envelopes, replay urgency, cross-project promotion eligibility, and Epistemic weak-region selection.
- [ ] Scalar projections are marked as display, sorting, queue ordering, or tie-breaker only and can be reproduced from vector/shape traces.
- [ ] Missing required score dimensions are explicit and can block or warn according to score-space policy.
- [ ] Qdrant vector similarity is treated as one score dimension, never the final recall rank.
- [ ] Docker production/test/local/CI fixtures prove high semantic similarity can still be inhibited by context-separation shapes.

## Interactive Memory Probing

- [ ] The backend probing core can start/list/resume/close sessions, record turns, attach recall traces, capture feedback, create findings, create regression candidates, and produce calibration records without requiring the UI workbench.
- [ ] A project-scoped probe session can be started, listed, resumed, and closed.
- [ ] A manual user question creates a probe turn with recall trace id, context pack id, answer metadata, source refs, warnings, and findings.
- [ ] The Dialogue Workbench shows answer, trace, selected/excluded candidates, source refs, confidence, staleness, contradiction, and access warnings.
- [ ] The Dialogue Workbench consumes backend probing contracts only; it does not own durable probe truth, correction gating, regression execution, or calibration policy.
- [ ] Users can confirm, correct, mark missing, mark wrong scope, request source, create review item, create regression test, or request learning proposal.
- [ ] Corrections create evidence and review candidates, not direct active memory mutations.
- [ ] High-risk corrections require human review before affecting active memory.
- [ ] Epistemic Drive can generate probe questions from weak regions and consume probe outcomes as evidence.
- [ ] Failed or important probe turns can create draft memory regression tests.
- [ ] Regression tests can replay recall and store pass/fail results linked to recall traces.
- [ ] Confidence calibration records identify overconfident incorrect answers and wrong-scope answers.
- [ ] Docker context-separation probes detect production/test/local/CI conflation.
- [ ] Probe transcripts obey access/redaction policy and do not leak secrets to external providers.

## Neuro-Cognitive Foundation

- [ ] Evidence anchors store source id, storage locator, structured path or text span where available, quote hash where applicable, trust level, redaction state, and source version/hash.
- [ ] A memory item can compose multiple atomic claims with different belief states.
- [ ] Claim support, attack, qualification, supersession, and scope narrowing are represented explicitly.
- [ ] Direct public upsert operations are not the authoritative write boundary.
- [ ] Mutation commands are idempotent, concurrency-aware, audited, evidence-backed, and projection-invalidating.
- [ ] Entity alias resolution and context frames run before semantic merge, claim promotion, recall rendering, and procedure execution.
- [ ] Production/test/local/CI Docker contexts are related but not substitutable.

## Cognitive Workspace And Attention

- [ ] Recall/probe/MAF traces can show workspace frame id, focus slots, goal stack, open questions, inhibited candidates, and attention decision.
- [ ] The attention router can choose recall, answer from workspace, clarification, source audit, probe, review, learning proposal, replay, or abstention.
- [ ] Inhibited candidates preserve reasons such as context boundary, redaction, staleness, contradiction, or budget pressure.
- [ ] Workspace frames expire by default and do not become source truth unless persisted as governed episodic input.

## Prediction Error And Salience

- [ ] Important probe, workflow, procedure, QA, and high-risk answer paths can record prediction expectation and observed mismatch.
- [ ] Salience signals preserve dimensions such as novelty, surprise, risk, usefulness, reward, rework cost, contradiction pressure, user interest, staleness pressure, source weakness, and calibration risk.
- [ ] Signals can affect activation, replay, attention, Epistemic Drive, and calibration, but cannot create truth or bypass policy.

## Temporal Replay And Procedural Skill

- [ ] Episodes preserve ordered steps, actors, decisions, artifacts, expected outcomes, actual outcomes, prediction errors, related claims, and related procedures.
- [ ] Replay jobs prioritize weak, useful, risky, surprising, stale, often-used, or contested memories and cannot directly promote truth.
- [ ] Procedure skills include preconditions, steps, postconditions, failure modes, validation evidence, maturity, risk, automation binding, and source anchors.
- [ ] Draft or simulated procedures cannot become automatable workflow guidance without validation and review policy.
- [ ] Simulation and cross-project analogy outputs remain labeled speculative until source-backed and reviewed.

## Metamemory Answer Gate

- [ ] Answer readiness is evaluated using source sufficiency, context fit, belief state, confidence calibration, contradiction risk, staleness, redaction, risk level, and access policy.
- [ ] The answer gate can answer, answer with warnings, ask clarification, request source audit, start probe, create review item, request learning proposal, or abstain.
- [ ] Source-poor high-confidence answers are blocked or warning-rendered.
- [ ] Contested claims and high-risk unvalidated procedures trigger warning, review, clarification, source audit, or abstention.
- [ ] Answer gate decisions are included in recall/probe traces and visible in relevant UI/workbench proof.
