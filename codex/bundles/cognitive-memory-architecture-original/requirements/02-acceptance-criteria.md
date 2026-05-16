# Acceptance Criteria

## V1 Foundation

- [ ] A new Cognitive Memory module can be registered in the main solution.
- [ ] EF migrations/models exist for source manifests, source items, memory items, relations, projections, recall traces, consolidation runs, and review items.
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
- [ ] Recall records a trace with candidate scores and selected items.
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

- [ ] Unit tests cover scoring, activation, source hashing, relation detection, and projection mapping.
- [ ] Integration tests cover ingestion -> canonicalization -> projection -> recall.
- [ ] Negative tests cover secret redaction, stale data, contradictions, rejected merges, and Qdrant outage fallback.
- [ ] UI tests cover review queue and recall trace inspection.
- [ ] Tests reject scalar-only Epistemic Drive scoring.
- [ ] Negative tests cover unapproved external study, missing learning source refs, high-risk draft promotion, duplicate proposal creation, probing failure handling, and Qdrant outage during proposal generation.
