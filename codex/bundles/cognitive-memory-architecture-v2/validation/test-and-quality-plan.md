# Test and Quality Plan

## Test Taxonomy

| Test type | Purpose |
|---|---|
| Unit tests | scoring, hashing, relation detection, activation, mapping. |
| Integration tests | DB + source ingest + projection + recall. |
| Contract tests | RAG adapter, embedding adapter, MAF executor contracts. |
| Golden dataset tests | ensure stable recall/clustering on known mindmaps. |
| Negative tests | stale data, contradictions, secret redaction, bad merges. |
| UI tests | review queue, recall trace viewer, memory detail. |
| Workflow tests | recall/consolidation executors inside workflow runtime. |
| Distributed worker tests | job claim, output validation, tampering rejection. |
| Epistemic Drive tests | coverage maps, gap detection, vector preservation, Pareto/ROI selection, proposal lifecycle. |

## Golden Mindmap Test

Create a small fixed project mindmap with:

- production Docker deployment node,
- testing Docker simulation node far away spatially,
- CI Docker test node,
- local development Docker Compose node,
- unrelated UI testing node,
- explicit links and parent branches.

Expected behavior:

- Docker nodes are semantically related.
- Production and testing Docker are context-separated.
- Deployment project summary includes both production and testing options.
- Procedure search for "test simulation" prioritizes test Docker.
- Production deployment recall does not use test-only configuration as authoritative.

## Source Hashing Tests

- Same source ingested twice does not create duplicates.
- Changed notes update source hash and enqueue reprocessing.
- Metadata-only change updates metadata hash if configured.
- Deleted source produces retire/supersede candidate, not immediate raw deletion.

## Activation Tests

Test score effects:

- human approval boosts retrieval,
- stale state penalizes retrieval,
- recent successful use boosts activation,
- contradiction penalizes context-pack selection,
- high semantic score alone cannot override access policy.

## Projection Tests

- Projection payload includes all required fields.
- Projection rebuild deletes old points for same memory item/profile.
- Qdrant outage falls back to lexical/graph recall.
- Filter by project/type/scope/tags works.
- Records with secret classification are not embedded.

## Recall Tests

- Recall trace contains all stages.
- Selected candidates include selection reasons.
- Context pack is token-limited.
- Context pack keeps source references.
- Recall for procedure intent prefers procedural memory over generic semantic topics.
- Recall for decision history includes episodic/decision records.

## Consolidation Tests

- Changed sources are detected.
- New canonical records are generated.
- Ambiguous merges create review tasks.
- Contradictions create contradiction candidates.
- Supersession updates stale records.
- Run report is persisted.
- Epistemic Drive stages run after activation/staleness/contradiction analysis.
- Duplicate scans do not create duplicate learning proposals.

## Epistemic Drive Tests

- Knowledge need vectors preserve all dimensions.
- Candidate selection stores Pareto rank, category, ROI estimate, and explanation.
- Scalar-only scoring is rejected by contract/model tests.
- Proposal explanations cite evidence refs.
- Docker operational knowledge fixture produces weak subareas for Compose, volumes, networking, secrets/configs, Swarm, and non-happy paths.
- Active project direction intersection raises relevant weak regions.
- Low uncertainty/low usage topics do not create urgent proposals.
- High uncertainty/low usage topics are tracked as known unknowns, not auto-studied.
- Source availability and source quality affect category/action without erasing risk or uncertainty dimensions.
- Learning proposal state transitions are audited.
- Learning outcome records remain draft until validation requirements are met.

## Probing Integration Tests

- Epistemic Drive generates probing question sets from gap regions.
- Failed probing answers increase gap evidence without overwriting validated memory.
- Successful probing can increase confidence/coverage through an auditable update.
- Probing before learning can cancel or narrow a learning task.
- Probing after learning validates improvement and updates the coverage map.

## Security Tests

- Secret-like values are redacted before embedding.
- Memory access policy denies unauthorized agent/user roles.
- External model context policy blocks restricted records.
- Prompt-injection-like source text is marked as untrusted source text, not instruction.
- External source study is blocked without required approval.
- Learning-derived canonical records without source refs are rejected.
- High-risk learning-derived procedures cannot become active without human review.
- Cross-project learning proposals do not expose project-private source text without approval.

## Distributed Compute Tests

- Worker can claim only compatible jobs.
- Worker output with wrong input hash is rejected.
- Worker output with wrong algorithm version is rejected.
- Duplicate result submission is idempotent.
- Worker cannot directly apply memory mutations.
- Worker cannot directly approve proposals, create learning outcomes, or update projections.

## Review Checklist Before Merge

- Build passes.
- Unit tests pass.
- Integration tests pass where environment is available.
- All new EF entities have configurations.
- All public contracts have stable names.
- No source comments are non-English.
- No generated memory item lacks source refs.
- No Qdrant point lacks payload metadata.
- No secret-like data is projected.
- No Epistemic Drive implementation stores only a final priority score.
- No learning task reads external sources without required approval.
- No learning-derived record replaces human-validated memory silently.

## Interactive Memory Probing Tests

- Probe session start/list/resume/close tests.
- Manual question creates probe turn, recall request, recall trace, answer metadata, and findings.
- Dialogue Workbench browser test shows answer, trace, source refs, confidence, warnings, and actions.
- Feedback action tests for confirm, correct, missing, wrong-scope, request-source, create-review, create-regression-test, and request-learning-proposal.
- Correction evidence tests prove active/canonical memory is not mutated directly.
- High-risk correction requires human review before memory promotion.
- Probe question generation uses weak coverage, stale records, contradictions, active directions, recall failures, and controlled serendipity.
- Probe outcomes publish `KnowledgeGapEvidenceRef` for Epistemic Drive.
- Regression test replay stores pass/fail result and new recall trace id.
- Confidence calibration tests cover high-confidence rejected answers, low-confidence confirmed answers, wrong-scope answers, missing-source answers, and redaction-limited answers.
- Docker context-separation probe pack catches production/test/local/CI conflation.
- Qdrant outage fallback still allows probing through lexical/graph/source recall.
