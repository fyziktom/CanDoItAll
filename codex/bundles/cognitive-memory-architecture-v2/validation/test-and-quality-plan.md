# Test and Quality Plan

## Test Taxonomy

| Test type | Purpose |
|---|---|
| Unit tests | score geometry, hashing, relation detection, activation, mapping. |
| Integration tests | DB + source ingest + projection + recall. |
| Contract tests | RAG adapter, embedding adapter, MAF executor contracts. |
| Golden dataset tests | ensure stable recall/clustering on known mindmaps. |
| Negative tests | stale data, contradictions, secret redaction, bad merges. |
| UI tests | review queue, recall trace viewer, memory detail. |
| Workflow tests | recall/consolidation executors inside workflow runtime. |
| Distributed worker tests | job claim, output validation, tampering rejection. |
| Epistemic Drive tests | coverage maps, gap detection, vector preservation, Pareto/ROI selection, proposal lifecycle. |
| Neuro-cognitive tests | workspace frames, attention routing, claim/evidence/belief state, prediction errors, salience signals, replay, procedural skills, simulation, and answer gating. |
| Score geometry tests | score-space definitions, vector snapshots, shape matching, missing dimensions, scalar projections, and trace reproducibility. |
| EF query-shape tests | no-tracking read queries, index coverage, paged result contracts, DTO projections, and no client evaluation on hot paths. |
| Performance guardrail scans | .NET performance anti-pattern scan for hot-path implementation files plus allocation review for vector/context-pack paths. |

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
- Large Workbench source fixtures prove source scanning is paged and does not materialize unbounded graph/source data before pagination.

## EF And Persistence Tests

- All read-only query handlers used by source lists, memory lists, review queues, trace viewers, probe sessions, proposal lists, and projection health use no-tracking reads.
- List/detail screens project to DTOs and do not load broad entity graphs through accidental `Include` chains.
- Required unique/indexed keys exist for source items, memory source refs, relations, projections, recall traces, review items, proposals, and probe/regression records.
- Required unique/indexed keys exist for evidence anchors, claims, claim/evidence links, mutation commands/audits, entity aliases, context frames, workspace slots, attention decisions, prediction errors, cognitive signals, episodes, replay jobs, procedure skills, simulation records, and answer-gate decisions.
- Required unique/indexed keys exist for score spaces, vector snapshots, score components, shape snapshots, evaluation traces, and scalar projections where those records are queried by project, owner, score space, dimension, schema version, or timestamp.
- Query-relevant evidence/source/review/proposal/probe/neuro-cognitive state is available through relational columns/tables, not JSON-only lookup.
- Hot-path recall candidate queries and projection-state queries are bounded by configured limits.
- Client-side query evaluation warnings fail tests.
- Bulk state transitions are either set-based or explicitly justified by audit/side-effect requirements.

## Performance Scan Checklist

- No sync-over-async wrappers on hot paths.
- No unbounded `IReadOnlyList<T>` results for source scans, traces, review queues, relation queries, proposal lists, or probe lists.
- No repeated vector `float[]` copying outside adapter or serialization boundaries.
- No uncached `JsonSerializerOptions` on durable payload hot paths.
- No broad string comparison without explicit `StringComparison` in new parser/filter code.
- No silent provider fallback; lower-quality fallback requires active mode permission and trace evidence.
- No scalar-only salience or untyped status/mode/operation fields in neuro-cognitive hot paths.
- No behavior-affecting scalar-only score, priority, confidence, weight, or score-breakdown dictionary in recall, attention, belief, replay, probing, answer gate, Epistemic Drive, or cross-project promotion hot paths.

## Score Geometry Tests

- Score-space registry returns versioned definitions for recall candidate, attention routing, belief state, salience signal, replay priority, probe assessment, answer gate, Epistemic need, cross-project promotion, procedure maturity, mindmap similarity, and memory activation.
- Vector snapshots preserve score space kind, schema version, normalization profile, typed dimensions, confidence, evidence refs, algorithm version, calculated timestamp, and input hash.
- Shape evaluation supports point vector, weighted region, centroid/radius, threshold envelope, boundary plane, Pareto frontier, and time-decayed trajectory where applicable.
- Missing required dimensions fail or warn according to score-space policy; they do not silently become zero or neutral.
- Scalar projections are reproducible from evaluation traces and are labeled as display, UI sorting, queue ordering, or tie-breaker only.
- Qdrant vector similarity enters recall as `SemanticSimilarity` or provider-specific projection evidence, not as final rank.
- Docker production/test/local/CI fixture proves context-separation boundary shapes inhibit semantically similar but operationally incompatible candidates.
- Analyzer or grep checks reject `FinalScore`, behavior-affecting `Priority`, untyped `ScoreBreakdown`, and `Dictionary<string,double>` scoring surfaces outside the score geometry foundation.

## Neuro-Cognitive Tests

### Cognitive Workspace And Attention

- Create workspace frame for probe session, MAF agent run, workflow run, process step, review session, and learning task.
- Add focus slots and verify context budget enforcement.
- Add inhibited candidates and verify recall/probe trace includes inhibition reason.
- Expired workspace frame does not become durable source truth.
- Important workspace frame can be persisted as episodic source input only through governed source/evidence policy.
- Ambiguous query routes to clarification.
- Source-sensitive query routes to source audit.
- Weak topic routes to probe before learning.
- Sufficient workspace routes to answer from workspace.
- High-risk unsupported procedure routes to review or abstention.
- Routing decision includes score vector, matched shape, scalar projection, missing dimensions, and explanation.

### Claim/Evidence/Belief And Mutation Authority

- One memory item can contain multiple claims with different belief states.
- Claim with supporting and attacking evidence becomes contested.
- Claim with source version change becomes stale or needs review.
- Unsupported generated summary cannot be promoted.
- Evidence anchor stores source item id, structured path, text span, quote hash, trust level, redaction state, and source hash/version.
- Claim-level contradiction is visible even when memory item summary is fluent.
- Duplicate mutation command with same idempotency key is idempotent.
- Mutation with stale version token is rejected or sent to review.
- High-risk claim mutation requires human review.
- Mutation invalidates relevant projection records after durable write.
- Audit event includes actor, evidence, policy decision, timestamps, and algorithm/profile version.

### Entity/Context Binding

- Production Docker and test Docker are related but not substitutable.
- Alias resolution maps names to entity ids with source evidence.
- Context frame includes project/environment/runtime/process/role/time/source-trust/risk/access dimensions.
- Recall filters or inhibits candidates by context boundary.
- Cross-project entity merge requires policy and approved reusable source.

### Prediction Error And Signal Ledger

- Overconfident incorrect probe answer creates prediction error and calibration-risk signal.
- Workflow failure creates procedure-failed prediction error and rework-cost signal.
- Confirmed useful procedure creates usefulness/reward signal.
- Stale source creates staleness-pressure signal.
- Signals preserve dimensions and do not collapse into one score.
- High salience cannot bypass access policy or source truth.
- Signal, prediction error, attention decision, and answer gate records are traceable to evidence, actor, time, and algorithm/profile version.

### Temporal Episode And Replay

- Episode preserves ordered steps and actors.
- Episode links decisions, artifacts, prediction errors, claims, and procedures.
- Query "why did we do this?" can retrieve decision episode and source evidence.
- Probe session can become episodic source input without becoming truth.
- High-risk stale procedure is prioritized over low-risk stable fact.
- Repeated wrong-scope prediction errors create context-boundary replay job.
- Failed probe regression creates replay job.
- Replay job output creates draft review/projection invalidation only.
- Distributed replay result with wrong input hash is rejected.

### Procedural Skill And Simulation

- Procedure skill includes preconditions, steps, postconditions, failure modes, evidence, maturity, risk, and automation binding.
- Draft skill cannot be used as automatable workflow template.
- Validated skill can suggest workflow/template promotion only under policy.
- Failure mode updates from prediction error evidence.
- Simulation output remains speculative until reviewed.
- Cross-project analogy output remains speculative and access-policy filtered.

### Metamemory Answer Gate

- Source-poor answer triggers source audit or warning.
- Ambiguous context triggers clarification.
- Contested claim triggers warning, review, or abstention.
- High-risk procedure without validation triggers abstention/review.
- Redaction-limited answer explains limitation.
- Answer gate decision is included in recall/probe trace.

## Activation Tests

Test score-geometry effects:

- human approval changes the memory activation vector and derived projection,
- stale state changes staleness pressure and derived projection,
- recent successful use changes usefulness/reward/recency dimensions,
- contradiction changes contradiction pressure and can inhibit context-pack selection,
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
- Knowledge need vectors reference generic `EpistemicNeed` score vector snapshots and evaluation traces.
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
- No salience implementation stores only a final priority score.
- No learning task reads external sources without required approval.
- No learning-derived record replaces human-validated memory silently.
- No simulation output or probe correction directly promotes active truth.
- No public authoritative write path bypasses mutation authority.

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
